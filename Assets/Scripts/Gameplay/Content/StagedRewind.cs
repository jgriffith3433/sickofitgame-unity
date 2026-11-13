using System;
using UnityEngine;
using SickOfIt.Combat.Health;
using SickOfIt.Core;
using SickOfIt.Core.Fx;
using SickOfIt.Json;

namespace SickOfIt.Gameplay.Content
{
    [Serializable, JsonType("stagedRewind")]
    [Doc("The first rewind, taught by a hit nobody can dodge (Level 1). The first time it runs in a level: a ring of 'shot' converges on the hero, the hit drops time to 'slowScale' " +
         "(presentation only, like the hit-freeze, until a rewind starts), the HUD rewind button grows and pulses, the hero says 'line' with one short caption, and the slow motion holds " +
         "for 1.5 real seconds at most (or until the player rewinds), then time returns by itself; when that rewind ends he says 'reliefLine'. Every later run (the replay after that rewind) fires one crossing 'shot' he can jump instead.")]
    public sealed class StagedRewindAction : GameAction
    {
        [Required, EntityRef] public string shot = "";
        [Doc("How far from the hero the ring starts, world units.")] public float distance = 2.4f;
        public float speed = 5.5f;
        [Doc("Time scale held after the hit.")] public float slowScale = 0.2f;
        [SoundRef] public string line = "";
        [Doc("The one caption (6 words at most).")] public string lineText = "";
        [SoundRef] public string reliefLine = "";
        public string reliefText = "";

        public override void Run(ActionContext context)
        {
            var world = context.World;
            var hero = world.PlayerController;
            if (hero == null)
                return;
            Vector2 at = hero.transform.position;
            bool first = FirstRewindStage.Begin(world, this);
            if (!first)
            {
                // The replay: one ember crossing at his height, high enough to jump.
                world.Spawn(shot, at + new Vector2(5.5f, 0.2f), new SpawnOptions { Velocity = new Vector2(-4f, 0f), FacePlayer = false });
                return;
            }
            foreach (float deg in new[] { 0f, 180f, 40f, 140f, 75f, 105f })
            {
                var dir = new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));
                world.Spawn(shot, at + dir * distance, new SpawnOptions { Velocity = -dir * speed, FacePlayer = false });
            }
        }
    }

    /// <summary>
    /// The staged first rewind's presentation: watches the hero's hearts after the ring is fired, slows time on the hit, holds it until a rewind
    /// starts, and answers the rewind's end. Nothing here is saved or rewound; the world's own state is never touched.
    /// </summary>
    public sealed class FirstRewindStage : MonoBehaviour
    {
        public enum Stage { Idle, Armed, Slow, Released, Rewinding, Done }

        /// <summary>The slow motion's hard cap in real (unscaled) seconds: it can never hold longer, rewind or not.</summary>
        public const float SlowSeconds = 1.5f;
        /// <summary>After the slow motion lets go the button keeps pulsing this long, so the lesson can still be taken.</summary>
        const float PulseAfterSeconds = 4f;

        static FirstRewindStage instance;
        static GameplayWorld stagedIn;

        public static Stage Current { get; private set; } = Stage.Idle;
        /// <summary>True while the HUD's rewind button should grow and pulse.</summary>
        public static bool Pulsing => Current == Stage.Slow || Current == Stage.Released;
        float slowAt;
        /// <summary>Times it happened this session (playtest).</summary>
        public static int Hits { get; private set; }
        public static int Relieved { get; private set; }

        StagedRewindAction action;
        GameplayWorld world;
        PlayerHealth health;
        int hearts;
        float armedAt;

        /// <summary>True the first time in this world (stage it); false on the replay after the rewind.</summary>
        public static bool Begin(GameplayWorld w, StagedRewindAction a)
        {
            if (stagedIn == w)
                return false;
            var hero = w.PlayerController;
            var hp = hero != null ? hero.GetComponent<PlayerHealth>() : null;
            // Safe by rule: never stage a hit that could be his last heart (the replay's jumpable ember runs instead).
            if (hp == null || hp.CurrentHealth <= 1)
                return false;
            stagedIn = w;
            if (instance == null)
            {
                var go = new GameObject("[FirstRewindStage]");
                instance = go.AddComponent<FirstRewindStage>();
            }
            instance.world = w;
            instance.action = a;
            instance.health = hp;
            instance.hearts = hp.CurrentHealth;
            instance.armedAt = Time.unscaledTime;
            Current = Stage.Armed;
            return true;
        }

        void Update()
        {
            if (Current == Stage.Idle || Current == Stage.Done)
                return;
            var clock = GameClock.Instance;
            if (world == null || health == null || clock == null)
            {
                EndSlow();
                Current = Stage.Idle;
                return;
            }
            bool rewinding = clock.IsRewinding;
            // The hero died during the beat: the beat is over and time is his again.
            if (health.CurrentHealth <= 0 && Current != Stage.Rewinding)
            {
                EndSlow();
                Current = Stage.Done;
                return;
            }
            switch (Current)
            {
                case Stage.Armed:
                    if (rewinding || Time.unscaledTime - armedAt > 4f)
                        Current = Stage.Done;
                    else if (health.CurrentHealth < hearts)
                    {
                        Current = Stage.Slow;
                        Hits++;
                        slowAt = Time.unscaledTime;
                        // The rest of the ring cannot finish him while time is slow: invincible through the beat.
                        health.SetInvincibilityTimer(SlowSeconds + 0.5f);
                        HitFreeze.Freeze(SlowSeconds, action.slowScale, 0f, "firstRewind");
                        world.Hud?.ShowText(action.lineText, TextStyle.Subtitle, new Color(1f, 0.96f, 0.84f), world.Now + SlowSeconds + 0.5f);
                        SickOfIt.Core.Audio.Sfx.Play(action.line);
                    }
                    break;
                case Stage.Slow:
                    if (rewinding)
                    {
                        Current = Stage.Rewinding;
                        EndSlow();
                    }
                    else if (Time.unscaledTime - slowAt >= SlowSeconds)
                    {
                        // Hard cap, real time: back to full speed on its own; the button keeps pulsing a little longer.
                        EndSlow();
                        Current = Stage.Released;
                    }
                    break;
                case Stage.Released:
                    if (rewinding)
                        Current = Stage.Rewinding;
                    else if (Time.unscaledTime - slowAt >= SlowSeconds + PulseAfterSeconds)
                        Current = Stage.Done;
                    break;
                case Stage.Rewinding:
                    if (!rewinding)
                    {
                        Current = Stage.Done;
                        Relieved++;
                        // The win: a bright gold flash and a rising sparkle, so the first rewind lands as a success.
                        world.Effects?.Flash(new Color(1f, 0.9f, 0.45f, 0.55f), 0.35f);
                        SickOfIt.Core.Audio.Sfx.Play("pickup_rewind");
                        world.Hud?.ShowText(action.reliefText, TextStyle.Subtitle, new Color(1f, 0.96f, 0.84f), world.Now + 1.6f);
                        SickOfIt.Core.Audio.Sfx.Play(action.reliefLine);
                    }
                    break;
            }
        }

        /// <summary>Puts time back and takes the caption down: every way out of the slow motion goes through here.</summary>
        void EndSlow()
        {
            HitFreeze.Cancel();
            world?.Hud?.ShowText("", TextStyle.Subtitle, Color.white, 0f);
        }

        void OnDisable() => HitFreeze.Cancel();

        void OnDestroy()
        {
            HitFreeze.Cancel();
            if (instance == this)
                instance = null;
            Current = Stage.Idle;
        }
    }
}

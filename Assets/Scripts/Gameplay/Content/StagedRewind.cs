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
         "until the player rewinds; when that rewind ends he says 'reliefLine'. Every later run (the replay after that rewind) fires one crossing 'shot' he can jump instead.")]
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
        public enum Stage { Idle, Armed, Slow, Rewinding, Done }

        static FirstRewindStage instance;
        static GameplayWorld stagedIn;

        public static Stage Current { get; private set; } = Stage.Idle;
        /// <summary>True while the HUD's rewind button should grow and pulse.</summary>
        public static bool Pulsing => Current == Stage.Slow;
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
            if (hp == null)
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
                Current = Stage.Idle;
                return;
            }
            bool rewinding = clock.IsRewinding;
            switch (Current)
            {
                case Stage.Armed:
                    if (rewinding || Time.unscaledTime - armedAt > 4f)
                        Current = Stage.Done;
                    else if (health.CurrentHealth < hearts)
                    {
                        Current = Stage.Slow;
                        Hits++;
                        HitFreeze.Freeze(600f, action.slowScale, 0f, "firstRewind");
                        world.Hud?.ShowText(action.lineText, TextStyle.Subtitle, new Color(1f, 0.96f, 0.84f), float.PositiveInfinity);
                        SickOfIt.Core.Audio.Sfx.Play(action.line);
                    }
                    break;
                case Stage.Slow:
                    if (rewinding)
                    {
                        Current = Stage.Rewinding;
                        HitFreeze.Cancel();
                        world.Hud?.ShowText("", TextStyle.Subtitle, Color.white, 0f);
                    }
                    break;
                case Stage.Rewinding:
                    if (!rewinding)
                    {
                        Current = Stage.Done;
                        Relieved++;
                        world.Hud?.ShowText(action.reliefText, TextStyle.Subtitle, new Color(1f, 0.96f, 0.84f), world.Now + 1.6f);
                        SickOfIt.Core.Audio.Sfx.Play(action.reliefLine);
                    }
                    break;
            }
        }

        void OnDestroy()
        {
            if (instance == this)
                instance = null;
            Current = Stage.Idle;
        }
    }
}

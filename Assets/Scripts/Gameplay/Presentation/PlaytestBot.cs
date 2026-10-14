using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Core.Input;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// A simple automatic player for playtests: shoots the nearest enemy, keeps a comfortable distance from
    /// threats, and jumps over incoming shots. It drives PlayerController through its input override, so
    /// it plays the real game. Its choices come from a seeded random stream, so runs are repeatable.
    /// </summary>
    public sealed class PlaytestBot : MonoBehaviour, IPlayerInputSource
    {
        public float preferredDistanceMin = 2.5f;
        public float preferredDistanceMax = 5f;
        public float dodgeRadius = 1.6f;

        GameplayWorld world;
        PlayerController controller;
        DeterministicRandom random;
        bool jumpPending;
        float nextJumpCheck;
        float strafeUntil;
        float strafeDirection = 1f;

        public Vector2 Move { get; private set; }
        public Vector2 FireDirection { get; private set; }

        public bool ConsumeJump()
        {
            bool j = jumpPending;
            jumpPending = false;
            return j;
        }

        public static PlaytestBot Attach(GameplayWorld world, uint seed = 12345)
        {
            var controller = world.Player != null ? world.Player.GetComponent<PlayerController>() : null;
            if (controller == null)
                return null;
            var bot = world.gameObject.AddComponent<PlaytestBot>();
            bot.world = world;
            bot.controller = controller;
            bot.random = new DeterministicRandom(seed);
            controller.InputOverride = bot;
            return bot;
        }

        void OnDestroy()
        {
            if (controller != null && ReferenceEquals(controller.InputOverride, this))
                controller.InputOverride = null;
        }

        void Update()
        {
            if (world == null || world.Player == null)
                return;
            if (world.Hud.PickerOpen)
            {
                world.Hud.Choose(0);
                return;
            }
            var clock = GameClock.Instance;
            float now = clock != null ? clock.Time : Time.time;
            Vector2 me = world.Player.Position;

            Entity target = null;
            Entity threat = null;
            float targetDistance = float.MaxValue;
            float threatDistance = float.MaxValue;
            foreach (var e in world.Live)
            {
                if (!e.IsAlive || !world.Player.IsHostileTo(e))
                    continue;
                float d = Vector2.Distance(me, e.Position);
                bool shootable = e.MaxHealth > 0;
                if (shootable && d < targetDistance)
                {
                    target = e;
                    targetDistance = d;
                }
                if (!shootable && d < threatDistance)
                {
                    threat = e;
                    threatDistance = d;
                }
            }

            FireDirection = target != null ? (target.Position + Vector2.up * 0.1f - me).normalized : Vector2.zero;

            HandleRewind(now);

            float moveX = 0f;
            if (target != null)
            {
                float dx = target.Position.x - me.x;
                if (targetDistance < preferredDistanceMin)
                    moveX = -Mathf.Sign(dx);
                else if (targetDistance > preferredDistanceMax)
                    moveX = Mathf.Sign(dx);
            }
            if (now < strafeUntil)
                moveX = strafeDirection;
            else if (random.Value() < 0.004f)
            {
                strafeDirection = random.Value() < 0.5f ? -1f : 1f;
                strafeUntil = now + random.Range(0.3f, 0.8f);
            }

            // Dodge: find the shot that will pass closest within the next second and step out of its way.
            bool jumpToDodge = false;
            float closestMiss = float.MaxValue;
            foreach (var e in world.Live)
            {
                if (!e.IsAlive || e.MaxHealth > 0 || !world.Player.IsHostileTo(e) || e.Body == null)
                    continue;
                Vector2 v = e.Body.linearVelocity;
                Vector2 rel = e.Position - me;
                float speed2 = v.sqrMagnitude;
                if (speed2 < 0.01f)
                    continue;
                float t = Mathf.Clamp(-Vector2.Dot(rel, v) / speed2, 0f, 1f);
                float miss = (rel + v * t).magnitude;
                if (miss > dodgeRadius * 0.45f || miss >= closestMiss)
                    continue;
                closestMiss = miss;
                bool fromAbove = v.y < -Mathf.Abs(v.x);
                if (fromAbove)
                    moveX = (rel.x + v.x * t) > 0f ? -1f : 1f;
                else
                    jumpToDodge = true;
            }

            var view = world.View;
            if (me.x < view.xMin + 0.8f) moveX = 1f;
            if (me.x > view.xMax - 0.8f) moveX = -1f;
            Move = new Vector2(moveX, 0f);

            if (now >= nextJumpCheck)
            {
                nextJumpCheck = now + 0.2f;
                if (jumpToDodge || (threat != null && threatDistance < dodgeRadius * 0.5f) || random.Value() < 0.04f)
                    jumpPending = true;
            }
        }

        // ─── Rewind like a player would: when hit, go back a second if there's juice ───

        public bool useRewind = true;
        int lastHealth = -1;
        float rewindTarget = -1f;

        void HandleRewind(float now)
        {
            var clock = GameClock.Instance;
            if (clock == null)
                return;
            if (clock.IsRewinding)
            {
                if (rewindTarget >= 0f && now <= rewindTarget)
                {
                    clock.StopRewind();
                    rewindTarget = -1f;
                }
                return;
            }
            rewindTarget = -1f;

            int health = world.Player.Health;
            bool hit = lastHealth >= 0 && health < lastHealth;
            lastHealth = health;
            if (!hit || !useRewind || now < 2f)
                return;
            var budget = SickOfIt.Core.Rewind.RewindBudget.Instance;
            if (budget == null || budget.Juice < 1.2f)
                return;
            rewindTarget = now - 1.2f;
            clock.StartRewind(3f);
            lastHealth = -1;
        }
    }
}

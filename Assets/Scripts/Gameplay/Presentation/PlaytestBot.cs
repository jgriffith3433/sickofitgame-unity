using UnityEngine;
using UnityEngine.InputSystem;
using SickOfIt.Core;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// A simple automatic player for playtests: shoots the nearest enemy, keeps a comfortable distance from
    /// threats, jumps over incoming shots, rewinds when hit and takes the first upgrade offered. It plays through
    /// the real input path, pressing keys and sticks on <see cref="PlaytestInput"/>'s virtual devices, so it hits
    /// what a player would. Its choices come from a seeded random stream, so runs are repeatable.
    /// Used by the Hawkes playtest scenarios (SickOfIt.Playtest) in builds and by the PlayMode playtests.
    /// </summary>
    public sealed class PlaytestBot : MonoBehaviour
    {
        public float preferredDistanceMin = 2.5f;
        public float preferredDistanceMax = 5f;
        public float dodgeRadius = 1.6f;

        GameplayWorld world;
        PlaytestInput input;
        bool ownsInput;
        DeterministicRandom random;
        float nextJumpCheck;
        float strafeUntil;
        float strafeDirection = 1f;
        int releaseJumpFrame = -1;
        int releasePickFrame = -1;

        public Vector2 Move { get; private set; }
        public Vector2 FireDirection { get; private set; }
        /// <summary>Rewinds the bot started (each one a hold of R).</summary>
        public int RewindsStarted { get; private set; }

        /// <summary>Starts playing the level. Uses the given input devices, or makes its own (removed with the bot).</summary>
        public static PlaytestBot Attach(GameplayWorld world, uint seed = 12345, PlaytestInput input = null)
        {
            if (world == null || world.Player == null)
                return null;
            var bot = world.gameObject.AddComponent<PlaytestBot>();
            bot.world = world;
            bot.ownsInput = input == null;
            bot.input = input ?? PlaytestInput.Create();
            bot.random = new DeterministicRandom(seed);
            return bot;
        }

        void OnDestroy()
        {
            if (input == null)
                return;
            if (ownsInput)
                input.Dispose();
            else
                input.ReleaseAll();
        }

        void Press(Key key, ref int releaseFrame)
        {
            input.SetKey(key, true);
            releaseFrame = Time.frameCount + 2;
        }

        void ReleaseTaps()
        {
            if (releaseJumpFrame >= 0 && Time.frameCount >= releaseJumpFrame)
            {
                input.SetKey(Key.Space, false);
                releaseJumpFrame = -1;
            }
            if (releasePickFrame >= 0 && Time.frameCount >= releasePickFrame)
            {
                input.SetKey(Key.Digit1, false);
                releasePickFrame = -1;
            }
        }

        void Idle()
        {
            Move = Vector2.zero;
            FireDirection = Vector2.zero;
            input.SetMoveX(0f);
            input.SetAim(Vector2.zero);
        }

        void Update()
        {
            if (world == null || world.Player == null || input == null || !input.Alive)
                return;
            ReleaseTaps();
            if (world.Hud != null && world.Hud.PickerOpen)
            {
                Idle();
                if (releasePickFrame < 0 && !input.IsHeld(Key.Digit1))
                    Press(Key.Digit1, ref releasePickFrame);
                return;
            }
            if (world.Hud != null && (world.Hud.Paused || world.Hud.ResultsOpen))
            {
                Idle();
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
            // A travel stage only ends by going somewhere: with nothing to shoot, keep walking.
            bool travelling = world.Effects.Mode == SickOfIt.Gameplay.Content.CameraMode.Travel
                && world.Effects.Track != null && world.Director.Script.world.travelDirection == SickOfIt.Gameplay.Content.TravelDirection.Right;
            if (travelling && target == null)
                moveX = 1f;
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
            input.SetMoveX(moveX);
            input.SetAim(FireDirection);

            if (now >= nextJumpCheck)
            {
                nextJumpCheck = now + 0.2f;
                if ((jumpToDodge || (threat != null && threatDistance < dodgeRadius * 0.5f) || random.Value() < 0.04f)
                    && releaseJumpFrame < 0 && !input.IsHeld(Key.Space))
                    Press(Key.Space, ref releaseJumpFrame);
            }
        }

        // ─── Rewind like a player would: when hit, hold R for about a second if there's juice ───

        public bool useRewind = true;
        int lastHealth = -1;
        float rewindTarget = -1f;
        float holdStartedAt = -1f;

        /// <summary>Holds R to rewind about 'seconds' of game time (if the game allows it now). Returns true if it pressed.</summary>
        public bool RewindNow(float seconds = 1.2f)
        {
            var clock = GameClock.Instance;
            if (clock == null || clock.IsRewinding || input == null || input.IsHeld(Key.R))
                return false;
            var budget = SickOfIt.Core.Rewind.RewindBudget.Instance;
            if (budget != null && !budget.CanRewind)
                return false;
            rewindTarget = clock.Time - seconds;
            holdStartedAt = Time.unscaledTime;
            input.SetKey(Key.R, true);
            RewindsStarted++;
            lastHealth = -1;
            return true;
        }

        void HandleRewind(float now)
        {
            var clock = GameClock.Instance;
            if (clock == null)
                return;
            if (input.IsHeld(Key.R))
            {
                // Let go once far enough back, or if the game didn't start a rewind (too early, no juice).
                bool reached = clock.IsRewinding && now <= rewindTarget;
                bool refused = !clock.IsRewinding && Time.unscaledTime - holdStartedAt > 0.4f;
                if (reached || refused || Time.unscaledTime - holdStartedAt > 5f)
                {
                    input.SetKey(Key.R, false);
                    rewindTarget = -1f;
                }
                return;
            }
            if (clock.IsRewinding)
                return;

            int health = world.Player.Health;
            bool hit = lastHealth >= 0 && health < lastHealth;
            lastHealth = health;
            if (!hit || !useRewind || now < 2f)
                return;
            var budget = SickOfIt.Core.Rewind.RewindBudget.Instance;
            if (budget == null || budget.Juice < 1.2f)
                return;
            RewindNow(1.2f);
        }
    }
}

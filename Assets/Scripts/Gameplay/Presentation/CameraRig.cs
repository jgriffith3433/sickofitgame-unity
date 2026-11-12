using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// Presentation-only camera direction on top of WorldEffects' simulated camera: look-ahead toward the hero's movement,
    /// a 10% zoom-out when a floor's finale starts, a 5% punch-in with slow motion on the last kill of a wave, and a short
    /// punch-in when a rewind undoes the hero's death. Nothing here is saved or rewound. Story beats override it while they run.
    /// Every camera event goes to the telemetry as kind "camera" (subject lookAhead / zoom / slowmo).
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public const float LookAheadUnits = 0.6f, LookAheadSmooth = 0.2f, VerticalBias = 0.25f;
        const float FinaleZoom = 1.10f, FinaleSeconds = 1f;
        const float PunchZoom = 0.95f, PunchSlowScale = 0.3f, PunchSeconds = 0.4f, RewindPunchSeconds = 0.3f;

        GameplayWorld world;
        Vector2 offset, offsetVel, lastHero, heroVel;
        bool haveHero, reachedLog;
        float baseZoom = 1f, zoom = 1f, zoomVel, appliedZoom = 1f;
        float finaleZoom = 1f, finaleFrom = 1f, finaleStart = -1f;
        float punchUntil = -1f, punchStart = -1f;
        float heroDeathAt = -1000f;
        bool heroDiedSinceRewind;

        /// <summary>The current look-ahead offset in world units.</summary>
        public Vector2 Offset => offset;
        /// <summary>The current zoom factor on the orthographic size (1 = base).</summary>
        public float Zoom => appliedZoom;

        public void Init(GameplayWorld owner)
        {
            world = owner;
            GameplayTelemetry.Recorded += OnRecorded;
            var clock = GameClock.Instance;
            if (clock != null)
            {
                clock.OnRewindStarted += OnRewindStarted;
                clock.OnRewindStopped += OnRewindStopped;
            }
        }

        void OnDestroy()
        {
            GameplayTelemetry.Recorded -= OnRecorded;
            var clock = GameClock.Instance;
            if (clock != null)
            {
                clock.OnRewindStarted -= OnRewindStarted;
                clock.OnRewindStopped -= OnRewindStopped;
            }
            if (world != null && world.Camera != null && world.Camera.orthographic && appliedZoom > 0f)
                world.Camera.orthographicSize /= appliedZoom;
        }

        bool Owned(GameplayTelemetry t) => world != null && t != null && t.World == world;

        bool BeatRunning => world.Director != null && world.Director.Beats != null && world.Director.Beats.Active;

        void OnRecorded(GameplayTelemetry t, GameplayTelemetry.Event e)
        {
            if (!Owned(t) || BeatRunning)
                return;
            if (e.Kind == "signal" && e.Subject != null && e.Subject.StartsWith("floors:") && e.Subject.EndsWith(":finale"))
            {
                finaleFrom = finaleZoom;
                finaleStart = Time.unscaledTime;
                finaleZoom = FinaleZoom;
                world.Telemetry.Record("camera", "zoom", Mathf.RoundToInt(FinaleZoom * 100f), "finale out 1s");
            }
            else if (e.Kind == "death")
            {
                if (world.PlayerController != null && e.Subject == world.Player?.DefId)
                {
                    heroDeathAt = Time.unscaledTime;
                    heroDiedSinceRewind = true;
                }
                else if (NoFoesLeft() && world.Player.IsAlive)
                    Punch(PunchSeconds, true, "last kill");
            }
        }

        bool NoFoesLeft()
        {
            foreach (var e in world.Live)
                if (e.IsAlive && !e.IsPlayer && e.Def != null && e.Def.team == SickOfIt.Gameplay.Content.Team.Enemy && e.FindAbility<SickOfIt.Gameplay.Content.ProjectileAbility>() == null)
                    return false;
            return true;
        }

        void OnRewindStarted() => heroDiedSinceRewind = heroDiedSinceRewind && Time.unscaledTime - heroDeathAt < 5f;

        void OnRewindStopped()
        {
            if (heroDiedSinceRewind && !BeatRunning)
                Punch(RewindPunchSeconds, false, "rewind undo");
            heroDiedSinceRewind = false;
        }

        void Punch(float seconds, bool slow, string why)
        {
            punchStart = Time.unscaledTime;
            punchUntil = punchStart + seconds;
            world.Telemetry.Record("camera", "zoom", Mathf.RoundToInt(PunchZoom * 100f), why);
            if (slow && world.Effects != null)
            {
                world.Effects.SlowMotion(PunchSlowScale, seconds);
                world.Telemetry.Record("camera", "slowmo", Mathf.RoundToInt(PunchSlowScale * 100f), seconds.ToString("0.00"));
            }
        }

        /// <summary>The offset to add to the camera's place this frame (zero while a beat runs).</summary>
        public Vector2 Step(float dt)
        {
            if (world == null || world.Camera == null || world.Player == null)
                return Vector2.zero;
            dt = Mathf.Max(dt, 0.0001f);
            bool beat = BeatRunning;
            var hero = world.Player.Position;
            if (haveHero)
                heroVel = Vector2.Lerp(heroVel, (hero - lastHero) / dt, 1f - Mathf.Exp(-12f * dt));
            lastHero = hero;
            haveHero = true;

            // Half the look-ahead on a portrait phone so the hero stays in the middle third.
            float share = world.Camera.aspect < 1f ? 0.5f : 1f;
            Vector2 target = Vector2.zero;
            if (!beat && !(GameClock.Instance != null && GameClock.Instance.IsRewinding))
            {
                if (Mathf.Abs(heroVel.x) > 0.5f)
                    target.x = Mathf.Sign(heroVel.x) * LookAheadUnits * share;
                if (Mathf.Abs(heroVel.y) > 0.5f)
                    target.y = Mathf.Sign(heroVel.y) * VerticalBias * share;
            }
            offset = new Vector2(
                Mathf.SmoothDamp(offset.x, target.x, ref offsetVel.x, LookAheadSmooth, Mathf.Infinity, dt),
                Mathf.SmoothDamp(offset.y, target.y, ref offsetVel.y, LookAheadSmooth, Mathf.Infinity, dt));
            if (!reachedLog && offset.magnitude >= 0.4f * share)
            {
                reachedLog = true;
                world.Telemetry.Record("camera", "lookAhead", Mathf.RoundToInt(offset.magnitude * 100f), $"{offset.x:0.00},{offset.y:0.00}");
            }
            else if (reachedLog && offset.magnitude < 0.05f)
                reachedLog = false;

            float want = baseZoom;
            if (finaleStart >= 0f)
            {
                float k = Mathf.Clamp01((Time.unscaledTime - finaleStart) / FinaleSeconds);
                k = k * k * (3f - 2f * k);
                want *= Mathf.Lerp(finaleFrom, finaleZoom, k);
            }
            if (!beat && Time.unscaledTime < punchUntil)
            {
                float u = Mathf.Clamp01((Time.unscaledTime - punchStart) / (punchUntil - punchStart));
                want *= Mathf.Lerp(PunchZoom, 1f, u * u);
            }
            zoom = want;
            ApplyZoom(zoom);
            return offset;
        }

        void ApplyZoom(float z)
        {
            var cam = world.Camera;
            if (cam == null || !cam.orthographic || Mathf.Approximately(z, appliedZoom))
                return;
            cam.orthographicSize = cam.orthographicSize / appliedZoom * z;
            appliedZoom = z;
        }
    }
}

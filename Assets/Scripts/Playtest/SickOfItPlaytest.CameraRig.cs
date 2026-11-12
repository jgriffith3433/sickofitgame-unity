using System.Collections;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.InputSystem;
using SickOfIt.Core;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The camera rig: Floor 1000 (level 2) loaded directly, real input (the move stick on a portrait phone, D otherwise).
        /// Checks the look-ahead (&gt;= 0.4 units within 0.5 s; half on portrait, hero in the middle third of the width),
        /// the finale zoom-out (&gt;= 8% within 1 s), then waits for a last-kill punch-in with slow-mo (camera events slowmo/zoom)
        /// and reports fps in slow-mo (&gt;= 30, unscaled). Metrics cameraRig.*.
        /// </summary>
        public const string CameraRigId = "camera-rig";

        sealed partial class Session
        {
            public IEnumerator CameraRigRun()
            {
                const int level = 2;
                var load = LoadLevel(level);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => LevelRunning(level) && GameplayWorld.Instance != null && GameplayWorld.Instance.Effects != null && GameplayWorld.Instance.Effects.Rig != null, 60f, "the level");
                if (Run.Done)
                    yield break;
                var world = GameplayWorld.Instance;
                var rig = world.Effects.Rig;
                var hero = world.PlayerController;
                yield return Run.Until(() => hero.IsGrounded, 20f, "the hero on the ground");
                yield return WaitReal(0.5f);
                bool phone = world.Camera.aspect < 1f;
                float need = phone ? 0.2f : 0.4f;

                int slow = 0, zooms = 0;
                void OnCam(GameplayTelemetry t, GameplayTelemetry.Event e)
                {
                    if (e.Kind != "camera") return;
                    Run.Event("camera", $"{e.Subject} {e.Value} {e.Detail}");
                    if (e.Subject == "slowmo") slow++;
                    if (e.Subject == "zoom") zooms++;
                }
                GameplayTelemetry.Recorded += OnCam;
                try
                {
                    float t0 = Time.unscaledTime, best = 0f, worstX = 0.5f, bestX = 0.5f;
                    if (phone) input.SetMoveStick(new Vector2(1f, 0f)); else input.SetKey(Key.D, true);
                    float reached = -1f;
                    while (!Run.Done && Time.unscaledTime - t0 < 1.5f)
                    {
                        yield return null;
                        best = Mathf.Max(best, Mathf.Abs(rig.Offset.x));
                        if (reached < 0f && best >= need) reached = Time.unscaledTime - t0;
                        float sx = world.Camera.WorldToViewportPoint(hero.transform.position).x;
                        worstX = Mathf.Min(worstX, sx); bestX = Mathf.Max(bestX, sx);
                    }
                    if (phone) input.SetMoveStick(Vector2.zero); else input.SetKey(Key.D, false);
                    Run.Metric("cameraRig.lookAheadUnits", best);
                    Run.Metric("cameraRig.lookAheadSeconds", reached);
                    CameraRigCheck("look-ahead", reached > 0f && reached <= 0.5f, $"{best:0.00} units, reached {need:0.0} in {reached:0.00} s");
                    if (phone)
                        CameraRigCheck("hero in the middle third", worstX >= 1f / 3f && bestX <= 2f / 3f, $"x {worstX:0.00}-{bestX:0.00}");

                    float z0 = rig.Zoom;
                    world.Emit("floors:1000:finale");
                    yield return WaitReal(1.05f);
                    float pct = (rig.Zoom / z0 - 1f) * 100f;
                    Run.Metric("cameraRig.finaleZoomPct", pct);
                    CameraRigCheck("finale zoom-out", pct >= 8f, $"{pct:0.0}% in 1 s");

                    float until = Time.unscaledTime + 60f, worstFps = 999f;
                    while (!Run.Done && Time.unscaledTime < until && slow == 0) yield return null;
                    int frames = 0; float ft = Time.unscaledTime;
                    while (!Run.Done && slow > 0 && Time.unscaledTime - ft < 0.5f) { frames++; yield return null; }
                    if (frames > 0) worstFps = frames / (Time.unscaledTime - ft);
                    Run.Metric("cameraRig.slowMoFps", worstFps);
                    CameraRigCheck("last kill slow-mo", slow > 0, slow > 0 ? "slow-mo seen" : "no last kill within 60 s (the bot does not fight here)");
                    CameraRigCheck("fps in slow-mo", worstFps >= 30f, $"{worstFps:0} fps");
                }
                finally { GameplayTelemetry.Recorded -= OnCam; }
            }

            void CameraRigCheck(string beat, bool ok, string detail)
            {
                Run.Event("check", $"camera-rig {beat}: {(ok ? "ok" : "FAILED")} ({detail})");
                if (!ok) Run.Problem(PlaytestSeverity.Bug, $"camera-rig {beat}: {detail}");
            }
        }
    }
}

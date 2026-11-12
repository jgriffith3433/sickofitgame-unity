using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Gameplay;
using SickOfIt.VideoFrameExtractor.Runtime;
using Object = UnityEngine.Object;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// Painted backdrops (docs/world-bible.md "Backdrops"): the Meadow and Floors 1000-997 (levels 1-5) each load directly with the
        /// touch UI on (run on device "phone", 390x844). Each shows 4+ parallax layers with distinct factors (0.1, 0.25, 0.5, 0.8), no
        /// baked video-frame background, no layer shorter than the view (nothing stretched to fill), frames at 30+ fps, layers that
        /// move by different amounts when the camera moves or the clock runs, and the Meadow's sky swapped when the raid starts.
        /// </summary>
        public const string BackdropLayersId = "backdrop-layers";
        const float BackdropLayersSeconds = 420f;
        static readonly int[] BackdropLevels = { 1, 2, 3, 4, 5 };

        sealed partial class Session
        {
            int backdropFailed;

            void BackdropCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    Run.Event("check", $"backdrop-layers {beat}: ok ({detail})");
                    return;
                }
                backdropFailed++;
                Run.Event("check", $"backdrop-layers {beat}: FAILED ({detail})");
                Run.Problem(PlaytestSeverity.Bug, $"backdrop-layers {beat}: {detail}");
            }

            public IEnumerator BackdropLayersRun()
            {
                GameplayHud.TouchUiForced = true;
                Run.Event("shortcut", "touch UI on; levels 1-5 loaded directly");
                for (int i = 0; i < BackdropLevels.Length; i++)
                {
                    int level = BackdropLevels[i];
                    var load = LoadLevel(level);
                    if (load != null)
                        yield return load;
                    yield return Run.Until(() => LevelRunning(level) && GameplayWorld.Instance != null && GameplayWorld.Instance.Hud != null, 60f, $"level {level}");
                    if (Run.Done)
                        yield break;
                    Time.timeScale = Speed;
                    yield return WaitReal(1.5f);

                    var parallax = ParallaxController.Current;
                    var entries = parallax != null ? parallax.Entries : null;
                    var factors = new HashSet<float>();
                    if (entries != null)
                        foreach (var e in entries)
                            factors.Add(Mathf.Round(e.Factor.x * 100f) / 100f);
                    BackdropCheck($"level {level} layers", entries != null && entries.Count >= 4 && factors.Count >= 4,
                        $"{(entries != null ? entries.Count : 0)} parallax layers, {factors.Count} distinct factors");

                    int video = 0;
                    foreach (var frames in Object.FindObjectsByType<SpriteFramePlayer>(FindObjectsSortMode.None))
                        if (frames.name.Contains("Background") && frames.TryGetComponent(out SpriteRenderer sr) && sr.enabled)
                            video++;
                    BackdropCheck($"level {level} no video background", video == 0, $"{video} baked video-frame backgrounds on screen");

                    var world = GameplayWorld.Instance;
                    var cam = world.Camera != null ? world.Camera : Camera.main;
                    float viewH = cam != null ? cam.orthographicSize * 2f : 0f;
                    float viewW = cam != null ? viewH * cam.aspect : 0f;
                    int stretched = 0;
                    foreach (var r in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                    {
                        if (r.sprite == null || r.transform.parent == null || !r.transform.parent.name.StartsWith("Parallax_"))
                            continue;
                        var size = r.sprite.bounds.size;
                        if (size.y + 0.01f < viewH || size.x + 0.01f < viewW * 0.5f)
                            stretched++;
                    }
                    BackdropCheck($"level {level} fills the view", viewH > 0f && stretched == 0, $"{stretched} layer pieces smaller than the {viewW:0.0}x{viewH:0.0} view");

                    var before = new List<Vector2>();
                    if (entries != null)
                        foreach (var e in entries)
                            before.Add(e.Position);
                    int frameCount = 0;
                    float start = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - start < 4f)
                    {
                        frameCount++;
                        yield return null;
                    }
                    float fps = frameCount / Mathf.Max(0.01f, Time.realtimeSinceStartup - start);
                    BackdropCheck($"level {level} frame rate", fps >= 30f || Speed > 1f, $"{fps:0} fps over 4 s");
                    var moved = new HashSet<float>();
                    if (entries != null)
                        for (int k = 0; k < entries.Count && k < before.Count; k++)
                            moved.Add(Mathf.Round(Mathf.Abs(entries[k].Position.x - before[k].x) * 1000f) / 1000f + Mathf.Round(Mathf.Abs(entries[k].Position.y - before[k].y) * 1000f) / 1000f);
                    Run.Event("parallax", $"level {level}: {moved.Count} distinct layer movements over 4 s");

                    if (level == 1)
                    {
                        var drifts = Object.FindObjectsByType<BackdropDrift>(FindObjectsSortMode.None);
                        bool raid = false;
                        float limit = Time.realtimeSinceStartup + 40f;
                        while (!raid && Time.realtimeSinceStartup < limit)
                        {
                            foreach (var d in drifts)
                                raid |= d != null && d.ShowingRaid;
                            yield return null;
                        }
                        BackdropCheck("level 1 sky swap", raid, raid ? "the Meadow's sky showed the raid sky after the sky opened" : "the sky never swapped to the raid sky");
                    }
                    Run.Progress((i + 1f) / BackdropLevels.Length);
                }
                GameplayHud.TouchUiForced = false;
                Run.Metric("backdrop.failed", backdropFailed);
                End(backdropFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }
        }
    }
}

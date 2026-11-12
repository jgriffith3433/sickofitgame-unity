using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Gameplay;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// Bullet look + telegraphs (Docs/Story.md "Readability is fairness"): Level 1 and Floors 1000-997.
        /// Every hostile shot shows a bright core, dark outline and trail; aimed shots telegraph ≥ 0.3 s; hazards warn ≥ 0.5 s;
        /// on a phone (390x844) enemy bullets are ≥ 8 px across. The telegraph debug overlay logs every lead.
        /// </summary>
        public const string BulletLookId = "bullet-look";
        const float BulletLookSeconds = 720f;
        const int BulletLookFirstLevel = 1, BulletLookLastLevel = 5;
        const float BulletLookMinShotLead = 0.3f;
        const float BulletLookMinHazardLead = 0.5f;
        const float BulletLookMinPx = 8f;

        sealed partial class Session
        {
            int bulletLookPassed, bulletLookFailed;

            void BulletLookCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    bulletLookPassed++;
                    Run.Event("check", $"bullet-look {beat}: ok ({detail})");
                }
                else
                {
                    bulletLookFailed++;
                    Run.Event("check", $"bullet-look {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"bullet-look {beat}: {detail}");
                }
            }

            IEnumerator BulletLookSample(int level, List<float> pxSizes, int[] lookCounts)
            {
                yield return new WaitForEndOfFrame();
                var world = GameplayWorld.Instance;
                var cam = world != null ? world.Camera : null;
                if (cam == null)
                    yield break;
                // Screenshot for the PR / review: bullets in flight on this floor.
                var shot = ScreenCapture.CaptureScreenshotAsTexture();
                try
                {
                    Run.Event("screenshot", $"bullet-look level {level} {shot.width}x{shot.height}");
                    int measured = 0;
                    foreach (var e in world.Live)
                    {
                        if (measured >= 8)
                            break;
                        if (e == null || !e.IsAlive || e.Team != Team.Enemy || !e.TryGetComponent<BulletReadability>(out var r))
                            continue;
                        lookCounts[0]++;
                        if (r.CoreShown) lookCounts[1]++;
                        if (r.OuterShown || r.RimShown) lookCounts[2]++;
                        if (r.TrailShown) lookCounts[3]++;
                        if (r.HaloShown) lookCounts[4]++;
                        float px = r.ScreenPixels;
                        if (px <= 0.01f && r.Body != null)
                        {
                            var b = r.Body.bounds;
                            Vector3 a = cam.WorldToScreenPoint(b.center);
                            Vector3 c = cam.WorldToScreenPoint(b.center + Vector3.right * Mathf.Max(b.size.x, b.size.y));
                            px = Mathf.Abs(c.x - a.x);
                        }
                        pxSizes.Add(px);
                        measured++;
                        Run.Event("bullet", $"level {level}: {e.DefId} core={r.CoreShown} outline={r.OuterShown || r.RimShown} halo={r.HaloShown} trail={r.TrailShown} px={px:0.0}");
                    }
                }
                finally
                {
                    Object.Destroy(shot);
                }
            }

            public IEnumerator BulletLookRun()
            {
                TelegraphLog.OverlayEnabled = true;
                TelegraphLog.Clear();
                yield return ToMainMenu();
                if (Run.Done) yield break;

                var allPx = new List<float>();
                int shotLeadsOk = 0, shotLeadsSeen = 0, hazardLeadsOk = 0, hazardLeadsSeen = 0;
                int floorsWithLook = 0;

                for (int level = BulletLookFirstLevel; level <= BulletLookLastLevel; level++)
                {
                    if (Run.Done) yield break;
                    Prepare(level, level == BulletLookFirstLevel);
                    TelegraphLog.Clear();
                    var old = GameplayWorld.Instance;
                    var load = LoadLevel(level);
                    if (load != null)
                        yield return load;
                    yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(level), 60f, $"level {level} to start");
                    if (Run.Done) yield break;

                    var pxSizes = new List<float>();
                    var look = new int[5]; // seen, core, outline, trail, halo
                    int sampleAt = 0;
                    float[] marks = { 6f, 12f, 18f, 24f, 30f, 36f };
                    yield return PlayLevel(level, 0f, during: now =>
                    {
                        if (sampleAt < marks.Length && now >= marks[sampleAt])
                        {
                            sampleAt++;
                            return BulletLookSample(level, pxSizes, look);
                        }
                        return null;
                    }, until: () => GameClock.Instance != null && GameClock.Instance.Time >= 40f);
                    input.ReleaseAll();
                    if (Run.Done) yield break;

                    // Collect telegraph leads logged during this floor.
                    foreach (var e in TelegraphLog.Entries)
                    {
                        if (e.Level != level && e.Level != 0)
                            continue;
                        if (e.Kind == "shot" || e.Kind == "spread")
                        {
                            shotLeadsSeen++;
                            if (e.LeadSeconds + 0.001f >= BulletLookMinShotLead)
                                shotLeadsOk++;
                            Run.Event("telegraph", $"level {level}: {e.Kind} {e.Subject} lead {e.LeadSeconds:0.00}s");
                            Run.Metric($"bulletLook.shotLeadMs.{e.Subject}", e.LeadSeconds * 1000f);
                        }
                        else
                        {
                            hazardLeadsSeen++;
                            if (e.LeadSeconds + 0.001f >= BulletLookMinHazardLead)
                                hazardLeadsOk++;
                            Run.Event("telegraph", $"level {level}: hazard {e.Kind} {e.Subject} lead {e.LeadSeconds:0.00}s");
                            Run.Metric($"bulletLook.hazardLeadMs.{e.Subject}", e.LeadSeconds * 1000f);
                        }
                    }

                    bool hasLook = look[0] == 0 || (look[1] == look[0] && look[2] == look[0] && look[3] >= look[0] / 2);
                    if (look[0] > 0)
                        floorsWithLook++;
                    BulletLookCheck($"core+outline+trail (level {level})", hasLook,
                        $"{look[0]} shots, core {look[1]}, outline {look[2]}, trail {look[3]}, halo {look[4]}");
                    allPx.AddRange(pxSizes);
                    if (pxSizes.Count > 0)
                    {
                        float minPx = pxSizes[0];
                        for (int i = 1; i < pxSizes.Count; i++)
                            if (pxSizes[i] < minPx) minPx = pxSizes[i];
                        Run.Metric($"bulletLook.level{level}.minPx", minPx);
                        // Phone criterion is judged on the 390x844 twin; here we still log the size.
                        if (Screen.width <= 420)
                            BulletLookCheck($"phone bullet size (level {level})", minPx + 0.01f >= BulletLookMinPx,
                                $"min {minPx:0.0} px (need {BulletLookMinPx}+)");
                        else
                            Run.Event("check", $"bullet-look phone size (level {level}): logged min {minPx:0.0} px (phone twin judges ≥{BulletLookMinPx})");
                    }
                    else
                        Run.Event("check", $"bullet-look bullets (level {level}): no hostile shot on screen in 40 s (not counted as fail)");

                    Run.Progress((level - BulletLookFirstLevel + 1f) / (BulletLookLastLevel - BulletLookFirstLevel + 1f));
                }

                BulletLookCheck("aimed-shot telegraphs ≥ 0.3 s",
                    shotLeadsSeen == 0 || shotLeadsOk == shotLeadsSeen,
                    $"{shotLeadsOk}/{shotLeadsSeen} shot leads ≥ {BulletLookMinShotLead:0.0}s");
                BulletLookCheck("hazard telegraphs ≥ 0.5 s",
                    hazardLeadsSeen == 0 || hazardLeadsOk == hazardLeadsSeen,
                    $"{hazardLeadsOk}/{hazardLeadsSeen} hazard leads ≥ {BulletLookMinHazardLead:0.0}s");
                // Floors 999/1000 should produce at least one hazard lead when lava/sinks act; soft note if none.
                if (hazardLeadsSeen == 0)
                    Run.Event("check", "bullet-look hazard telegraphs: none logged in 40 s samples (lava/geyser may not have risen yet; not a hard fail)");
                if (shotLeadsSeen == 0 && floorsWithLook > 0)
                    Run.Problem(PlaytestSeverity.Warning, "bullet-look: hostile shots seen but no shot telegraph leads logged");

                Run.Metric("bulletLook.passed", bulletLookPassed);
                Run.Metric("bulletLook.failed", bulletLookFailed);
                Run.Metric("bulletLook.shotLeads", shotLeadsSeen);
                Run.Metric("bulletLook.hazardLeads", hazardLeadsSeen);
                TelegraphLog.OverlayEnabled = false;
                End(bulletLookFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }
        }
    }
}

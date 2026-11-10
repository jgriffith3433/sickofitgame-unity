using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Core.Rewind;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The rewind-juice scenario: a longer rewind with rewards, readable CLEAN KILL / bonus popups and REWIND readout
        /// over Level 1's bright sky/grass and Floor 955's dark cavern (captured at 1280x720 and 390x844), scale-pop,
        /// >= 0.6 s full-opacity hold, and non-overlapping popup stacks.
        /// </summary>
        public const string RewindJuiceId = "rewind-juice";
        const int RewindJuiceLevel = 1;
        const string RewindJuiceDarkFloor = "955";
        const float RewindJuiceSeconds = 300f;

        sealed partial class Session
        {
            int rewindJuicePassed, rewindJuiceFailed;

            void RewindJuiceCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    rewindJuicePassed++;
                    Run.Event("check", $"rewind-juice {beat}: ok ({detail})");
                }
                else
                {
                    rewindJuiceFailed++;
                    Run.Event("check", $"rewind-juice {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"rewind-juice {beat}: {detail}");
                }
            }

            static Color RewindJuiceSampleAround(Texture2D shot, Rect screenRect)
            {
                float sx = Screen.width > 0 ? (float)shot.width / Screen.width : 1f;
                float sy = Screen.height > 0 ? (float)shot.height / Screen.height : 1f;
                float xMin = screenRect.xMin * sx, xMax = screenRect.xMax * sx;
                float yMin = screenRect.yMin * sy, yMax = screenRect.yMax * sy;
                const float pad = 8f;
                var pts = new Vector2[]
                {
                    new Vector2(Mathf.Lerp(xMin, xMax, 0.2f), yMax + pad),
                    new Vector2(Mathf.Lerp(xMin, xMax, 0.5f), yMax + pad),
                    new Vector2(Mathf.Lerp(xMin, xMax, 0.8f), yMax + pad),
                    new Vector2(Mathf.Lerp(xMin, xMax, 0.2f), yMin - pad),
                    new Vector2(Mathf.Lerp(xMin, xMax, 0.5f), yMin - pad),
                    new Vector2(Mathf.Lerp(xMin, xMax, 0.8f), yMin - pad),
                    new Vector2(xMin - pad, (yMin + yMax) * 0.5f),
                    new Vector2(xMax + pad, (yMin + yMax) * 0.5f),
                };
                float r = 0f, g = 0f, b = 0f;
                int count = 0;
                for (int i = 0; i < pts.Length; i++)
                {
                    int px = Mathf.Clamp(Mathf.RoundToInt(pts[i].x), 1, shot.width - 2);
                    int py = Mathf.Clamp(Mathf.RoundToInt(pts[i].y), 1, shot.height - 2);
                    var c = shot.GetPixel(px, py);
                    r += c.r; g += c.g; b += c.b;
                    count++;
                }
                return count > 0 ? new Color(r / count, g / count, b / count, 1f) : Color.gray;
            }

            static bool RewindJuiceInsideScreen(Rect r) =>
                r.width > 1f && r.height > 1f && r.xMin >= -2f && r.yMin >= -2f
                && r.xMax <= Screen.width + 2f && r.yMax <= Screen.height + 2f;

            IEnumerator RewindJuiceCapturePass(string placeLabel, string metricPrefix)
            {
                string res = $"{Screen.width}x{Screen.height}";
                Run.Scene($"{placeLabel} ({res})");
                Canvas.ForceUpdateCanvases();
                yield return new WaitForEndOfFrame();

                var hud = GameplayWorld.Instance != null ? GameplayWorld.Instance.Hud : null;
                if (hud == null)
                {
                    RewindJuiceCheck($"{placeLabel} HUD present ({res})", false, "no HUD");
                    yield break;
                }

                bool hasPopup = HudFloaters.TryGetLastScreenRect(out var popupRect, out var popupTextCol);
                bool hasReadout = hud.TryGetRewindReadoutScreenRect(out var readoutRect, out var readoutTextCol);

                var shot = ScreenCapture.CaptureScreenshotAsTexture();
                try
                {
                    if (hasPopup && shot != null)
                    {
                        var bg = RewindJuiceSampleAround(shot, popupRect);
                        var pillComp = Color.Lerp(bg, HudFloaters.PillInk, HudFloaters.LastPillAlpha);
                        var textOpaque = new Color(popupTextCol.r, popupTextCol.g, popupTextCol.b, 1f);
                        float textContrast = ContrastRatio(textOpaque, pillComp);
                        float badgeContrast = Mathf.Max(ContrastRatio(pillComp, bg), ContrastRatio(textOpaque, bg));
                        Run.Metric($"{metricPrefix}.popupContrast", textContrast);
                        Run.Event("capture", $"{placeLabel} ({res}) popup \"{HudFloaters.Last}\" at ({DescribeRect(popupRect)}): " +
                                             $"text #{ColorUtility.ToHtmlStringRGB(textOpaque)} on pill #{ColorUtility.ToHtmlStringRGB(pillComp)} " +
                                             $"over bg #{ColorUtility.ToHtmlStringRGB(bg)} (lum {Luminance(bg):0.00}) -> text/pill {textContrast:0.1}:1, vs bg {badgeContrast:0.1}:1");
                        RewindJuiceCheck($"{placeLabel} CLEAN KILL popup readable ({res})",
                            textContrast >= 4.5f && badgeContrast >= 3.5f && RewindJuiceInsideScreen(popupRect),
                            $"text/pill {textContrast:0.1}:1, vs bg #{ColorUtility.ToHtmlStringRGB(bg)} {badgeContrast:0.1}:1, rect ({DescribeRect(popupRect)}) on {res}");
                    }
                    else
                    {
                        RewindJuiceCheck($"{placeLabel} CLEAN KILL popup captured ({res})", false, $"hasPopup={hasPopup}");
                    }

                    if (hasReadout && shot != null)
                    {
                        var bg = RewindJuiceSampleAround(shot, readoutRect);
                        var pillComp = Color.Lerp(bg, HudFloaters.PillInk, 0.88f);
                        var textOpaque = new Color(readoutTextCol.r, readoutTextCol.g, readoutTextCol.b, 1f);
                        float textContrast = ContrastRatio(textOpaque, pillComp);
                        float badgeContrast = Mathf.Max(ContrastRatio(pillComp, bg), ContrastRatio(textOpaque, bg));
                        Run.Metric($"{metricPrefix}.readoutContrast", textContrast);
                        Run.Event("capture", $"{placeLabel} ({res}) REWIND readout at ({DescribeRect(readoutRect)}): " +
                                             $"text #{ColorUtility.ToHtmlStringRGB(textOpaque)} on pill #{ColorUtility.ToHtmlStringRGB(pillComp)} " +
                                             $"over bg #{ColorUtility.ToHtmlStringRGB(bg)} (lum {Luminance(bg):0.00}) -> text/pill {textContrast:0.1}:1, vs bg {badgeContrast:0.1}:1");
                        RewindJuiceCheck($"{placeLabel} REWIND readout readable ({res})",
                            textContrast >= 4.5f && badgeContrast >= 3.5f && RewindJuiceInsideScreen(readoutRect),
                            $"text/pill {textContrast:0.1}:1, vs bg #{ColorUtility.ToHtmlStringRGB(bg)} {badgeContrast:0.1}:1, rect ({DescribeRect(readoutRect)}) on {res}");
                    }
                    else
                    {
                        RewindJuiceCheck($"{placeLabel} REWIND readout captured ({res})", false, $"hasReadout={hasReadout}");
                    }

                    if (hasPopup && hasReadout)
                    {
                        RewindJuiceCheck($"{placeLabel} popup clears REWIND readout ({res})",
                            !OverlapsPx(popupRect, readoutRect),
                            $"popup ({DescribeRect(popupRect)}) vs readout ({DescribeRect(readoutRect)})");
                    }
                }
                finally
                {
                    if (shot != null)
                        UnityEngine.Object.Destroy(shot);
                }
            }

            IEnumerator RewindJuiceResizeCaptures(string placeLabel, string metricPrefix)
            {
                if (!CanResizeWindow)
                {
                    Run.Event("layout-size", $"{placeLabel} kept at {Screen.width}x{Screen.height}: WebGL uses the runner device's viewport (1280x720 desktop and 390x844 phone)");
                    yield break;
                }
                int w0 = Screen.width, h0 = Screen.height;
                var sizes = new (string tag, int w, int h)[]
                {
                    ("1280x720", 1280, 720),
                    ("390x844", 390, 844),
                };
                foreach (var sz in sizes)
                {
                    if (w0 == sz.w && h0 == sz.h)
                        continue;
                    Screen.SetResolution(sz.w, sz.h, false);
                    yield return Run.Until(() => Screen.width == sz.w && Screen.height == sz.h, 6f, $"resize to {sz.tag}");
                    if (Run.Done) yield break;
                    yield return WaitReal(0.25f);
                    var budget = RewindBudget.Instance;
                    if (budget != null)
                    {
                        budget.Configure(Mathf.Max(1f, budget.Max - 3f), budget.Max, 1f);
                        budget.Reward(0.5f, "CLEAN KILL");
                    }
                    yield return WaitReal(0.12f);
                    yield return RewindJuiceCapturePass(placeLabel, $"{metricPrefix}.{sz.tag}");
                    if (Run.Done) yield break;
                    yield return WaitReal(0.45f);
                }
                Screen.SetResolution(w0, h0, false);
                yield return Run.Until(() => Screen.width == w0 && Screen.height == h0, 6f, "restore window size");
                yield return WaitReal(0.2f);
            }

            public IEnumerator RewindJuiceRun()
            {
                var roguelite = RogueliteManager.Instance;
                var definition = GameDefinition.Instance;
                if (roguelite == null || definition == null)
                {
                    Run.Problem(PlaytestSeverity.Bug, "rewind-juice: no RogueliteManager or game definition");
                    End(PlaytestOutcome.Error);
                    yield break;
                }

                // 1. A new game: nothing bought.
                roguelite.ResetAllRogueliteData();
                Run.Event("shortcut", $"level {RewindJuiceLevel} loaded directly with the shop reset");
                var load = LoadLevel(RewindJuiceLevel);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => LevelRunning(RewindJuiceLevel) && RewindBudget.Instance != null, 60f, "the level's rewind juice");
                if (Run.Done)
                    yield break;
                var budget = RewindBudget.Instance;
                var config = definition.rewindBudget;
                Run.Metric("rewindJuice.newGame.juice", budget.Juice);
                Run.Metric("rewindJuice.newGame.max", budget.Max);
                RewindJuiceCheck("a new game starts with about 10 s", budget.Juice >= 9.9f && budget.Max >= 9.9f, $"juice {budget.Juice:0.0} s of {budget.Max:0.0} s (it was 3 s of 5 s)");
                Run.Event("rewind-juice", $"history window {definition.rewindMaxHistorySeconds:0} s every {definition.rewindRecordInterval:0.00} s; clean kill +{config.cleanKillRefund:0.0} s when not hurt for {config.cleanKillSeconds:0} s; " +
                                         $"survive {config.survivalSeconds:0} s after a death-rewind for +{config.survivalBonus:0.0} s");
                RewindJuiceCheck("the history window holds the whole cap", definition.rewindMaxHistorySeconds >= budget.Max, $"history {definition.rewindMaxHistorySeconds:0} s");

                // 2. The goods, bought through the shop's own purchase.
                roguelite.AddPoints(5000);
                foreach (var id in new[] { "juiceBox", "bottomlessJar", "quickUnwind" })
                {
                    UpgradeDefinition good = null;
                    foreach (var upgrade in RogueliteManager.ShopCatalog)
                    {
                        if (upgrade != null && upgrade.id == id)
                            good = upgrade;
                    }
                    int bought = 0;
                    while (good != null && roguelite.TryPurchase(good))
                        bought++;
                    RewindJuiceCheck($"shop sells {id}", good != null && bought == good.maxLevel, $"{bought} of {(good != null ? good.maxLevel : 0)} levels bought");
                }
                float expectedMax = config.maxSeconds + roguelite.GetShopTotal(SickOfIt.Data.Config.ShopEffect.RewindJuice);
                float speedBonus = roguelite.GetShopTotal(SickOfIt.Data.Config.ShopEffect.RewindSpeed);
                var old = GameplayWorld.Instance;
                load = LoadLevel(RewindJuiceLevel);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(RewindJuiceLevel) && RewindBudget.Instance != null, 60f, "the level with the goods");
                if (Run.Done)
                    yield break;
                budget = RewindBudget.Instance;
                Run.Metric("rewindJuice.upgraded.max", budget.Max);
                RewindJuiceCheck("the goods extend the cap much further", budget.Max >= expectedMax - 0.1f && budget.Max >= 24f, $"cap {budget.Max:0.0} s (expected {expectedMax:0.0} s); juice {budget.Juice:0.0} s");
                RewindJuiceCheck("rewind speed upgrade", speedBonus >= 0.74f, $"+{speedBonus * 100f:0}% rewind speed");
                Run.Progress(0.3f);

                // 3. Level 1 (bright sky and grass): play through real input until a clean kill pays, then check pop, hold >= 0.6 s, and capture.
                int rewards = 0;
                float lastJuice = budget.Juice;
                bool refundSeen = false;
                System.Action<float, string> onReward = (seconds, label) =>
                {
                    rewards++;
                    Run.Event("juice", $"{label} +{seconds:0.0} s, juice now {budget.Juice:0.0} s of {budget.Max:0.0} s");
                    if (label == "CLEAN KILL")
                        refundSeen = true;
                };
                budget.Rewarded += onReward;
                budget.Configure(budget.Max - 3f, budget.Max, 1f);
                budget.ConfigureRewards(config.survivalSeconds, config.survivalBonus);
                var world = GameplayWorld.Instance;
                yield return PlayLevel(RewindJuiceLevel, 0f, now => null, 0f, 1f, 200f, until: () => refundSeen && world.Hud.RewardsShown > 0);
                budget.Rewarded -= onReward;
                StopBot();
                if (Run.Done)
                    yield break;

                var hud = GameplayWorld.Instance != null ? GameplayWorld.Instance.Hud : null;
                RewindJuiceCheck("a clean kill refunds juice", refundSeen, $"{rewards} reward(s) paid; juice {lastJuice:0.0} -> {budget.Juice:0.0} s");
                RewindJuiceCheck("the HUD shows it", hud != null && hud.RewardsShown > 0, hud != null ? $"floated \"{hud.LastRewardShown}\" ({hud.RewardsShown} time(s))" : "no HUD");
                RewindJuiceCheck("the CLEAN KILL popup sits on a dark pill", HudFloaters.LastHadPill, $"popup \"{HudFloaters.Last}\" drawn over a pill (alpha {HudFloaters.LastPillAlpha:0.00})");
                RewindJuiceCheck("the REWIND readout sits on a dark pill", hud != null && hud.RewindReadoutHasPill, "the readout's pill is wider than its words and opaque enough to read on bright sky");

                // Fresh reward so scale-pop and hold are measured from a known spawn (the in-level kill may already have eased).
                yield return WaitReal(HudFloaters.Life + 0.05f);
                if (Run.Done)
                    yield break;
                budget = RewindBudget.Instance;
                if (budget == null)
                {
                    RewindJuiceCheck("rewind budget still alive for pop measure", false, "RewindBudget gone after Level 1 clean kill");
                    End(PlaytestOutcome.Error);
                    yield break;
                }
                budget.Configure(Mathf.Max(1f, budget.Max - 3f), budget.Max, 1f);
                budget.Reward(0.5f, "CLEAN KILL");
                float popSpawnMark = Time.realtimeSinceStartup;
                float peakScaleSeen = HudFloaters.LastCurrentScale;
                // Measure scale-pop during first ~0.3 s and capture the popup + REWIND readout over Level 1's bright sky/grass.
                for (int frame = 0; frame < 8; frame++)
                {
                    yield return null;
                    peakScaleSeen = Mathf.Max(peakScaleSeen, HudFloaters.LastCurrentScale);
                    if (Time.realtimeSinceStartup - popSpawnMark >= HudFloaters.PopSeconds)
                        break;
                }
                RewindJuiceCheck("popup pops in larger during first 0.3 s", peakScaleSeen > 1.08f, $"peak scale {peakScaleSeen:0.00}x over {HudFloaters.PopSeconds:0.00} s");
                yield return WaitReal(0.08f);
                yield return RewindJuiceCapturePass("Level 1 sky CLEAN KILL", "rewindJuice.sky");

                // Hold until >= 0.62 s after this spawn to prove full opacity holds >= 0.6 s in real time.
                float elapsedSinceSpawn = Time.realtimeSinceStartup - popSpawnMark;
                if (elapsedSinceSpawn < 0.62f)
                    yield return WaitReal(0.62f - elapsedSinceSpawn);
                float alphaAt06 = HudFloaters.LastCurrentAlpha;
                RewindJuiceCheck("popups hold full opacity 0.6 s or more",
                    HudFloaters.HoldSeconds >= 0.6f && alphaAt06 >= 0.99f,
                    $"HoldSeconds {HudFloaters.HoldSeconds:0.1} s, live alpha at 0.62 s = {alphaAt06:0.00}");
                yield return RewindJuiceResizeCaptures("Level 1 sky CLEAN KILL", "rewindJuice.sky");
                Run.Progress(0.6f);

                // 4. Stacking check: fire 3 rewards in quick succession and verify 3 live popups sit in distinct slots with 0 pixel overlap.
                yield return WaitReal(HudFloaters.Life + 0.1f);
                if (Run.Done)
                    yield break;
                budget = RewindBudget.Instance;
                if (budget != null)
                {
                    Run.Event("shortcut", "3 rewards in a row on Level 1 to verify non-overlapping popup stack");
                    budget.Configure(Mathf.Max(1f, budget.Max - 5f), budget.Max, 1f);
                    budget.Reward(0.5f, "CLEAN KILL");
                    yield return WaitReal(0.08f);
                    budget.Reward(0.5f, "CLEAN KILL");
                    yield return WaitReal(0.08f);
                    budget.Reward(1.5f, "SURVIVED");
                    yield return WaitReal(0.08f);

                    string stackRes = $"{Screen.width}x{Screen.height}";
                    Run.Scene($"Level 1 stacked popups ({stackRes})");
                    Canvas.ForceUpdateCanvases();
                    yield return new WaitForEndOfFrame();

                    var liveRects = new List<Rect>();
                    HudFloaters.GetActiveScreenRects(liveRects);
                    int stackOverlaps = 0, offScreen = 0;
                    for (int i = 0; i < liveRects.Count; i++)
                    {
                        if (!RewindJuiceInsideScreen(liveRects[i]))
                            offScreen++;
                        for (int j = i + 1; j < liveRects.Count; j++)
                        {
                            if (OverlapsPx(liveRects[i], liveRects[j]))
                                stackOverlaps++;
                        }
                    }
                    Run.Metric("rewindJuice.stackCount", liveRects.Count);
                    Run.Metric("rewindJuice.stackOverlaps", stackOverlaps);
                    RewindJuiceCheck("several popups in a row do not overlap",
                        liveRects.Count >= 3 && stackOverlaps == 0 && offScreen == 0 && HudFloaters.MaxSlotUsed >= 2,
                        $"{liveRects.Count} live popups in slots up to {HudFloaters.MaxSlotUsed} (last slot {HudFloaters.LastSlot}), {stackOverlaps} overlap(s), {offScreen} off-screen on {stackRes}");
                    yield return WaitReal(0.55f);
                }
                Run.Progress(0.75f);

                // 5. Dark floor (Floor 955, Ember Wastes cavern): try a real-input clean kill, then force a labeled
                // reward so contrast captures always run even when the bot can't clean-kill an elite floor in time.
                int darkLevel = ResolveAt(RewindJuiceDarkFloor, out string darkWhat);
                if (darkLevel > 0)
                {
                    bool darkRefund = false;
                    Prepare(darkLevel, true);
                    Run.Event("shortcut", $"load dark floor ({darkWhat}) for dark-background CLEAN KILL capture");
                    var prevWorld = GameplayWorld.Instance;
                    load = LoadLevel(darkLevel);
                    if (load != null)
                        yield return load;
                    yield return Run.Until(() => GameplayWorld.Instance != prevWorld && LevelRunning(darkLevel) && RewindBudget.Instance != null, 60f, "dark floor to start");
                    if (Run.Done)
                        yield break;

                    budget = RewindBudget.Instance;
                    System.Action<float, string> onDarkReward = (seconds, label) =>
                    {
                        Run.Event("juice", $"dark floor: {label} +{seconds:0.0} s");
                        if (label == "CLEAN KILL")
                            darkRefund = true;
                    };
                    budget.Rewarded += onDarkReward;
                    budget.Configure(Mathf.Max(1f, budget.Max - 3f), budget.Max, 1f);
                    var darkWorld = GameplayWorld.Instance;
                    int rewardsBefore = darkWorld.Hud.RewardsShown;
                    yield return PlayLevel(darkLevel, 0f, now => null, 0f, 1f, 20f,
                        until: () => darkRefund && darkWorld.Hud.RewardsShown > rewardsBefore);
                    budget.Rewarded -= onDarkReward;
                    StopBot();
                    if (Run.Done)
                        yield break;

                    RewindJuiceCheck("clean kill triggered over dark floor", darkRefund,
                        darkRefund ? $"{darkWhat}: real clean kill" : $"{darkWhat}: bot missed; forcing a CLEAN KILL reward for capture");
                    if (!darkRefund)
                    {
                        budget = RewindBudget.Instance;
                        if (budget != null)
                        {
                            budget.Configure(Mathf.Max(1f, budget.Max - 3f), budget.Max, 1f);
                            budget.Reward(0.5f, "CLEAN KILL");
                            darkRefund = true;
                            yield return WaitReal(0.08f);
                        }
                    }
                    if (darkRefund)
                    {
                        yield return WaitReal(0.12f);
                        yield return RewindJuiceCapturePass("Dark floor CLEAN KILL", "rewindJuice.dark");
                        yield return WaitReal(0.55f);
                        yield return RewindJuiceResizeCaptures("Dark floor CLEAN KILL", "rewindJuice.dark");
                    }
                    else
                    {
                        RewindJuiceCheck("dark floor CLEAN KILL popup ready", false, "no RewindBudget after dark floor load");
                    }
                }
                else
                {
                    RewindJuiceCheck("dark floor resolved", false, darkWhat);
                }

                Run.Metric("rewindJuice.passed", rewindJuicePassed);
                Run.Metric("rewindJuice.failed", rewindJuiceFailed);
                Run.Progress(1f);
                End(rewindJuiceFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core.Addressables;
using SickOfIt.Core.Progression;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// Death to play without a load (Docs/Story.md "Flow between attempts"; run it on device "phone", 390x844, touch): ten deaths in a row, each
        /// followed by a finger tap on Again!. The first three are the median reported as `againPhone.medianSeconds` (budget 1 s); no loading cover
        /// may show, frames stay at 30+ fps, and the next-floor warm owner never lingers after an arrival.
        /// </summary>
        public const string AgainPhoneId = "again-phone";
        const float AgainPhoneSeconds = 1500f;
        const int AgainPhoneDeaths = 10;

        sealed partial class Session
        {
            int againPhoneFailed;

            void AgainPhoneCheck(string beat, bool ok, string detail)
            {
                if (!ok) againPhoneFailed++;
                Run.Event("check", $"again-phone {beat}: {(ok ? "ok" : "FAILED")} ({detail})");
                if (!ok)
                    Run.Problem(PlaytestSeverity.Bug, $"again-phone {beat}: {detail}");
                Run.Metric("againPhone.failed", againPhoneFailed);
            }

            public IEnumerator AgainPhoneRun()
            {
                var definition = GameDefinition.Instance;
                var texts = definition.menuTheme.texts;
                int level = WaystoneLevel;
                yield return ToMainMenu();
                if (Run.Done) yield break;
                RogueliteManager.Instance.ResetAllRogueliteData();
                GameProgressManager.Instance.InitializeNewPlayer();
                GameProgressManager.Instance.SetLevelCompleted(1);
                GameProgressManager.Instance.SetLevelCompleted(LandingLevel);
                ShaftProgress.Floor = LandingLastFloor;
                ShaftProgress.Plunged = true;
                YoobaProgress.Met = true;
                WaystoneProgress.Reach(definition.shaft != null ? definition.shaft.bottomFloor : LandingFirstFloor);
                if (GrudgeCount() == 0)
                    foreach (var id in GameContent.Current.Upgrades.Keys) { UpgradeProgress.Add(id); break; }
                Run.Event("shortcut", $"level {level} loaded directly; the bot plays to lose {AgainPhoneDeaths} times, Again! by touch");
                var load = LoadLevel(level);
                if (load != null)
                    yield return load;

                var times = new List<float>();
                int covered = 0;
                float worstFrame = 0f, slowSeconds = 0f, frames = 0f;
                for (int i = 0; i < AgainPhoneDeaths; i++)
                {
                    reckless = true;
                    try { yield return PlayLevel(level, 0f, null, 0f, 1f, 150f, waitOutDeath: true); }
                    finally { reckless = false; }
                    if (Run.Done) yield break;
                    if (lastOutcome != "died") { AgainPhoneCheck($"death {i + 1}", false, $"the bot couldn't die on level {level} ({lastOutcome})"); break; }
                    yield return Run.Until(() => DeathRuntime.Current != null && DeathRuntime.Current.AgainOffered, 30f, "the death screen");
                    if (Run.Done) yield break;
                    yield return WaitReal(1.5f);
                    var death = DeathRuntime.Current;
                    MenuButton button = null;
                    yield return Run.Until(() => (button = FindButton(texts.deathAgain)) != null && button.IsInteractable(), 10f, "the Again! button");
                    if (Run.Done) yield break;
                    var at = ScreenPoint(button);
                    float tapped = Time.realtimeSinceStartup;
                    int finger = input.TouchDown(at);
                    yield return WaitReal(0.05f);
                    input.TouchUp(finger, at);
                    bool coverSeen = false;
                    yield return Run.Until(() =>
                    {
                        float dt = Time.unscaledDeltaTime;
                        frames++;
                        worstFrame = Mathf.Max(worstFrame, dt);
                        if (dt > 1f / 30f) slowSeconds += dt;
                        coverSeen |= ScreenTransition.LoadingVisible;
                        return LevelRunning(level);
                    }, 30f, $"level {level} after Again! {i + 1}");
                    if (Run.Done) yield break;
                    float took = Time.realtimeSinceStartup - tapped;
                    if (coverSeen) covered++;
                    if (i < 3) times.Add(took);
                    Run.Event("again", $"death {i + 1}: tap to play in {took:0.00} s{(coverSeen ? ", a loading cover showed" : "")}");
                    Run.Metric("againPhone.seconds." + (i + 1), took);
                    AgainPhoneCheck($"released {i + 1}", !GameAssets.Holds("levelNext"), "the next-floor warm owner is not held after the arrival");
                    Run.Progress((i + 1f) / AgainPhoneDeaths);
                }
                times.Sort();
                float median = times.Count > 0 ? times[times.Count / 2] : -1f;
                Run.Metric("againPhone.medianSeconds", median);
                AgainPhoneCheck("median", median >= 0f && median < AgainBudgetSeconds, $"Again! tap to play, median of the first 3: {median:0.00} s (budget {AgainBudgetSeconds:0.0} s)");
                AgainPhoneCheck("noCover", covered == 0, $"{covered} of {AgainPhoneDeaths} Again! taps showed a loading cover");
                float slowShare = frames > 0 ? slowSeconds : 0f;
                Run.Metric("againPhone.worstFrameMs", worstFrame * 1000f);
                AgainPhoneCheck("fps", worstFrame < 0.25f && slowShare < 1f, $"worst frame {worstFrame * 1000f:0} ms, {slowShare:0.0} s under 30 fps across the loads");
                End(againPhoneFailed == 0 ? PlaytestOutcome.Won : PlaytestOutcome.Done);
            }
        }
    }
}

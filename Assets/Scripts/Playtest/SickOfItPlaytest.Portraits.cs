using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core.Audio;
using SickOfIt.Core.Input;
using SickOfIt.Core.UI.Portraits;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Gameplay;
using SickOfIt.Gameplay.Content;
using LevelRuntime = SickOfIt.Core.Progression.LevelRuntime;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The speaker portraits scenario (Docs/Story.md "Portraits"). 1. Level 1's portal scene (a setup shortcut jumps to its first phase) plays uncut:
        /// the Overlord's and the hero's real voiced lines each show their painted portrait. 2. For each of the five characters (hero, Overlord, Yooba,
        /// Rook, Wick) real voiced lines of each of the four moods are played through the voice queue (a setup shortcut; the lines are the game's own
        /// audio.json clips). Every frame the card is up it is measured: 72+ px tall, fully on screen, slid in within 0.15 s, clear of the hero and the touch
        /// stick; the mouth opens and shuts while the line plays and is shut within 0.2 s of its end. Run it at 390x844 (touch) and desktop.
        /// `check` events portraits &lt;beat&gt;, `portrait` events, metrics portraits.*
        /// </summary>
        public const string PortraitsId = "portraits";
        const float PortraitsSeconds = 360f;

        sealed partial class Session
        {
            int ptPassed, ptFailed;
            readonly Dictionary<string, HashSet<string>> ptMoods = new Dictionary<string, HashSet<string>>();
            readonly List<string> ptEvents = new List<string>();
            float ptMinHeight = 9999f, ptMaxSlide, ptMaxCloseLag;
            bool ptOffScreen, ptOnHero, ptOnStick;
            int ptLines, ptLinesWithCard, ptFlapLines;

            void PtCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    ptPassed++;
                    Run.Event("check", $"portraits {beat}: ok ({detail})");
                    return;
                }
                ptFailed++;
                Run.Event("check", $"portraits {beat}: FAILED ({detail})");
                Run.Problem(PlaytestSeverity.Bug, $"portraits {beat}: {detail}");
            }

            void PtReported(string kind, string detail)
            {
                if (kind == "show")
                {
                    // "<id> <mood> (<sound>)"
                    var parts = detail.Split(' ');
                    if (parts.Length >= 2)
                    {
                        if (!ptMoods.TryGetValue(parts[0], out var set))
                            ptMoods[parts[0]] = set = new HashSet<string>();
                        set.Add(parts[1]);
                    }
                }
                if (kind == "show" || kind == "slid-in" || kind == "claimed")
                    Run.Event("portrait", $"{kind}: {detail}");
            }

            /// <summary>Watches one stretch frame by frame until 'until' (or 'limit' seconds): sizes, edges, the hero, the stick, the mouth.</summary>
            IEnumerator PtWatch(System.Func<bool> until, float limit)
            {
                float t0 = Time.realtimeSinceStartup;
                string lineSeen = "";
                bool wasSpeaking = false, wasOpen = false, lineHadCard = false;
                int toggles = 0;
                float endedAt = -1f;
                while (!Run.Done && Time.realtimeSinceStartup - t0 < limit && !until())
                {
                    var view = SpeakerPortraits.Instance;
                    bool speaking = PortraitVoice.Speaking;
                    if (speaking && PortraitVoice.Current != null && PortraitVoice.Current.id != lineSeen && PortraitLibrary.ForVoice(PortraitVoice.Voice) != null)
                    {
                        if (lineSeen != "")
                            PtEndLine(lineHadCard, toggles);
                        lineSeen = PortraitVoice.Current.id;
                        lineHadCard = false;
                        toggles = 0;
                        wasOpen = false;
                        ptLines++;
                    }
                    if (view != null && view.Visible)
                    {
                        if (speaking)
                            lineHadCard = true;
                        var r = view.ScreenRect;
                        ptMinHeight = Mathf.Min(ptMinHeight, r.height);
                        if (view.SlideInSeconds > 0f)
                            ptMaxSlide = Mathf.Max(ptMaxSlide, view.SlideInSeconds);
                        bool settled = view.SlideInSeconds > 0f;
                        if (settled)
                        {
                            var safe = Screen.safeArea;
                            if (r.xMin < safe.xMin - 1f || r.xMax > safe.xMax + 1f || r.yMin < safe.yMin - 1f || r.yMax > safe.yMax + 1f)
                                ptOffScreen = true;
                            if (HeroUnder(r))
                                ptOnHero = true;
                            if (TouchStickZone.Zone.HasValue && TouchStickZone.Zone.Value.Overlaps(r))
                                ptOnStick = true;
                        }
                        if (speaking && view.MouthIsOpen != wasOpen)
                        {
                            toggles++;
                            wasOpen = view.MouthIsOpen;
                        }
                        // The mouth must be shut within 0.2 s of the end of the line.
                        if (wasSpeaking && !speaking)
                            endedAt = Time.realtimeSinceStartup;
                        if (!speaking && endedAt >= 0f && view.MouthIsOpen)
                            ptMaxCloseLag = Mathf.Max(ptMaxCloseLag, Time.realtimeSinceStartup - endedAt);
                        else if (!speaking && endedAt >= 0f && !view.MouthIsOpen)
                            ptMaxCloseLag = Mathf.Max(ptMaxCloseLag, 0f);
                    }
                    wasSpeaking = speaking;
                    yield return null;
                }
                if (lineSeen != "")
                    PtEndLine(lineHadCard, toggles);
            }

            void PtEndLine(bool hadCard, int toggles)
            {
                if (hadCard)
                    ptLinesWithCard++;
                if (toggles >= 2)
                    ptFlapLines++;
            }

            static bool HeroUnder(Rect card)
            {
                var hero = PlayerController.Active;
                var cam = Camera.main;
                if (hero == null || cam == null)
                    return false;
                Vector3 a = cam.WorldToScreenPoint(hero.transform.position + new Vector3(-0.45f, -0.45f, 0f));
                Vector3 b = cam.WorldToScreenPoint(hero.transform.position + new Vector3(0.45f, 0.45f, 0f));
                var body = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
                return body.Overlaps(card);
            }

            /// <summary>The game's own first line of this voice and mood that has a clip.</summary>
            static string PtLineFor(string voice, string mood)
            {
                var sounds = GameDefinition.Instance != null && GameDefinition.Instance.audio != null ? GameDefinition.Instance.audio.sounds : null;
                if (sounds == null)
                    return null;
                foreach (var s in sounds)
                    if (s != null && s.kind == SoundKind.Voice && s.voice == voice && PortraitMood.Of(s) == mood && Sfx.Has(s.id))
                        return s.id;
                return null;
            }

            IEnumerator PtSay(string id)
            {
                Sfx.Play(id);
                yield return Run.Until(() => PortraitVoice.Current != null && PortraitVoice.Current.id == id, 4f, "line start " + id);
                yield return PtWatch(() => !PortraitVoice.Speaking, 20f);
                // Give the card its hold and its slide away before the next line (a different character slides in fresh).
                yield return PtWatch(() => SpeakerPortraits.Instance == null || !SpeakerPortraits.Instance.Visible, 2f);
            }

            public IEnumerator PortraitsRun()
            {
                bool phone = Screen.height > Screen.width;
                bool touchBefore = GameplayHud.TouchUiForced;
                if (phone)
                    GameplayHud.TouchUiForced = true;
                SpeakerPortraits.Reported += PtReported;
                GameplayTelemetry.Recorded += BdTelemetry;
                try
                {
                    var cfg = PortraitLibrary.Config;
                    PtCheck("config", cfg != null && cfg.enabled && cfg.characters.Count >= 5 && cfg.slideSeconds <= 0.15f, $"{cfg?.characters.Count} characters, slide {cfg?.slideSeconds:0.00} s");
                    int painted = 0;
                    if (cfg != null)
                        foreach (var c in cfg.characters)
                        {
                            int n = PortraitLibrary.PaintedMoods(c);
                            Run.Event("portrait", $"{c.id}: {n} painted moods, {c.frames.Count} frames");
                            if (n >= 4)
                                painted++;
                        }
                    Run.Metric("portraits.charactersPainted", painted);
                    if (painted < 5)
                        Run.Problem(PlaytestSeverity.Warning, $"portraits: only {painted} of 5 characters have all 4 moods painted; the rest show the stand-in bust");

                    // 1. The portal scene: the Overlord and the hero speak in a beat.
                    BeatRuntime.ForgetAll();
                    Run.Event("shortcut", "level 1 loaded directly, the portal scene's first phase entered (setup shortcut)");
                    yield return BdLoadLevel1(null);
                    if (Run.Done)
                        yield break;
                    GameplayWorld.Instance.Director.GoToPhase("portalBeam");
                    yield return PtWatch(() => BdHas("portal:black") || BdHas("beat:level1Portal:end"), 18f);
                    PtCheck("beat lines", ptLinesWithCard >= 4 && ptLinesWithCard == ptLines, $"{ptLinesWithCard} of {ptLines} voiced lines of the portal scene showed a portrait");
                    PtCheck("beat speakers", ptMoods.ContainsKey("overlord") && ptMoods.ContainsKey("hero"), $"portraits shown for: {string.Join(", ", ptMoods.Keys)}");
                    yield return WaitReal(1.5f);

                    // 2. Every character, every mood.
                    foreach (var who in new[] { ("player", "hero"), ("overlord", "overlord"), ("yooba", "yooba"), ("rook", "rook"), ("wick", "wick") })
                    {
                        foreach (string mood in PortraitMood.All)
                        {
                            string id = PtLineFor(who.Item1, mood);
                            if (id == null)
                            {
                                Run.Event("portrait", $"{who.Item2} {mood}: no voiced line with a clip (skipped)");
                                continue;
                            }
                            yield return PtSay(id);
                            if (Run.Done)
                                yield break;
                        }
                        int seen = ptMoods.TryGetValue(who.Item2, out var set) ? set.Count : 0;
                        PtCheck($"{who.Item2} moods", seen >= 4, $"{seen} different expressions shown ({(set != null ? string.Join(", ", set) : "none")})");
                        Run.Metric($"portraits.moods.{who.Item2}", seen);
                    }

                    PtCheck("every line", ptLines > 0 && ptLinesWithCard == ptLines, $"{ptLinesWithCard} of {ptLines} voiced lines showed a portrait");
                    PtCheck("size", ptMinHeight >= 72f, $"smallest card {ptMinHeight:0} px tall at {Screen.width}x{Screen.height}");
                    PtCheck("slide", ptMaxSlide > 0f && ptMaxSlide <= 0.15f, $"slid in within {ptMaxSlide:0.000} s");
                    PtCheck("on screen", !ptOffScreen, "the card stays inside the safe area");
                    PtCheck("clear of the hero", !ptOnHero, "the card never covered the hero");
                    PtCheck("clear of the stick", !ptOnStick, TouchStickZone.Zone.HasValue ? "never over the touch stick" : "no touch stick up (desktop)");
                    PtCheck("mouth flaps", ptFlapLines >= Mathf.Max(1, ptLinesWithCard / 2), $"{ptFlapLines} of {ptLinesWithCard} lines opened and shut the mouth");
                    PtCheck("mouth closes", ptMaxCloseLag <= 0.2f, $"the mouth was shut {ptMaxCloseLag:0.000} s after a line ended (0.2 s allowed)");
                    Run.Metric("portraits.minHeightPx", ptMinHeight);
                    Run.Metric("portraits.slideSeconds", ptMaxSlide);
                    Run.Metric("portraits.mouthCloseSeconds", ptMaxCloseLag);
                    Run.Metric("portraits.passed", ptPassed);
                    Run.Metric("portraits.failed", ptFailed);
                    Run.Progress(1f);
                    End(ptFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
                }
                finally
                {
                    SpeakerPortraits.Reported -= PtReported;
                    GameplayTelemetry.Recorded -= BdTelemetry;
                    GameplayHud.TouchUiForced = touchBefore;
                    LevelRuntime.PendingScene = null;
                    input.ReleaseAll();
                }
            }
        }
    }
}

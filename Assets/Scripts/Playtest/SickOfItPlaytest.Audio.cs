using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.InputSystem;
using SickOfIt.Core;
using SickOfIt.Core.Audio;
using SickOfIt.Core.Input;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The audio scenario (Docs/SoundAudit.md "The audio scenario"): the sound of Level 1's first 60 s of game time, then Floor 1000's bed.
        /// Level 1 is loaded directly (a setup shortcut: the imps' shots graze from further away) and played by the bot through real input; at set
        /// moments a jump, a dash (Shift, or a right-half swipe on a touch device) and a rewind are pressed (a tap and a swipe where the touch UI is on),
        /// and a diamond is dropped at his feet (setup shortcut). Every sound that starts is logged (`sound` events: id, take n/m, pitch, volume, bus)
        /// from <see cref="Sfx.Played"/>. Checks: a sound for every verb (shot, hit, kill, graze, pickup, jump, land, dash, rewind); no take twice in a row;
        /// effects within 6% of pitch and voices at 1; at most 4 copies of one effect at once; Music at least 6 dB under each voice line
        /// and back afterwards (the duck's own samples and the music source's volume); Level 1 and Floor 1000 each start an ambient bed; the page's
        /// first tap started sound (and the master limiter is in the page), and the menu loaded within 20 s (WebGL).
        /// </summary>
        public const string AudioId = "audio";
        const int AudioLevel = 1;
        const int AudioFloorLevel = 2;
        const float AudioPlaySeconds = 60f;
        const float AudioSeconds = 420f;

        sealed partial class Session
        {
            int audioPassed, audioFailed;
            readonly List<Sfx.PlayedSound> audioLog = new List<Sfx.PlayedSound>();
            readonly List<(float t, bool on)> voiceEdges = new List<(float, bool)>();
            readonly List<(float t, float duck, float source)> duckSamples = new List<(float, float, float)>();
            AudioSource audioMusic;
            float audioMusicBefore;
            readonly List<float> voiceBefores = new List<float>();
            bool audioJumped1, audioJumped2, audioDashed, audioDiamond, audioRewound;

            void AudioCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    audioPassed++;
                    Run.Event("check", $"audio {beat}: ok ({detail})");
                }
                else
                {
                    audioFailed++;
                    Run.Event("check", $"audio {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"audio {beat}: {detail}");
                }
            }

            void AudioOnPlayed(Sfx.PlayedSound p)
            {
                audioLog.Add(p);
                Run.Event("sound", $"{p.id} take {p.take + 1}/{p.takes} pitch {p.pitch.ToString("0.000", CultureInfo.InvariantCulture)} volume {p.volume.ToString("0.00", CultureInfo.InvariantCulture)} bus {p.bus}");
            }

            void AudioOnVoice(bool on)
            {
                voiceEdges.Add((Time.unscaledTime, on));
                if (on)
                {
                    audioMusic = FindMusicSource();
                    audioMusicBefore = audioMusic != null ? audioMusic.volume : -1f;
                    voiceBefores.Add(audioMusicBefore);
                }
                Run.Event("duck", on ? $"a voice line starts, music source volume {audioMusicBefore.ToString("0.000", CultureInfo.InvariantCulture)}" : "the voice line ends");
            }

            // Fired by AudioBuses on every step of the duck (only while it moves): the duck's gain and the music source's volume at that moment.
            void AudioOnBuses() => duckSamples.Add((Time.unscaledTime, AudioBuses.MusicDuck, audioMusic != null ? audioMusic.volume : -1f));

            static AudioSource FindMusicSource()
            {
                foreach (var binder in Object.FindObjectsByType<MusicVolume>(FindObjectsSortMode.None))
                {
                    if (binder != null && binder.Source != null && binder.Source.isPlaying && MusicVolume.AuthoredOf(binder.Source) > 0.05f)
                        return binder.Source;
                }
                return null;
            }

            public IEnumerator AudioRun()
            {
                yield return ToMainMenu();
                if (Run.Done) yield break;
                float menuSeconds = Time.realtimeSinceStartup - 1.5f;
                Run.Metric("audio.menuSeconds", menuSeconds);
                if (Application.platform == RuntimePlatform.WebGLPlayer)
                    AudioCheck("the main menu loads within 20 s", menuSeconds <= 20f, $"{menuSeconds:0.0} s after the game started");

                // The page's first tap (done by the runner's start-up) must have started sound; the master limiter is in the page.
                string page = PageSound.State();
                if (page == null)
                    Run.Event("tap-prompt", "no page prompt here (editor or standalone): the page checks are skipped");
                else
                {
                    bool gone = page.Contains("\"prompt\":\"gone\"") && !page.Contains("\"taps\":0");
                    AudioCheck("the first tap starts sound", gone && !page.Contains("\"audio\":\"none\""), page);
                    if (!page.Contains("\"audio\":\"none\""))
                        AudioCheck("the master limiter is on the page's audio", !page.Contains("\"limiter\":0") && page.Contains("\"limiter\":"), page);
                }

                Sfx.Played += AudioOnPlayed;
                AudioBuses.VoiceChanged += AudioOnVoice;
                AudioBuses.Changed += AudioOnBuses;
                try
                {
                    RogueliteManager.Instance.ResetAllRogueliteData();
                    Run.Event("shortcut", $"load level {AudioLevel} directly");
                    var old = GameplayWorld.Instance;
                    var load = LoadLevel(AudioLevel);
                    if (load != null)
                        yield return load;
                    yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(AudioLevel), 60f, "level 1 to start");
                    if (Run.Done) yield break;
                    var world = GameplayWorld.Instance;
                    if (world.Style != null)
                    {
                        // Setup shortcut: the imps' shots graze from further away, so the bot meets a graze inside the minute.
                        world.Style.Config.grazeDistance = 2.5f;
                        Run.Event("shortcut", "grazeDistance 2.5 (the bot's dodging still decides what grazes)");
                    }

                    yield return Run.Until(() => audioLog.Exists(p => p.bus == Bus.Ambience), 10f, "Level 1's ambient bed");
                    if (Run.Done) yield break;
                    AudioCheck("Level 1 logs an ambient bed when loaded", audioLog.Exists(p => p.bus == Bus.Ambience && p.id.StartsWith("ambience_meadow")),
                        string.Join(", ", audioLog.FindAll(p => p.bus == Bus.Ambience).ConvertAll(p => p.id)));

                    Run.Event("audio", $"playing {AudioPlaySeconds:0} s of Level 1; touch UI {(GameplayHud.TouchUiOn ? "on" : "off")}");
                    yield return PlayLevel(AudioLevel, 0f, during: AudioDuring,
                        until: () => GameplayWorld.Instance != null && GameClock.Instance != null && GameClock.Instance.Time >= AudioPlaySeconds);
                    if (Run.Done) yield break;
                    int level1Sounds = audioLog.Count;
                    Run.Metric("audio.level1Sounds", level1Sounds);

                    AudioVerbChecks();
                    AudioTakeChecks();
                    AudioDuckChecks();
                    AudioAmbienceChecks("Level 1");

                    // Floor 1000: its own bed.
                    int before = audioLog.Count;
                    Run.Event("shortcut", $"load level {AudioFloorLevel} (Floor 1000) directly");
                    old = GameplayWorld.Instance;
                    load = LoadLevel(AudioFloorLevel);
                    if (load != null)
                        yield return load;
                    yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(AudioFloorLevel), 60f, "Floor 1000 to start");
                    if (Run.Done) yield break;
                    yield return Run.Until(() => audioLog.FindIndex(before, p => p.bus == Bus.Ambience) >= 0, 10f, "Floor 1000's ambient bed");
                    if (Run.Done) yield break;
                    yield return WaitReal(3f);
                    int bed = audioLog.FindIndex(before, p => p.bus == Bus.Ambience);
                    AudioCheck("Floor 1000 logs an ambient bed when loaded", bed >= 0 && audioLog[bed].id == "ambience_floor1000", bed >= 0 ? audioLog[bed].id : "none");
                    AudioCheck("Floor 1000's bed is audible", AmbienceBed.Playing && AmbienceBed.Volume > 0.01f, $"{AmbienceBed.Current}, volume {AmbienceBed.Volume:0.000}");
                }
                finally
                {
                    Sfx.Played -= AudioOnPlayed;
                    AudioBuses.VoiceChanged -= AudioOnVoice;
                    AudioBuses.Changed -= AudioOnBuses;
                    input.ReleaseAll();
                }
                if (Run.Done)
                    yield break;
                Run.Metric("audio.sounds", audioLog.Count);
                Run.Metric("audio.passed", audioPassed);
                Run.Metric("audio.failed", audioFailed);
                Run.Progress(1f);
                End(audioFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }

            // The moments the bot doesn't make by itself: a jump twice, a dash, a diamond at his feet, a rewind.
            IEnumerator AudioDuring(float now)
            {
                var world = GameplayWorld.Instance;
                var hero = world != null ? world.PlayerController : null;
                if (hero == null)
                    return null;
                if (!audioJumped1 && now >= 3f && hero.IsGrounded)
                {
                    audioJumped1 = true;
                    return AudioJump();
                }
                if (!audioJumped2 && now >= 9f && hero.IsGrounded)
                {
                    audioJumped2 = true;
                    return AudioJump();
                }
                if (!audioDashed && now >= 13f && hero.DashUnlocked)
                {
                    audioDashed = true;
                    return AudioDash(hero);
                }
                if (!audioDiamond && now >= 16f)
                {
                    audioDiamond = true;
                    var at = (Vector2)hero.transform.position + new Vector2(0.9f, 0.2f);
                    world.Spawn("diamond", at, new SpawnOptions());
                    Run.Event("shortcut", "a diamond dropped at his feet (the pickup's own collect does the rest)");
                }
                if (!audioRewound && now >= 38f && HasJuice())
                {
                    audioRewound = true;
                    return HoldRewind(1.2f);
                }
                return null;
            }

            IEnumerator AudioJump()
            {
                if (GameplayHud.TouchUiOn)
                {
                    var at = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                    int id = input.TouchDown(at);
                    yield return WaitReal(0.08f);
                    input.TouchUp(id, at);
                }
                else
                {
                    input.SetKey(Key.Space, true);
                    yield return WaitReal(0.1f);
                    input.SetKey(Key.Space, false);
                }
            }

            IEnumerator AudioDash(PlayerController hero)
            {
                if (GameplayHud.TouchUiOn)
                {
                    float shortSide = Mathf.Min(Screen.width, Screen.height);
                    var start = new Vector2(Screen.width * 0.75f, Screen.height * 0.45f);
                    int id = input.TouchDown(start);
                    yield return null;
                    input.TouchMove(id, start + new Vector2(shortSide * 0.04f, 0f));
                    yield return null;
                    input.TouchMove(id, start + new Vector2(shortSide * 0.16f, 0f));
                    yield return null;
                    yield return null;
                    input.TouchUp(id, start + new Vector2(shortSide * 0.16f, 0f));
                }
                else
                {
                    input.SetKey(Key.LeftShift, true);
                    yield return null;
                    yield return null;
                    input.SetKey(Key.LeftShift, false);
                }
            }

            int AudioCount(string id) => audioLog.FindAll(p => p.id == id).Count;

            void AudioVerbChecks()
            {
                var e = Sfx.Events;
                var verbs = new (string verb, string id)[]
                {
                    ("shot", e.playerShoot), ("hit", e.enemyHit), ("kill", e.killPop), ("graze", "graze_tick"), ("pickup", e.pickupDiamond),
                    ("jump", e.playerJump), ("land", e.playerLand), ("dash", e.playerDash), ("rewind", e.rewindStart),
                };
                var missing = new List<string>();
                var counts = new List<string>();
                foreach (var (verb, id) in verbs)
                {
                    int n = AudioCount(id);
                    counts.Add($"{verb} {id} x{n}");
                    Run.Metric("audio.count." + verb, n);
                    if (n == 0)
                        missing.Add(verb);
                }
                AudioCheck("a sound for every shot, hit, kill, graze, pickup, jump, land, dash and rewind", missing.Count == 0,
                    missing.Count == 0 ? string.Join(", ", counts) : "no sound for: " + string.Join(", ", missing) + " (" + string.Join(", ", counts) + ")");
                int stops = AudioCount(e.rewindStop);
                AudioCheck("the rewind's stop is heard too", stops > 0 || AudioCount(e.rewindStart) == 0, $"{stops} x {e.rewindStop}");
            }

            // The verbs played at their own pitch (the graze rises with its streak and the rewind's loop ramps by design).
            static bool AudioPitchChecked(string id)
            {
                var e = Sfx.Events;
                return id == e.playerShoot || id == e.enemyHit || id == e.killPop || id == e.playerHurt || id == e.pickupDiamond
                    || id == e.playerJump || id == e.playerLand || id == e.playerDash || id == e.rewindStop;
            }

            void AudioTakeChecks()
            {
                // No take twice in a row, per sound with several takes.
                var last = new Dictionary<string, int>();
                int repeats = 0, multi = 0;
                string firstRepeat = "";
                var takesSeen = new Dictionary<string, HashSet<int>>();
                foreach (var p in audioLog)
                {
                    if (p.takes < 2 || p.bus == Bus.Voice || p.bus == Bus.Ambience)
                        continue;
                    multi++;
                    if (!takesSeen.TryGetValue(p.id, out var seen))
                        takesSeen[p.id] = seen = new HashSet<int>();
                    seen.Add(p.take);
                    if (last.TryGetValue(p.id, out int was) && was == p.take)
                    {
                        repeats++;
                        if (firstRepeat.Length == 0)
                            firstRepeat = $"{p.id} take {p.take + 1} twice in a row at {p.time:0.00}";
                    }
                    last[p.id] = p.take;
                }
                AudioCheck("no sound with several takes plays the same take twice in a row", repeats == 0 && multi > 0,
                    repeats == 0 ? $"{multi} plays of {takesSeen.Count} multi-take sounds" : $"{repeats} repeats, first: {firstRepeat}");
                int varied = 0;
                foreach (var kv in takesSeen)
                {
                    if (kv.Value.Count >= 2)
                        varied++;
                }
                AudioCheck("the takes really vary", varied >= 4, $"{varied} sounds used 2+ takes");

                // Pitch: effects within 6%, voices never moved. (The graze rises with its streak by design.)
                int pitchOff = 0, voicePitchOff = 0;
                foreach (var p in audioLog)
                {
                    if (p.bus == Bus.Voice && !Mathf.Approximately(p.pitch, 1f))
                        voicePitchOff++;
                    else if (p.bus == Bus.Sfx && AudioPitchChecked(p.id) && Mathf.Abs(p.pitch - 1f) > 0.065f)
                        pitchOff++;
                }
                AudioCheck("effects stay within 6% of pitch and voices are never moved", pitchOff == 0 && voicePitchOff == 0, $"{pitchOff} effects off, {voicePitchOff} voice lines off");

                // At most 4 copies of one effect at once: the starts inside one clip's length.
                int worst = 0;
                string worstId = "";
                var byId = new Dictionary<string, List<float>>();
                foreach (var p in audioLog)
                {
                    if (p.bus != Bus.Sfx)
                        continue;
                    if (!byId.TryGetValue(p.id, out var times))
                        byId[p.id] = times = new List<float>();
                    times.Add(p.time);
                }
                foreach (var kv in byId)
                {
                    float length = Sfx.LengthOf(kv.Key);
                    var times = kv.Value;
                    for (int i = 0, j = 0; i < times.Count; i++)
                    {
                        while (times[i] - times[j] >= length)
                            j++;
                        if (i - j + 1 > worst)
                        {
                            worst = i - j + 1;
                            worstId = kv.Key;
                        }
                    }
                }
                AudioCheck("at most 4 copies of one effect at once", worst <= Sfx.MaxCopies, $"most at once: {worst} ({worstId})");
            }

            void AudioDuckChecks()
            {
                var intervals = new List<(float s, float e)>();
                float open = -1f;
                foreach (var (t, on) in voiceEdges)
                {
                    if (on && open < 0f)
                        open = t;
                    else if (!on && open >= 0f)
                    {
                        intervals.Add((open, t));
                        open = -1f;
                    }
                }
                int judged = 0, deepOk = 0, backOk = 0, sourceJudged = 0;
                var notes = new List<string>();
                for (int k = 0; k < intervals.Count; k++)
                {
                    var (s, e) = intervals[k];
                    if (e - s < 0.6f)
                        continue; // too short to judge a duck that takes 0.1 s to arrive
                    judged++;
                    float minDuck = 1f, minSource = float.MaxValue;
                    float before = k < voiceBefores.Count ? voiceBefores[k] : -1f;
                    float nextStart = k + 1 < intervals.Count ? intervals[k + 1].s : float.MaxValue;
                    float lastDuck = -1f;
                    foreach (var smp in duckSamples)
                    {
                        if (smp.t >= s && smp.t <= e + 0.05f)
                        {
                            minDuck = Mathf.Min(minDuck, smp.duck);
                            if (smp.source >= 0f)
                                minSource = Mathf.Min(minSource, smp.source);
                        }
                        if (smp.t > e && smp.t < Mathf.Min(e + 1.2f, nextStart))
                            lastDuck = smp.duck;
                    }
                    // The plateau must be at least 6 dB down (0.501), the source too when music was audible.
                    float db = 20f * Mathf.Log10(Mathf.Max(0.0001f, minDuck));
                    bool deep = minDuck <= 0.5012f;
                    if (minSource < float.MaxValue && before > 0.01f)
                    {
                        sourceJudged++;
                        float sourceDb = 20f * Mathf.Log10(Mathf.Max(0.0001f, minSource / before));
                        if (sourceDb > -5.9f)
                            deep = false;
                        notes.Add($"line {k + 1}: duck {db:0.0} dB, source {sourceDb:0.0} dB");
                    }
                    else
                        notes.Add($"line {k + 1}: duck {db:0.0} dB");
                    if (deep)
                        deepOk++;
                    // Back to the prior level: the release ends at 1.0 (unless another line began inside 0.6 s).
                    bool another = nextStart < e + 0.7f;
                    if (another || lastDuck >= 0.99f)
                        backOk++;
                }
                Run.Metric("audio.voiceLines", intervals.Count);
                Run.Metric("audio.voiceLinesJudged", judged);
                AudioCheck("voice lines happened in the minute", judged >= 1, $"{intervals.Count} lines, {judged} long enough to judge");
                AudioCheck("music is at least 6 dB lower during each voice line", judged >= 1 && deepOk == judged, $"{deepOk}/{judged} ({string.Join("; ", notes)})");
                AudioCheck("music returns to its prior level after each voice line", judged >= 1 && backOk == judged, $"{backOk}/{judged}, release {AudioBuses.ReleaseSeconds:0.0} s");
                Run.Metric("audio.duckDb", AudioBuses.DuckDb);
            }

            void AudioAmbienceChecks(string where)
            {
                var beds = audioLog.FindAll(p => p.bus == Bus.Ambience);
                AudioCheck($"{where}'s bed plays on the Ambience bus", beds.Count >= 1, string.Join(", ", beds.ConvertAll(p => p.id)));
                AudioCheck("the Meadow's bed turns to its burning one", beds.Exists(p => p.id == "ambience_meadow_burning"), string.Join(", ", beds.ConvertAll(p => p.id)));
            }
        }
    }
}

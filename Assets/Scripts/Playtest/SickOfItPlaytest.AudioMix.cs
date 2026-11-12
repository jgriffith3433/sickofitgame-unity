using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.UI;
using SickOfIt.Combat.Health;
using SickOfIt.Core;
using SickOfIt.Core.Audio;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The audio-mix scenario (docs/audio-mix.md): voice ducks Music ≥6 dB, pause drops Music ≥8 dB, one heart plays
        /// the heartbeat and LowHealth snapshot, Settings Music/Effects/Voice sliders are ≥48 px and persist after a
        /// reopen. Run it at 390x844 too (thumb drag). Setup: Level 1 loaded directly.
        /// </summary>
        public const string AudioMixId = "audio-mix";
        const float AudioMixSeconds = 300f;

        sealed partial class Session
        {
            int mixPassed, mixFailed;
            readonly List<(float t, string name, bool on)> mixSnapshots = new List<(float, string, bool)>();
            readonly List<(float t, float duck, float musicDb)> mixDuckSamples = new List<(float, float, float)>();

            void MixCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    mixPassed++;
                    Run.Event("check", $"audio-mix {beat}: ok ({detail})");
                }
                else
                {
                    mixFailed++;
                    Run.Event("check", $"audio-mix {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"audio-mix {beat}: {detail}");
                }
            }

            void MixOnSnapshot(string name, bool on)
            {
                mixSnapshots.Add((Time.unscaledTime, name, on));
                Run.Event("snapshot", $"{name} {(on ? "on" : "off")} musicDb={AudioBuses.MusicGainDb.ToString("0.0", CultureInfo.InvariantCulture)} sfxDb={AudioBuses.SfxGainDb.ToString("0.0", CultureInfo.InvariantCulture)}");
            }

            void MixOnVoice(bool on) =>
                Run.Event("duck", on
                    ? $"voice starts musicDb={AudioBuses.MusicGainDb.ToString("0.0", CultureInfo.InvariantCulture)} ambDb={AudioBuses.AmbienceGainDb.ToString("0.0", CultureInfo.InvariantCulture)}"
                    : $"voice ends musicDb={AudioBuses.MusicGainDb.ToString("0.0", CultureInfo.InvariantCulture)}");

            void MixOnBuses() =>
                mixDuckSamples.Add((Time.unscaledTime, AudioBuses.MusicDuck, AudioBuses.MusicGainDb));

            public IEnumerator AudioMixRun()
            {
                mixPassed = mixFailed = 0;
                mixSnapshots.Clear();
                mixDuckSamples.Clear();
                AudioBuses.SnapshotChanged += MixOnSnapshot;
                AudioBuses.VoiceChanged += MixOnVoice;
                AudioBuses.Changed += MixOnBuses;
                Sfx.Played += MixOnPlayed;

                try
                {
                    yield return ToMainMenu();
                    if (Run.Done) yield break;

                    // --- Settings sliders persist + height (criteria 4 and 5) ---
                    yield return OpenMainSettings();
                    if (Run.Done) yield break;
                    var texts = GameDefinition.Instance.menuTheme.texts;
                    yield return CheckSliderHeights(texts);
                    float musicBefore = GameSettings.MusicVolume;
                    float sfxBefore = GameSettings.SfxVolume;
                    float voiceBefore = GameSettings.VoiceVolume;
                    float musicTarget = musicBefore > 0.55f ? 0.4f : 0.7f;
                    float sfxTarget = sfxBefore > 0.55f ? 0.35f : 0.8f;
                    float voiceTarget = voiceBefore > 0.55f ? 0.45f : 0.75f;
                    yield return DragSettingsSlider(texts.musicVolume, musicTarget, () => Mathf.Abs(GameSettings.MusicVolume - musicTarget) < 0.06f);
                    if (Run.Done) yield break;
                    yield return DragSettingsSlider(texts.effectsVolume, sfxTarget, () => Mathf.Abs(GameSettings.SfxVolume - sfxTarget) < 0.06f);
                    if (Run.Done) yield break;
                    yield return DragSettingsSlider(texts.voiceVolume, voiceTarget, () => Mathf.Abs(GameSettings.VoiceVolume - voiceTarget) < 0.06f);
                    if (Run.Done) yield break;
                    Run.Event("settings", $"set Music={GameSettings.MusicVolume:0.##} Effects={GameSettings.SfxVolume:0.##} Voice={GameSettings.VoiceVolume:0.##} json={GameSettings.SoundJson}");
                    yield return CloseSettings();
                    if (Run.Done) yield break;
                    // "Reload": reopen settings and read the saved values (PlayerPrefs already flushed).
                    yield return OpenMainSettings();
                    if (Run.Done) yield break;
                    MixCheck("Music/Effects/Voice persist after reopen",
                        Mathf.Abs(GameSettings.MusicVolume - musicTarget) < 0.06f
                        && Mathf.Abs(GameSettings.SfxVolume - sfxTarget) < 0.06f
                        && Mathf.Abs(GameSettings.VoiceVolume - voiceTarget) < 0.06f,
                        $"Music {GameSettings.MusicVolume:0.##} Effects {GameSettings.SfxVolume:0.##} Voice {GameSettings.VoiceVolume:0.##} (wanted {musicTarget:0.##}/{sfxTarget:0.##}/{voiceTarget:0.##})");
                    // Put them back so the rest of the run and other scenarios hear the game.
                    GameSettings.MusicVolume = musicBefore;
                    GameSettings.SfxVolume = sfxBefore;
                    GameSettings.VoiceVolume = voiceBefore;
                    yield return CloseSettings();
                    if (Run.Done) yield break;

                    // --- Level 1: voice duck, pause, heartbeat ---
                    var old = GameplayWorld.Instance;
                    var load = LoadLevel(1);
                    if (load != null)
                        while (!load.isDone) yield return null;
                    yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(1), 60f, "Level 1 to start");
                    if (Run.Done) yield break;

                    float musicFlat = AudioBuses.MusicGainDb;
                    Run.Event("mix", $"baseline musicDb={musicFlat.ToString("0.0", CultureInfo.InvariantCulture)}");

                    // Force a short voice line so the duck is measurable even if Level 1's early lines are late.
                    Sfx.Play("vo_final_wave");
                    float duckAt = Time.unscaledTime;
                    yield return Run.Until(() => AudioBuses.Ducking, 5f, "a voice line ducks Music");
                    if (Run.Done) yield break;
                    yield return WaitReal(AudioBuses.AttackSeconds + 0.05f);
                    float ducked = AudioBuses.MusicGainDb;
                    float drop = musicFlat - ducked;
                    MixCheck("voice ducks Music at least 6 dB", drop >= 6f - 0.15f,
                        $"before {musicFlat:0.0} dB, during {ducked:0.0} dB, drop {drop:0.0} dB (MusicDuck={AudioBuses.MusicDuck:0.000})");
                    MixCheck("Ambience ducks under voice", AudioBuses.AmbienceDuck <= AudioBuses.AmbienceDuckGain + 0.02f,
                        $"AmbienceDuck={AudioBuses.AmbienceDuck:0.000} target={AudioBuses.AmbienceDuckGain:0.000}");
                    yield return Run.Until(() => !AudioBuses.Ducking, 8f, "voice duck releases");
                    if (Run.Done) yield break;
                    yield return WaitReal(AudioBuses.ReleaseSeconds + 0.15f);
                    float recovered = AudioBuses.MusicGainDb;
                    MixCheck("Music returns within 1 s after the line", Mathf.Abs(recovered - musicFlat) < 1.5f && Time.unscaledTime - duckAt < 12f,
                        $"after {recovered:0.0} dB (was {musicFlat:0.0})");

                    // Pause: Music -10 dB within 0.5 s.
                    float beforePause = AudioBuses.MusicGainDb;
                    yield return ClickUntil("II", () => GameplayWorld.Instance != null && GameplayWorld.Instance.Hud != null && GameplayWorld.Instance.Hud.Paused, 10f);
                    if (Run.Done) yield break;
                    float pauseDeadline = Time.unscaledTime + 0.5f;
                    float pausedDb = beforePause;
                    while (Time.unscaledTime < pauseDeadline)
                    {
                        pausedDb = AudioBuses.MusicGainDb;
                        if (beforePause - pausedDb >= 8f - 0.15f)
                            break;
                        yield return null;
                    }
                    MixCheck("pause drops Music at least 8 dB within 0.5 s", beforePause - pausedDb >= 8f - 0.15f,
                        $"before {beforePause:0.0} dB, paused {pausedDb:0.0} dB, PauseActive={AudioBuses.PauseActive}");
                    MixCheck("pause mutes SFX", AudioBuses.Gain(Bus.Sfx) <= 0.001f, $"sfx gain {AudioBuses.Gain(Bus.Sfx):0.000}");
                    yield return ClickUntil(texts.resume, () => GameplayWorld.Instance == null || !GameplayWorld.Instance.Hud.Paused, 10f);
                    if (Run.Done) yield break;
                    yield return WaitReal(0.2f);

                    // One heart: heartbeat + LowHealth.
                    var health = Object.FindFirstObjectByType<PlayerHealth>();
                    MixCheck("Level 1 has a PlayerHealth", health != null, health != null ? $"{health.CurrentHealth}/{health.MaxHealth}" : "missing");
                    if (health != null)
                    {
                        health.SetHealth(1);
                        yield return WaitReal(0.25f);
                        MixCheck("LowHealth snapshot at 1 heart", AudioBuses.LowHealthActive, $"musicDb={AudioBuses.MusicGainDb:0.0}");
                        MixCheck("heartbeat loop plays at 1 heart", HeartbeatMix.Playing, $"Playing={HeartbeatMix.Playing}");
                        health.SetHealth(Mathf.Max(2, health.MaxHealth));
                        yield return WaitReal(0.25f);
                        MixCheck("heartbeat stops when healed", !HeartbeatMix.Playing && !AudioBuses.LowHealthActive,
                            $"Playing={HeartbeatMix.Playing} LowHealth={AudioBuses.LowHealthActive}");
                    }

                    Run.Metric("audioMix.passed", mixPassed);
                    Run.Metric("audioMix.failed", mixFailed);
                    Run.Metric("audioMix.snapshots", mixSnapshots.Count);
                    MixCheck("scenario checks passed", mixFailed == 0, $"{mixPassed} ok, {mixFailed} failed");
                    Run.Progress(1f);
                }
                finally
                {
                    AudioBuses.SnapshotChanged -= MixOnSnapshot;
                    AudioBuses.VoiceChanged -= MixOnVoice;
                    AudioBuses.Changed -= MixOnBuses;
                    Sfx.Played -= MixOnPlayed;
                }
            }

            void MixOnPlayed(Sfx.PlayedSound p)
            {
                if (p.id == "player_heartbeat" || p.bus == Bus.Ui)
                    Run.Event("sound", $"{p.id} bus {p.bus} vol {p.volume.ToString("0.00", CultureInfo.InvariantCulture)}");
            }

            IEnumerator CheckSliderHeights(MenuTexts texts)
            {
                foreach (string label in new[] { texts.musicVolume, texts.effectsVolume, texts.voiceVolume })
                {
                    var slider = FindSettingsSlider(label);
                    MixCheck($"{label} slider exists", slider != null, label);
                    if (slider == null) continue;
                    var rect = (RectTransform)slider.transform;
                    float h = rect.rect.height;
                    // On a scaled canvas, convert to screen pixels.
                    var corners = new Vector3[4];
                    rect.GetWorldCorners(corners);
                    float px = Mathf.Abs(corners[1].y - corners[0].y);
                    Run.Metric($"audioMix.sliderPx.{label}", px);
                    Run.Event("slider", $"{label} layoutH={h:0.#} screenPx={px:0.#}");
                    MixCheck($"{label} slider at least 48 px tall", px >= 47.5f || h >= 47.5f, $"{px:0.#} px (layout {h:0.#})");
                }
                yield break;
            }

            IEnumerator OpenMainSettings()
            {
                var texts = GameDefinition.Instance.menuTheme.texts;
                yield return ClickUntil(texts.settings, () => FindSettingsSlider(texts.musicVolume) != null
                    || Object.FindObjectsByType<Slider>(FindObjectsSortMode.None).Length > 0, 15f);
            }

            IEnumerator CloseSettings()
            {
                var texts = GameDefinition.Instance.menuTheme.texts;
                yield return ClickUntil(texts.done, () => FindSettingsSlider(texts.musicVolume) == null, 10f);
                yield return WaitReal(0.3f);
            }
        }
    }
}

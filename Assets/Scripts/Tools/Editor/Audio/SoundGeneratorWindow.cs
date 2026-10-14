#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEngine;
using SickOfIt.Data.Config;

namespace SickOfIt.Tools.Editor.Audio
{
    /// <summary>
    /// SickOfIt > Audio > Sound Generator: lists audio.json's sounds with their state, makes missing or outdated
    /// ones with ElevenLabs, regenerates a single sound, and previews clips. The API key is kept on this machine
    /// (EditorPrefs), or taken from the ELEVENLABS_API_KEY environment variable.
    /// </summary>
    public sealed class SoundGeneratorWindow : EditorWindow
    {
        Vector2 scroll;
        string keyInput = "";
        bool showKey;
        CancellationTokenSource running;
        string progressText;
        float progress;
        string lastResult;
        AudioConfig audio;
        string loadError;

        [MenuItem("SickOfIt/Audio/Sound Generator (ElevenLabs)", priority = 200)]
        static void Open() => GetWindow<SoundGeneratorWindow>("Sound Generator").Show();

        void OnEnable()
        {
            keyInput = ElevenLabsClient.KeyFromEnvironment ? "" : ElevenLabsClient.ApiKey;
            Reload();
        }

        void OnFocus() => Reload();

        void Reload()
        {
            if (running != null)
                return;
            audio = SoundGenerator.LoadAudio(out loadError);
            Repaint();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("ElevenLabs", EditorStyles.boldLabel);
            if (ElevenLabsClient.KeyFromEnvironment)
            {
                EditorGUILayout.HelpBox("Using the API key from the ELEVENLABS_API_KEY environment variable.", MessageType.Info);
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    keyInput = showKey ? EditorGUILayout.TextField("API key", keyInput) : EditorGUILayout.PasswordField("API key", keyInput);
                    showKey = GUILayout.Toggle(showKey, "Show", "Button", GUILayout.Width(50));
                    if (GUILayout.Button("Save", GUILayout.Width(60)))
                    {
                        ElevenLabsClient.SaveKey(keyInput);
                        ShowNotification(new GUIContent("Key saved on this computer"));
                    }
                }
                EditorGUILayout.HelpBox("The key is stored on this computer only (EditorPrefs), never in the project.", MessageType.None);
            }

            if (!string.IsNullOrEmpty(loadError))
            {
                EditorGUILayout.HelpBox(loadError, MessageType.Error);
                return;
            }
            if (audio == null)
                return;

            DrawVoices();

            var pending = SoundGenerator.Pending(audio);
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{audio.sounds.Count} sounds, {pending.Count} to make", EditorStyles.boldLabel);
                GUI.enabled = running == null && pending.Count > 0;
                if (GUILayout.Button($"Generate {pending.Count} missing / outdated", GUILayout.Width(240)))
                    Run(pending);
                GUI.enabled = running == null;
                if (GUILayout.Button("Edit audio.json", GUILayout.Width(110)))
                    AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<TextAsset>(SoundGenerator.AudioFile));
                GUI.enabled = true;
            }

            if (running != null)
            {
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 20), progress, progressText ?? "");
                if (GUILayout.Button("Cancel"))
                    running.Cancel();
            }
            if (!string.IsNullOrEmpty(lastResult))
                EditorGUILayout.HelpBox(lastResult, lastResult.Contains("failed") ? MessageType.Warning : MessageType.Info);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var sound in audio.sounds)
            {
                if (sound == null)
                    continue;
                var status = SoundGenerator.StatusOf(sound, audio);
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    var color = status == SoundGenerator.Status.Ready ? new Color(0.5f, 0.9f, 0.5f)
                        : status == SoundGenerator.Status.NoPrompt ? Color.gray : new Color(1f, 0.75f, 0.3f);
                    var old = GUI.color;
                    GUI.color = color;
                    GUILayout.Label(status.ToString(), GUILayout.Width(70));
                    GUI.color = old;
                    GUILayout.Label(sound.id, EditorStyles.boldLabel, GUILayout.Width(150));
                    GUILayout.Label((sound.kind == SoundKind.Voice ? "\"" + sound.prompt + "\"" : sound.prompt), EditorStyles.wordWrappedMiniLabel);
                    GUI.enabled = sound.clip != null;
                    if (GUILayout.Button("▶", GUILayout.Width(28)))
                        Preview(sound.clip);
                    GUI.enabled = running == null && !string.IsNullOrEmpty(sound.prompt);
                    if (GUILayout.Button(sound.clip != null ? "Redo" : "Make", GUILayout.Width(50)))
                        Run(new List<SoundDef> { sound });
                    if (GUILayout.Button(new GUIContent("3 takes", "Make 3 variations to choose from (uses 3x the credits)."), GUILayout.Width(62)))
                        RunTakes(sound, 3);
                    GUI.enabled = true;
                }

                var takes = SoundGenerator.TakesOf(audio, sound.id);
                if (takes.Count > 0)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(80);
                        GUILayout.Label("Takes:", GUILayout.Width(44));
                        for (int t = 0; t < takes.Count; t++)
                        {
                            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(takes[t]);
                            if (GUILayout.Button($"▶ {t + 1}", GUILayout.Width(44)) && clip != null)
                                Preview(clip);
                            GUI.enabled = running == null;
                            if (GUILayout.Button("Use", GUILayout.Width(40)))
                            {
                                SoundGenerator.UseTake(audio, sound, takes[t]);
                                lastResult = $"{sound.id}: using take {t + 1}.";
                                Reload();
                                GUIUtility.ExitGUI();
                            }
                            GUI.enabled = true;
                            GUILayout.Space(6);
                        }
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        async void Run(List<SoundDef> sounds)
        {
            if (!ElevenLabsClient.KeyFromEnvironment && keyInput != ElevenLabsClient.ApiKey && !string.IsNullOrEmpty(keyInput))
                ElevenLabsClient.SaveKey(keyInput);
            running = new CancellationTokenSource();
            lastResult = null;
            try
            {
                var result = await SoundGenerator.GenerateAsync(audio, sounds, (done, total, id) =>
                {
                    progress = total > 0 ? (float)done / total : 1f;
                    progressText = id != null ? $"Making {id} ({done + 1}/{total})..." : "Done";
                    Repaint();
                }, running.Token);
                lastResult = $"Made {result.Made} sound(s)." + (result.Failures.Count > 0
                    ? $" {result.Failures.Count} failed:\n" + string.Join("\n", result.Failures.Take(8))
                    : "");
                if (result.Failures.Count > 0)
                    Debug.LogWarning("[Sound Generator] " + lastResult);
            }
            catch (Exception e)
            {
                lastResult = "Generation failed: " + e.Message;
                Debug.LogException(e);
            }
            finally
            {
                running.Dispose();
                running = null;
                Reload();
            }
        }

        bool showVoices = true;

        void DrawVoices()
        {
            EditorGUILayout.Space();
            showVoices = EditorGUILayout.Foldout(showVoices, $"Voices ({audio.voices.Count})", true, EditorStyles.foldoutHeader);
            if (!showVoices)
                return;
            foreach (var voice in audio.voices)
            {
                if (voice == null)
                    continue;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(voice.name, EditorStyles.boldLabel, GUILayout.Width(110));
                        var old = GUI.color;
                        GUI.color = string.IsNullOrEmpty(voice.voiceId) ? new Color(1f, 0.75f, 0.3f) : new Color(0.5f, 0.9f, 0.5f);
                        GUILayout.Label(string.IsNullOrEmpty(voice.voiceId) ? "not designed yet" : "voice " + voice.voiceId, GUILayout.Width(220));
                        GUI.color = old;
                        GUILayout.FlexibleSpace();
                        GUI.enabled = running == null && !string.IsNullOrWhiteSpace(voice.description);
                        if (GUILayout.Button(new GUIContent("Design 3 candidates", "ElevenLabs Voice Design makes candidate voices from the description to audition."), GUILayout.Width(150)))
                            RunDesign(voice);
                        GUI.enabled = true;
                    }
                    GUILayout.Label(voice.description, EditorStyles.wordWrappedMiniLabel);

                    var designs = SoundGenerator.DesignsOf(audio, voice.name);
                    if (designs.Count == 0)
                        continue;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("Candidates:", GUILayout.Width(72));
                        for (int d = 0; d < designs.Count; d++)
                        {
                            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(designs[d].path);
                            if (GUILayout.Button($"▶ {d + 1}", GUILayout.Width(44)) && clip != null)
                                Preview(clip);
                            GUI.enabled = running == null;
                            if (GUILayout.Button(new GUIContent("Use", "Save this voice to your ElevenLabs account and speak this voice's lines with it."), GUILayout.Width(40)))
                                RunUseDesign(voice, designs[d].generatedVoiceId, d + 1);
                            GUI.enabled = true;
                            GUILayout.Space(6);
                        }
                    }
                }
            }
        }

        async void RunDesign(VoiceDef voice)
        {
            running = new CancellationTokenSource();
            lastResult = null;
            progress = 0.3f;
            progressText = $"Designing {voice.name}...";
            try
            {
                SaveTypedKey();
                int count = await SoundGenerator.DesignVoiceAsync(audio, voice, running.Token);
                lastResult = $"Made {count} candidate voice(s) for {voice.name}. Play them and press Use on the one that fits.";
            }
            catch (Exception e)
            {
                lastResult = "Voice design failed: " + e.Message;
                Debug.LogException(e);
            }
            finally
            {
                running.Dispose();
                running = null;
                Reload();
            }
        }

        async void RunUseDesign(VoiceDef voice, string generatedVoiceId, int number)
        {
            running = new CancellationTokenSource();
            lastResult = null;
            progress = 0.5f;
            progressText = $"Saving candidate {number} as {voice.name}...";
            try
            {
                SaveTypedKey();
                await SoundGenerator.UseDesignAsync(audio, voice, generatedVoiceId, running.Token);
                lastResult = $"{voice.name} now uses candidate {number}. Its lines show as outdated: Generate to record them.";
            }
            catch (Exception e)
            {
                lastResult = "Saving the voice failed: " + e.Message + " (design previews expire after a while; design again if needed)";
                Debug.LogException(e);
            }
            finally
            {
                running.Dispose();
                running = null;
                Reload();
            }
        }

        void SaveTypedKey()
        {
            if (!ElevenLabsClient.KeyFromEnvironment && keyInput != ElevenLabsClient.ApiKey && !string.IsNullOrEmpty(keyInput))
                ElevenLabsClient.SaveKey(keyInput);
        }

        async void RunTakes(SoundDef sound, int count)
        {
            if (!ElevenLabsClient.KeyFromEnvironment && keyInput != ElevenLabsClient.ApiKey && !string.IsNullOrEmpty(keyInput))
                ElevenLabsClient.SaveKey(keyInput);
            running = new CancellationTokenSource();
            lastResult = null;
            try
            {
                var made = await SoundGenerator.MakeTakesAsync(audio, sound, count, (done, total, id) =>
                {
                    progress = total > 0 ? (float)done / total : 1f;
                    progressText = id != null ? $"Take {done + 1}/{total} of {id}..." : "Done";
                    Repaint();
                }, running.Token);
                lastResult = $"Made {made.Count} take(s) of {sound.id}. Play them below and press Use on the best one.";
            }
            catch (Exception e)
            {
                lastResult = "Takes failed: " + e.Message;
                Debug.LogException(e);
            }
            finally
            {
                running.Dispose();
                running = null;
                Reload();
            }
        }

        static void Preview(AudioClip clip)
        {
            // AudioUtil is internal; reflection is the standard way to play a clip in the editor.
            var type = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            var stop = type?.GetMethod("StopAllPreviewClips", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            var play = type?.GetMethod("PlayPreviewClip", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public,
                null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
            stop?.Invoke(null, null);
            play?.Invoke(null, new object[] { clip, 0, false });
        }

        /// <summary>
        /// Batch mode: makes every missing or outdated sound, then quits (exit 0 = all made, 1 = some failed or
        /// config errors, 2 = crash). Needs ELEVENLABS_API_KEY (or a key saved on this machine).
        /// </summary>
        public static async void GenerateSounds()
        {
            int exit;
            try
            {
                var audio = SoundGenerator.LoadAudio(out string error);
                if (audio == null)
                {
                    Debug.LogError("[Sound Generator] " + (error ?? "No GameDefinition."));
                    exit = 1;
                }
                else
                {
                    var pending = SoundGenerator.Pending(audio);
                    Debug.Log($"[Sound Generator] {pending.Count} sound(s) to make.");
                    var result = await SoundGenerator.GenerateAsync(audio, pending,
                        (done, total, id) => { if (id != null) Debug.Log($"[Sound Generator] {done + 1}/{total} {id}"); },
                        CancellationToken.None);
                    foreach (var failure in result.Failures)
                        Debug.LogError("[Sound Generator] " + failure);
                    Debug.Log($"[Sound Generator] Made {result.Made}, failed {result.Failures.Count}.");
                    exit = result.Failures.Count == 0 ? 0 : 1;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                exit = 2;
            }
            if (Application.isBatchMode)
                EditorApplication.Exit(exit);
        }
    }
}
#endif

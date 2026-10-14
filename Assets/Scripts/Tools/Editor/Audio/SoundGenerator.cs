#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Json;
using SickOfIt.Tools.Editor.GameConfig;

namespace SickOfIt.Tools.Editor.Audio
{
    /// <summary>
    /// Makes the sounds in audio.json with ElevenLabs. For each sound that has a prompt but no clip, or whose
    /// prompt/settings changed since its clip was made, it asks ElevenLabs, saves the audio under audio.json's
    /// outputFolder, sets the import settings, and writes "clip" and "generatedFrom" back into audio.json.
    /// Used by the Sound Generator window and the GenerateSounds batch command.
    /// </summary>
    public static class SoundGenerator
    {
        public const string AudioFile = GameConfigLayout.RootFolder + "/audio.json";

        public enum Status { Ready, Missing, Outdated, NoPrompt }

        public static Status StatusOf(SoundDef sound, AudioConfig audio)
        {
            if (string.IsNullOrEmpty(sound.prompt))
                return sound.clip != null ? Status.Ready : Status.NoPrompt;
            if (sound.clip == null)
                return Status.Missing;
            return sound.generatedFrom == sound.GenerationKey(audio.defaultVoiceId) ? Status.Ready : Status.Outdated;
        }

        /// <summary>The sounds that need making (missing or outdated), or all of 'ids' when given.</summary>
        public static List<SoundDef> Pending(AudioConfig audio, ICollection<string> ids = null)
        {
            return audio.sounds.Where(s => s != null && !string.IsNullOrEmpty(s.prompt) &&
                                           (ids != null ? ids.Contains(s.id) : StatusOf(s, audio) != Status.Ready)).ToList();
        }

        public sealed class Result
        {
            public int Made;
            public readonly List<string> Failures = new List<string>();
        }

        /// <summary>
        /// Makes 'sounds' one after another (ElevenLabs limits concurrent requests on most plans), saving each as
        /// soon as it arrives so a failure or cancel keeps what's done. Must be awaited on the main thread.
        /// </summary>
        public static async Task<Result> GenerateAsync(AudioConfig audio, IList<SoundDef> sounds, Action<int, int, string> progress,
            CancellationToken cancel)
        {
            var result = new Result();
            string folder = "Assets/" + (string.IsNullOrEmpty(audio.outputFolder) ? "Audio/Generated" : audio.outputFolder.Trim('/'));
            Directory.CreateDirectory(folder);

            for (int i = 0; i < sounds.Count; i++)
            {
                if (cancel.IsCancellationRequested)
                    break;
                var sound = sounds[i];
                progress?.Invoke(i, sounds.Count, sound.id);
                try
                {
                    byte[] bytes = await Request(audio, sound, cancel);
                    string extension = audio.outputFormat.StartsWith("mp3", StringComparison.Ordinal) ? ".mp3"
                        : audio.outputFormat.StartsWith("wav", StringComparison.Ordinal) ? ".wav" : ".ogg";
                    string assetPath = $"{folder}/{SafeName(sound.id)}{extension}";
                    File.WriteAllBytes(assetPath, bytes);
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                    ConfigureImport(assetPath, sound);
                    WriteBack(sound.id, assetPath.Substring("Assets/".Length), sound.GenerationKey(audio.defaultVoiceId));
                    result.Made++;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception e)
                {
                    result.Failures.Add($"{sound.id}: {e.Message}");
                    // A bad key or no credits will fail every request the same way; stop instead of repeating it.
                    if (e is ElevenLabsException el && (el.Status == 401 || el.Status == 402 || el.Status == 0))
                        break;
                }
            }
            progress?.Invoke(sounds.Count, sounds.Count, null);
            if (result.Made > 0)
                GameConfigSync.ImportNow("sound generator");
            return result;
        }

        static Task<byte[]> Request(AudioConfig audio, SoundDef sound, CancellationToken cancel)
        {
            if (sound.kind == SoundKind.Voice)
            {
                string voice = string.IsNullOrEmpty(sound.voiceId) ? audio.defaultVoiceId : sound.voiceId;
                if (string.IsNullOrEmpty(voice))
                    throw new InvalidOperationException("no voice: set its voiceId or audio.json's defaultVoiceId");
                return ElevenLabsClient.SpeechAsync(voice, sound.prompt, audio.voiceModel, audio.outputFormat, cancel);
            }
            return ElevenLabsClient.SoundEffectAsync(sound.prompt, sound.durationSeconds, sound.promptInfluence, sound.loop, audio.outputFormat, cancel);
        }

        /// <summary>Short effects decompress on load (no hitch on first play); longer voice lines stay compressed.</summary>
        static void ConfigureImport(string assetPath, SoundDef sound)
        {
            if (!(AssetImporter.GetAtPath(assetPath) is AudioImporter importer))
                return;
            var settings = importer.defaultSampleSettings;
            settings.loadType = sound.kind == SoundKind.Voice || sound.durationSeconds > 4f
                ? AudioClipLoadType.CompressedInMemory
                : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = sound.kind == SoundKind.Voice;
            importer.loadInBackground = false;
            importer.SaveAndReimport();
        }

        /// <summary>Sets "clip" and "generatedFrom" on the sound in audio.json, keeping the rest of the file as it is.</summary>
        static void WriteBack(string id, string clipId, string key)
        {
            var root = JObject.Parse(File.ReadAllText(AudioFile));
            if (root["sounds"] is JArray sounds)
            {
                foreach (var token in sounds)
                {
                    if (token is JObject sound && (string)sound["id"] == id)
                    {
                        sound["clip"] = clipId;
                        sound["generatedFrom"] = key;
                    }
                }
            }
            File.WriteAllText(AudioFile, JsonText.Format(root));
        }

        static string SafeName(string id)
        {
            var chars = id.Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray();
            return new string(chars);
        }

        /// <summary>The imported audio settings, after importing any JSON edits first.</summary>
        public static AudioConfig LoadAudio(out string error)
        {
            error = null;
            if (!GameConfigSync.ImportIfChanged("sound generator"))
            {
                error = "The game config has errors (see Logs/GameConfig.log); fix them first.";
                return null;
            }
            var definition = GameConfigSync.LoadDefinition();
            return definition != null ? definition.audio : null;
        }
    }
}
#endif

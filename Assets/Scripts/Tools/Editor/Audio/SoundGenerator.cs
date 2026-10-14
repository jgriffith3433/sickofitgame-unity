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
            return sound.generatedFrom == sound.GenerationKey(audio) ? Status.Ready : Status.Outdated;
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
                    WriteBack(sound.id, assetPath.Substring("Assets/".Length), sound.GenerationKey(audio));
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

        // ─── Takes: several variations of one sound to choose from ──────────

        static string Folder(AudioConfig audio) => "Assets/" + (string.IsNullOrEmpty(audio.outputFolder) ? "Audio/Generated" : audio.outputFolder.Trim('/'));
        static string TakesFolder(AudioConfig audio) => Folder(audio) + "/Takes";
        static string Extension(AudioConfig audio) => audio.outputFormat.StartsWith("mp3", StringComparison.Ordinal) ? ".mp3"
            : audio.outputFormat.StartsWith("wav", StringComparison.Ordinal) ? ".wav" : ".ogg";

        /// <summary>Existing takes of a sound (asset paths), oldest first.</summary>
        public static List<string> TakesOf(AudioConfig audio, string id)
        {
            string folder = TakesFolder(audio);
            if (!Directory.Exists(folder))
                return new List<string>();
            string prefix = SafeName(id) + "_take";
            return Directory.GetFiles(folder)
                .Select(p => p.Replace(Path.DirectorySeparatorChar, '/'))
                .Where(p => Path.GetFileName(p).StartsWith(prefix, StringComparison.Ordinal) && !p.EndsWith(".meta", StringComparison.Ordinal))
                .OrderBy(p => File.GetCreationTimeUtc(p))
                .ToList();
        }

        /// <summary>
        /// Makes 'count' variations of one sound into the Takes folder without touching the sound's clip; pick one
        /// with <see cref="UseTake"/>. ElevenLabs gives a different result each call, like the Playground's variations.
        /// </summary>
        public static async Task<List<string>> MakeTakesAsync(AudioConfig audio, SoundDef sound, int count, Action<int, int, string> progress,
            CancellationToken cancel)
        {
            var made = new List<string>();
            string folder = TakesFolder(audio);
            Directory.CreateDirectory(folder);
            int first = TakesOf(audio, sound.id).Count + 1;
            for (int i = 0; i < count && !cancel.IsCancellationRequested; i++)
            {
                progress?.Invoke(i, count, sound.id);
                byte[] bytes = await Request(audio, sound, cancel);
                string path = $"{folder}/{SafeName(sound.id)}_take{first + i}{Extension(audio)}";
                File.WriteAllBytes(path, bytes);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                ConfigureImport(path, sound);
                made.Add(path);
            }
            progress?.Invoke(count, count, null);
            return made;
        }

        /// <summary>Makes a take the sound's clip (copied over its main file) and records the prompt it came from.</summary>
        public static void UseTake(AudioConfig audio, SoundDef sound, string takePath)
        {
            string target = $"{Folder(audio)}/{SafeName(sound.id)}{Path.GetExtension(takePath)}";
            Directory.CreateDirectory(Folder(audio));
            File.Copy(takePath, target, true);
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceUpdate);
            ConfigureImport(target, sound);
            WriteBack(sound.id, target.Substring("Assets/".Length), sound.GenerationKey(audio));
            GameConfigSync.ImportNow("sound generator: picked a take");
        }

        // ─── Voice design: candidate voices for a VoiceDef, then save the one you like ───────────

        static string DesignsFolder(AudioConfig audio) => Folder(audio) + "/VoiceDesigns";
        static string DesignsIndex(AudioConfig audio, string voiceName) => $"{DesignsFolder(audio)}/{SafeName(voiceName)}_designs.json";

        /// <summary>Candidate voices made so far for a voice (preview file, ElevenLabs' id to save it), oldest first.</summary>
        public static List<(string path, string generatedVoiceId)> DesignsOf(AudioConfig audio, string voiceName)
        {
            var result = new List<(string, string)>();
            string index = DesignsIndex(audio, voiceName);
            if (!File.Exists(index))
                return result;
            foreach (var pair in JObject.Parse(File.ReadAllText(index)).Properties())
            {
                if (File.Exists(pair.Name))
                    result.Add((pair.Name, (string)pair.Value));
            }
            return result;
        }

        /// <summary>Asks ElevenLabs Voice Design for candidates (usually 3) and saves their previews to audition.</summary>
        public static async Task<int> DesignVoiceAsync(AudioConfig audio, VoiceDef voice, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(voice.description))
                throw new InvalidOperationException($"voice \"{voice.name}\" has no description to design from");
            var previews = await ElevenLabsClient.DesignVoiceAsync(voice.description, voice.sampleText, cancel);
            string folder = DesignsFolder(audio);
            Directory.CreateDirectory(folder);
            string indexPath = DesignsIndex(audio, voice.name);
            var index = File.Exists(indexPath) ? JObject.Parse(File.ReadAllText(indexPath)) : new JObject();
            int next = index.Count + 1;
            foreach (var preview in previews)
            {
                string path = $"{folder}/{SafeName(voice.name)}_design{next++}.mp3";
                File.WriteAllBytes(path, preview.Audio);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                index[path] = preview.GeneratedVoiceId;
            }
            File.WriteAllText(indexPath, index.ToString());
            return previews.Length;
        }

        /// <summary>
        /// Saves a candidate as a voice in the ElevenLabs account and makes it this voice's voiceId in audio.json.
        /// Every line spoken in this voice then shows as outdated, ready to remake.
        /// </summary>
        public static async Task UseDesignAsync(AudioConfig audio, VoiceDef voice, string generatedVoiceId, CancellationToken cancel)
        {
            string voiceId = await ElevenLabsClient.SaveDesignedVoiceAsync($"Sick Of It - {voice.name}", voice.description, generatedVoiceId, cancel);
            if (string.IsNullOrEmpty(voiceId))
                throw new InvalidOperationException("ElevenLabs didn't return a voice id");
            EditAudioJson(root =>
            {
                if (!(root["voices"] is JArray voices))
                    return;
                foreach (var token in voices)
                {
                    if (token is JObject entry && (string)entry["name"] == voice.name)
                        entry["voiceId"] = voiceId;
                }
            });
            GameConfigSync.ImportNow("sound generator: picked a voice");
        }

        static Task<byte[]> Request(AudioConfig audio, SoundDef sound, CancellationToken cancel)
        {
            if (sound.kind == SoundKind.Voice)
            {
                var speaker = audio.ResolveVoice(sound);
                if (string.IsNullOrEmpty(speaker.voiceId))
                    throw new InvalidOperationException(string.IsNullOrEmpty(sound.voice)
                        ? "no voice: set its voice (e.g. \"player\") or audio.json's defaultVoiceId"
                        : $"voice \"{sound.voice}\" has no voiceId yet: design it in the Voices section first");
                return ElevenLabsClient.SpeechAsync(speaker.voiceId, sound.prompt, speaker.model, speaker.stability, speaker.speed,
                    audio.outputFormat, cancel);
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
            EditAudioJson(root =>
            {
                if (!(root["sounds"] is JArray sounds))
                    return;
                foreach (var token in sounds)
                {
                    if (token is JObject sound && (string)sound["id"] == id)
                    {
                        sound["clip"] = clipId;
                        sound["generatedFrom"] = key;
                    }
                }
            });
        }

        static void EditAudioJson(Action<JObject> edit)
        {
            var root = JObject.Parse(File.ReadAllText(AudioFile));
            edit(root);
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

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Gameplay;
using SickOfIt.Json;
using Object = UnityEngine.Object;

namespace SickOfIt.Tools.Editor.GameConfig
{
    public sealed class GameConfigLoadResult
    {
        /// <summary>An in-memory GameDefinition built from the files. The caller destroys it.</summary>
        public GameDefinition Definition;
        public readonly JsonIssueList Issues = new JsonIssueList();
        /// <summary>True when any file was in an older format and was upgraded in memory.</summary>
        public bool Migrated;
    }

    /// <summary>Builds a GameDefinition from the config JSON file set, collecting every problem it finds.</summary>
    public static class GameConfigImporter
    {
        public static GameConfigLoadResult Load(IConfigFiles files, IAssetIdResolver resolver)
        {
            GameConfigLayout.Validate();
            var result = new GameConfigLoadResult();
            var issues = result.Issues;
            var recorder = new RecordingAssetResolver(resolver);
            var serializer = new UnityJsonSerializer(recorder);
            var rawFields = new List<(string file, string path, JToken token)>();
            serializer.RawJsonValidator = (token, field, _, file, path) => rawFields.Add((file, path, token));
            var definition = ScriptableObject.CreateInstance<GameDefinition>();
            result.Definition = definition;

            var root = ReadFile(files, GameConfigLayout.RootFile, result);
            if (root == null)
                return result;

            var rootSkip = GameConfigLayout.SplitFieldNames();
            rootSkip.Add(GameConfigLayout.VersionKey);
            serializer.PopulateFields(root, definition, rootSkip, issues, GameConfigLayout.RootFile);

            foreach (var split in GameConfigLayout.Files)
            {
                var obj = ReadFile(files, split.File, result);
                if (obj == null)
                    continue;
                var field = typeof(GameDefinition).GetField(split.Field);
                object target = field.GetValue(definition) ?? UnityJsonSerializer.CreateInstance(field.FieldType);
                serializer.PopulateFields(obj, target, VersionOnly(), issues, split.File);
                field.SetValue(definition, target);
            }

            var listed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var collection in GameConfigLayout.Collections)
                LoadCollection(files, root, collection, definition, serializer, result, listed);

            var contentSources = LoadContent(files, definition, result);
            ReportUnlistedFiles(files, listed, issues);
            foreach (string problem in definition.Validate())
            {
                // Scripted levels don't use the enemies list.
                if (problem.Contains("No enemies") && definition.levels.Exists(l => l.HasScript && problem.Contains($"({l.levelName})")))
                    continue;
                issues.Warning("", "", problem);
            }

            ValidateGameplay(definition, contentSources, rawFields, recorder, issues);
            definition.assetRegistry = recorder.BuildRegistry();
            return result;
        }

        /// <summary>Embeds every file under content/ into GameDefinition.content, in path order.</summary>
        static List<GameContent.FileSource> LoadContent(IConfigFiles files, GameDefinition definition, GameConfigLoadResult result)
        {
            var sources = new List<GameContent.FileSource>();
            definition.content = new List<ContentFile>();
            foreach (string file in files.ListAll())
            {
                if (!GameConfigLayout.IsContentFile(file))
                    continue;
                var obj = ReadFile(files, file, result);
                if (obj == null)
                    continue;
                string path = file.Substring(GameConfigLayout.ContentFolder.Length + 1);
                definition.content.Add(new ContentFile { path = path, json = JsonText.Compact(obj) });
                sources.Add(new GameContent.FileSource { Path = path, DisplayPath = file, Json = obj });
            }
            return sources;
        }

        /// <summary>
        /// Parses the content and level scripts the way the game will, from the original JSON (so messages keep
        /// line numbers), checks references between them, and records every asset they use.
        /// </summary>
        static void ValidateGameplay(GameDefinition definition, List<GameContent.FileSource> contentSources,
            List<(string file, string path, JToken token)> rawFields, RecordingAssetResolver recorder, JsonIssueList issues)
        {
            var scripts = new List<GameContent.ScriptSource>();
            var levels = GameConfigLayout.CollectionForField("levels");
            foreach (var raw in rawFields)
            {
                if (!raw.file.StartsWith(levels.Folder + "/", StringComparison.Ordinal) || raw.path != "script")
                    continue;
                var level = definition.levels.Find(l => levels.FileFor(GameConfigLayout.SafeFileName(l.levelName)) == raw.file)
                            ?? definition.levels.Find(l => raw.file.EndsWith("/" + l.levelName + ".json", StringComparison.Ordinal));
                if (level == null)
                    continue;
                scripts.Add(new GameContent.ScriptSource { LevelName = level.levelName, DisplayPath = raw.file, JsonPath = "script", Json = raw.token });
            }

            var content = GameContent.Build(contentSources, scripts, recorder, new EditorSpriteSheets());
            issues.AddRange(content.Issues);
            ContentValidator.Validate(content, issues, definition.audio.sounds.Where(s => s != null).Select(s => s.id).ToList());
            ValidateAudio(definition.audio, issues);
        }

        /// <summary>audio.json: unique ids, and every game event naming a sound that exists.</summary>
        static void ValidateAudio(SickOfIt.Data.Config.AudioConfig audio, JsonIssueList issues)
        {
            const string file = "audio.json";
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < audio.sounds.Count; i++)
            {
                var sound = audio.sounds[i];
                if (sound == null)
                    continue;
                if (string.IsNullOrEmpty(sound.id))
                    issues.Error(file, $"sounds[{i}].id", "Every sound needs an id.");
                else if (!ids.Add(sound.id))
                    issues.Error(file, $"sounds[{i}].id", $"Sound \"{sound.id}\" is defined twice.");
                if (sound.clip == null && string.IsNullOrEmpty(sound.prompt))
                    issues.Warning(file, $"sounds[{i}]", $"Sound \"{sound.id}\" has neither a clip nor a prompt to generate one from.");
                if (sound.kind == SickOfIt.Data.Config.SoundKind.Effect && sound.durationSeconds != 0f
                    && (sound.durationSeconds < 0.5f || sound.durationSeconds > 30f))
                    issues.Error(file, $"sounds[{i}].durationSeconds", "The ElevenLabs API accepts 0.5-30 seconds (0 = let it decide); for shorter sounds, use 0.5 and ask for a very short sound in the prompt.");
                if (sound.kind == SickOfIt.Data.Config.SoundKind.Voice)
                {
                    var names = audio.voices.Where(v => v != null).Select(v => v.name).ToList();
                    if (!string.IsNullOrEmpty(sound.voice) && audio.FindVoice(sound.voice) == null)
                        issues.Error(file, $"sounds[{i}].voice", $"No voice \"{sound.voice}\" in voices.{UnityJsonSerializer.SuggestName(sound.voice, names)}");
                    else if (string.IsNullOrEmpty(sound.voice) && string.IsNullOrEmpty(sound.voiceId) && string.IsNullOrEmpty(audio.defaultVoiceId))
                        issues.Warning(file, $"sounds[{i}].voice", $"Voice line \"{sound.id}\" has no voice: set its voice (e.g. \"player\").");
                }
            }
            var voiceNames = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < audio.voices.Count; i++)
            {
                var voice = audio.voices[i];
                if (voice == null)
                    continue;
                if (string.IsNullOrEmpty(voice.name) || !voiceNames.Add(voice.name))
                    issues.Error(file, $"voices[{i}].name", "Every voice needs a unique name.");
                if (string.IsNullOrEmpty(voice.voiceId))
                    issues.Warning(file, $"voices[{i}].voiceId", $"Voice \"{voice.name}\" has no voiceId yet, so its lines can't be made. Design it in SickOfIt > Audio > Sound Generator (Voices), or paste a voice id from your ElevenLabs library.");
                if (voice.model == "eleven_v3" && !(Mathf.Approximately(voice.stability, 0f) || Mathf.Approximately(voice.stability, 0.5f) || Mathf.Approximately(voice.stability, 1f)))
                    issues.Error(file, $"voices[{i}].stability", "eleven_v3 takes stability 0 (creative), 0.5 (natural) or 1 (robust).");
            }
            foreach (var field in typeof(SickOfIt.Data.Config.SoundEvents).GetFields())
            {
                var id = field.GetValue(audio.events) as string;
                if (!string.IsNullOrEmpty(id) && !ids.Contains(id))
                    issues.Warning(file, "events." + field.Name, $"No sound \"{id}\" in sounds, so this event is silent.{UnityJsonSerializer.SuggestName(id, ids)}");
            }
        }

        static void LoadCollection(IConfigFiles files, JObject root, GameConfigLayout.Collection collection, GameDefinition definition,
            UnityJsonSerializer serializer, GameConfigLoadResult result, HashSet<string> listed)
        {
            var issues = result.Issues;
            string rootFile = GameConfigLayout.RootFile;
            var field = typeof(GameDefinition).GetField(collection.Field);
            var elementType = GameConfigLayout.ElementType(collection);
            var items = (IList)Activator.CreateInstance(field.FieldType);
            field.SetValue(definition, items);

            var manifest = root[collection.Field];
            if (manifest == null)
            {
                issues.Error(rootFile, collection.Field, $"Missing the \"{collection.Field}\" list: the file names in {collection.Folder}/, in order. Use [] for none.");
                return;
            }
            if (!(manifest is JArray ids))
            {
                issues.Error(rootFile, collection.Field, $"Must be a list of file names in {collection.Folder}/, like [\"Level1\"].", JsonText.LineOf(manifest));
                return;
            }

            var keyOwners = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < ids.Count; i++)
            {
                string path = $"{collection.Field}[{i}]";
                if (ids[i].Type != JTokenType.String)
                {
                    issues.Error(rootFile, path, "Expected a file name string.", JsonText.LineOf(ids[i]));
                    continue;
                }

                string id = (string)ids[i];
                string file = collection.FileFor(id);
                if (!listed.Add(file))
                {
                    issues.Error(rootFile, path, $"\"{id}\" is listed more than once.", JsonText.LineOf(ids[i]));
                    continue;
                }
                if (!files.Exists(file))
                {
                    issues.Error(rootFile, path, $"Lists \"{id}\", but {GameConfigLayout.RootFolder}/{file} doesn't exist.", JsonText.LineOf(ids[i]));
                    continue;
                }

                var obj = ReadFile(files, file, result);
                if (obj == null)
                    continue;

                object item = UnityJsonSerializer.CreateInstance(elementType);
                serializer.PopulateFields(obj, item, VersionOnly(), issues, file);
                items.Add(item);

                string key = GameConfigLayout.KeyOf(item, collection);
                if (string.IsNullOrEmpty(key))
                    issues.Warning(file, collection.KeyField, $"\"{collection.KeyField}\" is empty.");
                else if (keyOwners.TryGetValue(key, out string owner))
                    issues.Error(file, collection.KeyField, $"\"{collection.KeyField}\": \"{key}\" is already used by {owner}.");
                else
                    keyOwners[key] = file;
            }
        }

        static void ReportUnlistedFiles(IConfigFiles files, HashSet<string> listed, JsonIssueList issues)
        {
            var known = new HashSet<string>(GameConfigLayout.Files.Select(f => f.File)) { GameConfigLayout.RootFile };
            foreach (string file in files.ListAll())
            {
                if (known.Contains(file) || listed.Contains(file) || GameConfigLayout.IsContentFile(file))
                    continue;

                var collection = GameConfigLayout.Collections.FirstOrDefault(c => file.StartsWith(c.Folder + "/", StringComparison.Ordinal)
                    && file.IndexOf('/', c.Folder.Length + 1) < 0);
                issues.Warning(file, "", collection != null
                    ? $"Not listed in game.json \"{collection.Field}\", so it is ignored. Add \"{System.IO.Path.GetFileNameWithoutExtension(file)}\" to that list to include it."
                    : "Not part of the config layout, so it is ignored.");
            }
        }

        static JObject ReadFile(IConfigFiles files, string file, GameConfigLoadResult result)
        {
            if (!files.Exists(file))
            {
                result.Issues.Error(file, "", $"Missing file {GameConfigLayout.RootFolder}/{file}.");
                return null;
            }

            var token = JsonText.Parse(files.Read(file), file, result.Issues);
            if (token == null)
                return null;
            if (!(token is JObject obj))
            {
                result.Issues.Error(file, "", "The file must contain a JSON object ({ ... }).", JsonText.LineOf(token));
                return null;
            }
            if (!GameConfigMigrations.Upgrade(obj, file, result.Issues, out bool changed))
                return null;
            result.Migrated |= changed;
            return obj;
        }

        static HashSet<string> VersionOnly() => new HashSet<string> { GameConfigLayout.VersionKey };

        /// <summary>
        /// Copies <paramref name="loaded"/> into the <paramref name="target"/> asset and saves it.
        /// Returns false, changing nothing, when the two already hold the same data.
        /// </summary>
        public static bool ApplyTo(GameDefinition target, GameDefinition loaded)
        {
            loaded.name = target.name;
            loaded.hideFlags = target.hideFlags;
            if (EditorJsonUtility.ToJson(loaded) == EditorJsonUtility.ToJson(target))
                return false;

            Undo.RecordObject(target, "Import Game Config");
            EditorUtility.CopySerialized(loaded, target);
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(target);
            return true;
        }
    }
}
#endif

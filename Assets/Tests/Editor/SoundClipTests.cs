using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace SickOfIt.Tests.Editor
{
    /// <summary>
    /// The sound validator: every sound id in audio.json has a clip file, and every sound id the game's config refers to
    /// (events, entities' sounds.*Sound, weapons' sounds, hintVoice, showText/playSound ids, ...) is defined. Same rules as
    /// Tools/Audio/sound_audit.py --check; reads the JSON, so it needs no import.
    /// </summary>
    public class SoundClipTests
    {
        static string Config => Path.Combine(Application.dataPath, "GameConfig");

        static JObject Audio() => JObject.Parse(File.ReadAllText(Path.Combine(Config, "audio.json")));

        [Test]
        public void EverySoundHasAClip()
        {
            var missing = new List<string>();
            // Prompts whose clips are not made yet (Tools/Audio/pending_clips.json): reported by sound_audit.py, not failed.
            var pending = new HashSet<string>();
            string pendingFile = Path.Combine(Application.dataPath, "..", "Tools", "Audio", "pending_clips.json");
            if (File.Exists(pendingFile))
                foreach (var id in JObject.Parse(File.ReadAllText(pendingFile))["ids"])
                    pending.Add((string)id);
            foreach (var sound in Audio()["sounds"])
            {
                if (pending.Contains((string)sound["id"]))
                    continue;
                string clip = (string)sound["clip"];
                if (string.IsNullOrEmpty(clip) || !File.Exists(Path.Combine(Application.dataPath, clip)))
                    missing.Add((string)sound["id"]);
            }
            Assert.IsEmpty(missing, "Sounds without a clip (generate them: SickOfIt > Audio > Sound Generator):\n" + string.Join("\n", missing));
        }

        [Test]
        public void EverySoundIdTheConfigReferencesIsDefined()
        {
            var audio = Audio();
            var ids = new HashSet<string>(audio["sounds"].Select(s => (string)s["id"]));
            var undefined = new List<string>();
            foreach (var property in ((JObject)audio["events"]).Properties())
            {
                string id = (string)property.Value;
                if (!string.IsNullOrEmpty(id) && !ids.Contains(id))
                    undefined.Add($"audio.json events.{property.Name}: {id}");
            }
            foreach (var file in Directory.GetFiles(Config, "*.json", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(file) == "audio.json")
                    continue;
                JToken root;
                try { root = JToken.Parse(File.ReadAllText(file)); }
                catch { continue; }
                Collect(root, ids, undefined, Path.GetRelativePath(Config, file));
            }
            Assert.IsEmpty(undefined, "Sound ids referenced but not in audio.json:\n" + string.Join("\n", undefined));
        }

        static bool IsSoundKey(string key) => key.EndsWith("Sound") || key.EndsWith("sound") || key == "sound" || key == "hintVoice" || key == "heroLine";

        static void Check(string id, HashSet<string> ids, List<string> undefined, string where)
        {
            if (!string.IsNullOrEmpty(id) && !id.Contains('/') && !id.Contains(' ') && id != "Sound" && !ids.Contains(id))
                undefined.Add($"{where}: {id}");
        }

        static void Collect(JToken token, HashSet<string> ids, List<string> undefined, string where)
        {
            if (token is JObject obj)
            {
                foreach (var property in obj.Properties())
                {
                    if (property.Value.Type == JTokenType.String && IsSoundKey(property.Name))
                        Check((string)property.Value, ids, undefined, where);
                    else if (property.Name == "sounds" && property.Value is JObject block)
                    {
                        foreach (var entry in block.Properties())
                            if (entry.Value.Type == JTokenType.String)
                                Check((string)entry.Value, ids, undefined, where);
                    }
                    Collect(property.Value, ids, undefined, where);
                }
            }
            else if (token is JArray array)
            {
                foreach (var item in array)
                    Collect(item, ids, undefined, where);
            }
        }
    }
}

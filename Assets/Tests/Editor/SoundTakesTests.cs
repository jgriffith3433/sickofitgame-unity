using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using SickOfIt.Core.Audio;
using SickOfIt.Data.Config;

namespace SickOfIt.Tests.Editor
{
    /// <summary>
    /// Takes, buses and the duck (Docs/SoundAudit.md "Takes, buses and beds"): a sound with several takes never plays the same take twice
    /// in a row, the duck is 8 dB, and audio.json's core verbs have three takes each on the right bus.
    /// </summary>
    public class SoundTakesTests
    {
        static AudioClip Clip(string name) => AudioClip.Create(name, 100, 1, 44100, false);

        [Test]
        public void ATakeIsNeverRepeatedInARow()
        {
            var sound = new SoundDef { id = "t_take_test", clip = Clip("a"), takes = new List<AudioClip> { Clip("b"), Clip("c") } };
            int last = -1;
            var seen = new HashSet<int>();
            for (int i = 0; i < 500; i++)
            {
                var clip = Sfx.PickTake(sound, out int take, out int count);
                Assert.AreEqual(3, count);
                Assert.IsNotNull(clip);
                Assert.AreNotEqual(last, take, $"take {take + 1} came twice in a row at pick {i}");
                last = take;
                seen.Add(take);
            }
            Assert.AreEqual(3, seen.Count, "every take comes up");
        }

        [Test]
        public void TwoTakesAlternate()
        {
            var sound = new SoundDef { id = "t_take_two", clip = Clip("a"), takes = new List<AudioClip> { Clip("b") } };
            Sfx.PickTake(sound, out int first, out _);
            for (int i = 0; i < 20; i++)
            {
                Sfx.PickTake(sound, out int take, out _);
                Assert.AreNotEqual(first, take);
                first = take;
            }
        }

        [Test]
        public void ATakeThatIsNotLoadedYetIsSkipped()
        {
            var sound = new SoundDef { id = "t_take_late", clip = Clip("a"), takes = new List<AudioClip> { null, null } };
            var clip = Sfx.PickTake(sound, out int take, out int count);
            Assert.AreEqual(1, count);
            Assert.AreEqual(0, take);
            Assert.IsNotNull(clip);
            Assert.IsNull(Sfx.PickTake(new SoundDef { id = "t_take_none" }, out take, out count));
            Assert.AreEqual(0, count);
        }

        [Test]
        public void TheDuckIsEightDecibelsAndTheBusesStartFlat()
        {
            AudioBuses.ResetForTests();
            Assert.AreEqual(Mathf.Pow(10f, -8f / 20f), AudioBuses.DuckGain, 1e-5f);
            Assert.AreEqual(Mathf.Pow(10f, -4f / 20f), AudioBuses.AmbienceDuckGain, 1e-5f);
            Assert.Less(AudioBuses.DuckGain, 0.5012f, "at least 6 dB");
            Assert.AreEqual(0.08f, AudioBuses.AttackSeconds, 1e-5f);
            Assert.AreEqual(0.4f, AudioBuses.ReleaseSeconds, 1e-5f);
            foreach (Bus bus in System.Enum.GetValues(typeof(Bus)))
                Assert.AreEqual(1f, AudioBuses.Gain(bus), 1e-5f, bus.ToString());
            AudioBuses.SetTrimDb(Bus.Sfx, -6f);
            Assert.AreEqual(0.501f, AudioBuses.Gain(Bus.Sfx), 0.001f);
            AudioBuses.ResetForTests();
        }

        [Test]
        public void PauseLowHealthAndRewindSnapshotsStackOnTheBuses()
        {
            AudioBuses.ResetForTests();
            AudioBuses.SetPause(true);
            Assert.AreEqual(AudioBuses.PauseMusicDb, AudioBuses.MusicGainDb, 0.05f);
            Assert.AreEqual(0f, AudioBuses.Gain(Bus.Sfx), 1e-5f);
            Assert.AreEqual(1f, AudioBuses.Gain(Bus.Ui), 1e-5f);
            AudioBuses.SetPause(false);
            AudioBuses.SetLowHealth(true);
            Assert.AreEqual(AudioBuses.LowHealthMusicDb, AudioBuses.MusicGainDb, 0.05f);
            AudioBuses.SetRewind(true);
            Assert.AreEqual(AudioBuses.RewindSfxDb, AudioBuses.SfxGainDb, 0.05f);
            AudioBuses.ResetForTests();
        }

        [Test]
        public void SoundsPlayOnTheirKindsBus()
        {
            Assert.AreEqual(Bus.Voice, AudioConfig.BusOf(new SoundDef { kind = SoundKind.Voice }));
            Assert.AreEqual(Bus.Sfx, AudioConfig.BusOf(new SoundDef { kind = SoundKind.Effect }));
            Assert.AreEqual(Bus.Ambience, AudioConfig.BusOf(new SoundDef { kind = SoundKind.Effect, bus = SoundBus.Ambience }));
            Assert.AreEqual(Bus.Ui, AudioConfig.BusOf(new SoundDef { kind = SoundKind.Effect, bus = SoundBus.Ui }));
        }

        static JObject Audio() => JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "GameConfig", "audio.json")));

        [Test]
        public void TheCoreVerbsHaveThreeTakesEachOnTheSfxBus()
        {
            var sounds = ((JArray)Audio()["sounds"]).ToDictionary(s => (string)s["id"]);
            foreach (string id in new[] { "player_shoot", "hit_pop", "kill_pop", "player_hurt", "graze_tick", "pickup_diamond", "player_jump", "player_land", "player_dash", "rewind_start", "rewind_stop" })
            {
                Assert.IsTrue(sounds.ContainsKey(id), id);
                var s = sounds[id];
                Assert.AreEqual(2, ((JArray)s["takes"]).Count, id + " takes besides its clip");
                Assert.AreEqual("Sfx", (string)s["bus"], id);
                Assert.LessOrEqual((float)s["pitchVariation"], 0.0601f, id + " pitch");
                Assert.LessOrEqual((int)s["maxVoices"], Sfx.MaxCopies, id + " copies");
                foreach (var take in (JArray)s["takes"])
                    Assert.IsTrue(File.Exists(Path.Combine(Application.dataPath, (string)take)), (string)take);
            }
        }

        [Test]
        public void VoiceLinesNeverVaryAndLevelBedsAreAmbienceSounds()
        {
            var audio = Audio();
            foreach (var s in (JArray)audio["sounds"])
            {
                if ((string)s["kind"] != "Voice")
                    continue;
                Assert.AreEqual(0f, s["pitchVariation"] != null ? (float)s["pitchVariation"] : 0f, (string)s["id"] + " is a voice: no pitch change");
                Assert.IsNull(s["takes"] != null && ((JArray)s["takes"]).Count > 0 ? s["takes"] : null, (string)s["id"] + " is a voice: one take");
            }
            var beds = ((JArray)audio["sounds"]).Where(s => (string)s["bus"] == "Ambience" && s["loop"] != null && (bool)s["loop"]).ToList();
            Assert.GreaterOrEqual(beds.Count, 7, "Meadow calm/burning, Shaft, Floors 1000-997");
            foreach (var bed in beds)
                Assert.IsTrue((bool)bed["loop"], (string)bed["id"] + " loops");
            foreach (string level in new[] { "Level1", "Floor1000", "Floor999", "Floor998", "Floor997" })
            {
                var json = JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "GameConfig", "levels", level + ".json")));
                string path = (string)json["ambientSound"];
                Assert.IsFalse(string.IsNullOrEmpty(path), level + " has an ambientSound");
                Assert.IsTrue(File.Exists(Path.Combine(Application.dataPath, path)), path);
                Assert.IsTrue(beds.Any(b => (string)b["clip"] == path), level + "'s bed is an Ambience sound of audio.json");
                Assert.IsTrue(json["ambientEmitters"] is JArray && ((JArray)json["ambientEmitters"]).Count >= 3, level + " has ≥3 emitters");
            }
        }
    }
}

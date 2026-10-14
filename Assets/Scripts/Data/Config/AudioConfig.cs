using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SickOfIt.Data.Config
{
    public enum SoundKind
    {
        [Tooltip("A sound effect made from a description (ElevenLabs text-to-sound-effects).")] Effect,
        [Tooltip("A spoken line (ElevenLabs text-to-speech).")] Voice,
    }

    /// <summary>
    /// One sound in audio.json. Describe it in 'prompt' and run SickOfIt > Audio > Sound Generator (or the
    /// GenerateSounds batch command): the generator calls ElevenLabs, saves the file under audio.json's outputFolder,
    /// and fills in 'clip'. Changing the prompt (or duration, influence, loop, voice) marks it out of date.
    /// A hand-made clip works too: set 'clip' and leave 'prompt' empty.
    /// </summary>
    [Serializable]
    public class SoundDef
    {
        [Tooltip("Name the game and other config refer to, e.g. \"player_shoot\".")]
        public string id = "";
        public SoundKind kind = SoundKind.Effect;
        [Tooltip("Effect: what it sounds like. Voice: the words to say.")]
        public string prompt = "";
        [Tooltip("Effect length in seconds (0.5-30). 0 = let ElevenLabs decide.")]
        public float durationSeconds;
        [Tooltip("Effect: 0-1, how literally to follow the prompt (ElevenLabs default 0.3).")]
        public float promptInfluence = 0.3f;
        [Tooltip("Effect: make it loop seamlessly.")]
        public bool loop;
        [Tooltip("Voice: ElevenLabs voice id. Empty = audio.json's defaultVoiceId.")]
        public string voiceId = "";

        [Tooltip("The audio file. Filled in by the generator.")]
        public AudioClip clip;
        [Tooltip("Written by the generator: the settings 'clip' was made from. If they differ from the current ones, it's out of date.")]
        public string generatedFrom = "";

        [Range(0f, 2f)] public float volume = 1f;
        [Tooltip("Random pitch change per play (0.06 = up to 6% higher or lower), so repeats don't sound robotic.")]
        public float pitchVariation = 0.06f;
        [Tooltip("At most this many copies playing at once.")]
        public int maxVoices = 3;
        [Tooltip("Ignore repeats closer together than this (seconds).")]
        public float cooldown = 0.04f;

        /// <summary>Everything that changes what ElevenLabs would make; compared with generatedFrom.</summary>
        public string GenerationKey(string defaultVoiceId)
        {
            var c = CultureInfo.InvariantCulture;
            return kind == SoundKind.Voice
                ? $"voice|{(string.IsNullOrEmpty(voiceId) ? defaultVoiceId : voiceId)}|{prompt}"
                : $"effect|{durationSeconds.ToString("0.##", c)}|{promptInfluence.ToString("0.##", c)}|{loop}|{prompt}";
        }
    }

    /// <summary>Which sound plays for each game event (sound ids from audio.json; empty = silent).</summary>
    [Serializable]
    public class SoundEvents
    {
        public string playerShoot = "player_shoot";
        public string playerJump = "player_jump";
        public string playerLand = "player_land";
        public string playerHurt = "player_hurt";
        public string playerDie = "player_die";
        public string enemyHurt = "enemy_hurt";
        public string enemyDie = "enemy_die";
        public string explosion = "explosion";
        public string pickupDiamond = "pickup_diamond";
        public string pickupRewind = "pickup_rewind";
        public string rewindStart = "rewind_start";
        public string rewindStop = "rewind_stop";
        public string rewindEmpty = "rewind_empty";
        public string bannerShow = "banner_whoosh";
        public string bossAppear = "boss_appear";
        public string upgradeOffer = "upgrade_offer";
        public string upgradePick = "upgrade_pick";
        public string levelClear = "level_clear";
        [UnityEngine.Tooltip("Spoken after levelClear.")] public string levelClearVoice = "vo_level_clear";
        [UnityEngine.Tooltip("Spoken when you die.")] public string deathVoice = "vo_you_died";
        public string gradeReveal = "grade_reveal";
        public string deathWarning = "death_warning";
        public string pause = "ui_pause";
        public string uiHover = "ui_hover";
        public string uiClick = "ui_click";
        public string uiBack = "ui_back";
    }

    /// <summary>All sounds and the ElevenLabs settings used to make them (audio.json).</summary>
    [Serializable]
    public class AudioConfig
    {
        [Tooltip("Where generated files go, under Assets/.")]
        public string outputFolder = "Audio/Generated";
        [Tooltip("ElevenLabs voice for Voice sounds that don't name one.")]
        public string defaultVoiceId = "";
        [Tooltip("ElevenLabs text-to-speech model.")]
        public string voiceModel = "eleven_multilingual_v2";
        [Tooltip("ElevenLabs output format (mp3_44100_128 works on every plan).")]
        public string outputFormat = "mp3_44100_128";
        public SoundEvents events = new SoundEvents();
        public List<SoundDef> sounds = new List<SoundDef>();

        public SoundDef Find(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            foreach (var s in sounds)
            {
                if (s != null && s.id == id)
                    return s;
            }
            return null;
        }
    }
}

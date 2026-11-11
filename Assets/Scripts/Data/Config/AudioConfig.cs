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

    /// <summary>The four mixer groups (Core/Audio/AudioBuses.cs): every sound plays on one.</summary>
    public enum Bus
    {
        Music,
        Voice,
        Sfx,
        Ambience,
    }

    /// <summary>The mixer group a sound is set to play on (Core/Audio/AudioBuses.cs). Auto = Voice for spoken lines, SFX for effects.</summary>
    public enum SoundBus
    {
        Auto,
        Sfx,
        Voice,
        Ambience,
        Music,
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
        [Tooltip("Effect length in seconds (0.5-30; the API rejects less). 0 = let ElevenLabs decide.")]
        public float durationSeconds;
        [Tooltip("Effect: 0-1, how literally to follow the prompt (ElevenLabs default 0.3).")]
        public float promptInfluence = 0.3f;
        [Tooltip("Effect: make it loop seamlessly.")]
        public bool loop;
        [Tooltip("Voice: which of audio.json's voices speaks it (\"player\", \"announcer\").")]
        public string voice = "";
        [Tooltip("Voice: a raw ElevenLabs voice id instead of a named voice. Empty = the named voice, else defaultVoiceId.")]
        public string voiceId = "";

        [Tooltip("The audio file. Filled in by the generator. With 'takes' it is the first take.")]
        public AudioClip clip;
        [Tooltip("More takes of the same sound (clip is the first). Each play picks one at random, never the one it played last. Effects only.")]
        public List<AudioClip> takes = new List<AudioClip>();
        [Tooltip("Which mixer group it plays on. Auto: Voice for spoken lines, SFX for effects. Beds that loop under a level are Ambience.")]
        public SoundBus bus = SoundBus.Auto;
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
        public string GenerationKey(AudioConfig audio)
        {
            var c = CultureInfo.InvariantCulture;
            if (kind != SoundKind.Voice)
                return $"effect|{durationSeconds.ToString("0.##", c)}|{promptInfluence.ToString("0.##", c)}|{loop}|{prompt}";
            var speaker = audio.ResolveVoice(this);
            return $"voice|{speaker.voiceId}|{speaker.model}|{speaker.stability.ToString("0.##", c)}|{speaker.speed.ToString("0.##", c)}|{prompt}";
        }
    }

    /// <summary>
    /// A named voice (the player, the announcer). Describe it and use the Sound Generator's "Design" to have
    /// ElevenLabs create candidates; picking one saves it to your ElevenLabs account and fills in voiceId. Or paste
    /// the id of any voice from your ElevenLabs library.
    /// </summary>
    [Serializable]
    public class VoiceDef
    {
        public string name = "";
        [Tooltip("What the voice sounds like (age, tone, accent, attitude, recording quality), for ElevenLabs Voice Design.")]
        public string description = "";
        [Tooltip("What the design previews say (100-1000 characters). Empty = ElevenLabs writes something.")]
        public string sampleText = "";
        [Tooltip("ElevenLabs voice id. Filled in when you pick a design.")]
        public string voiceId = "";
        [Tooltip("Speech model. eleven_v3 understands delivery tags in the line: [sighs], [sarcastic], [shouting], [whispers]...")]
        public string model = "eleven_v3";
        [Tooltip("eleven_v3 takes 0 (creative, most expressive), 0.5 (natural) or 1 (robust, steady).")]
        public float stability = 0.5f;
        [Tooltip("Speaking speed, 0.7-1.2.")]
        public float speed = 1f;
    }

    /// <summary>Which sound plays for each game event (sound ids from audio.json; empty = silent).</summary>
    [Serializable]
    public class SoundEvents
    {
        public string playerShoot = "player_shoot";
        public string playerJump = "player_jump";
        public string playerLand = "player_land";
        public string playerDash = "player_dash";
        public string playerDashReady = "player_dash_ready";
        public string playerWallCling = "player_wall_cling";
        public string playerWallJump = "player_wall_jump";
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
        [UnityEngine.Tooltip("Music stinger when a weapon is found for the first time.")] public string weaponSting = "sting_weapon";
        public string pause = "ui_pause";
        public string uiHover = "ui_hover";
        public string uiClick = "ui_click";
        public string uiBack = "ui_back";
        [UnityEngine.Tooltip("A hero shot lands on an enemy: a short layered pop on top of the enemy's own hurt sound.")] public string enemyHit = "hit_pop";
        [UnityEngine.Tooltip("An enemy dies: a layered pop on top of the enemy's own death sound.")] public string killPop = "kill_pop";
    }

    /// <summary>All sounds and the ElevenLabs settings used to make them (audio.json).</summary>
    [Serializable]
    public class AudioConfig
    {
        [Tooltip("Where generated files go, under Assets/.")]
        public string outputFolder = "Audio/Generated";
        [Tooltip("ElevenLabs voice for Voice sounds that name neither a voice nor a voiceId.")]
        public string defaultVoiceId = "";
        [Tooltip("The game's voices: who says the lines.")]
        public List<VoiceDef> voices = new List<VoiceDef>();
        [Tooltip("ElevenLabs text-to-speech model.")]
        public string voiceModel = "eleven_multilingual_v2";
        [Tooltip("ElevenLabs output format (mp3_44100_128 works on every plan).")]
        public string outputFormat = "mp3_44100_128";
        public SoundEvents events = new SoundEvents();
        public List<SoundDef> sounds = new List<SoundDef>();

        public VoiceDef FindVoice(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            foreach (var v in voices)
            {
                if (v != null && v.name == name)
                    return v;
            }
            return null;
        }

        /// <summary>The voice id and speech settings a Voice sound is made with.</summary>
        public VoiceDef ResolveVoice(SoundDef sound)
        {
            var named = FindVoice(sound.voice);
            if (named != null && string.IsNullOrEmpty(sound.voiceId))
                return named;
            return new VoiceDef
            {
                name = sound.voice,
                voiceId = !string.IsNullOrEmpty(sound.voiceId) ? sound.voiceId : named != null ? named.voiceId : defaultVoiceId,
                model = named != null ? named.model : voiceModel,
                stability = named != null ? named.stability : 0.5f,
                speed = named != null ? named.speed : 1f,
            };
        }

        /// <summary>The mixer group of a sound (its own, or by kind).</summary>
        public static Bus BusOf(SoundDef sound)
        {
            switch (sound.bus)
            {
                case SoundBus.Sfx: return Bus.Sfx;
                case SoundBus.Voice: return Bus.Voice;
                case SoundBus.Ambience: return Bus.Ambience;
                case SoundBus.Music: return Bus.Music;
                default: return sound.kind == SoundKind.Voice ? Bus.Voice : Bus.Sfx;
            }
        }

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

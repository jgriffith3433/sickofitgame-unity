using UnityEngine;

namespace SickOfIt.Data
{
    public enum DifficultyLevel { Assist, Normal, Hard, HellLoop }

    /// <summary>
    /// The difficulty picked at New Game (Docs/Story.md "How sick of it are you?"), saved in PlayerPrefs and kept until the next New Game.
    /// Normal is every number at 1: the bullet hell as designed. The others scale four things, constant for a whole play so a rewind
    /// replays the same future: how fast hostile shots fly, how often enemies shoot (density), how hard a hit lands on the hero and how
    /// much rewind juice he holds and earns.
    /// </summary>
    public static class Difficulty
    {
        const string Key = "Difficulty";

        public readonly struct Modifiers
        {
            public readonly string name, blurb;
            public readonly float bulletSpeed, density, damageTaken, juice;
            public Modifiers(string name, string blurb, float bulletSpeed, float density, float damageTaken, float juice)
            {
                this.name = name; this.blurb = blurb; this.bulletSpeed = bulletSpeed; this.density = density;
                this.damageTaken = damageTaken; this.juice = juice;
            }
        }

        static readonly Modifiers[] Table =
        {
            new Modifiers("Assist", "Slower, sparser shots. Hits barely count. Lots of rewind. Aim assist on.", 0.8f, 0.75f, 0.5f, 1.5f),
            new Modifiers("Normal", "Hell as designed.", 1f, 1f, 1f, 1f),
            new Modifiers("Hard", "Faster, thicker shots. Hits bite. Less rewind.", 1.15f, 1.25f, 1.5f, 0.75f),
            new Modifiers("Hell Loop", "Everything Hell has, again and again. Rewind is a rumor.", 1.3f, 1.5f, 2f, 0.5f),
        };

        public static event System.Action Changed;

        static int? cached;

        public static DifficultyLevel Current
        {
            get
            {
                cached ??= Mathf.Clamp(PlayerPrefs.GetInt(Key, (int)DifficultyLevel.Normal), 0, Table.Length - 1);
                return (DifficultyLevel)cached.Value;
            }
            set
            {
                cached = (int)value;
                PlayerPrefs.SetInt(Key, (int)value);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        public static Modifiers Of(DifficultyLevel level) => Table[(int)level];
        public static Modifiers Now => Table[(int)Current];

        public static float BulletSpeed => Now.bulletSpeed;
        public static float Density => Now.density;
        public static float DamageTaken => Now.damageTaken;
        public static float Juice => Now.juice;

        /// <summary>
        /// A hit on the hero scaled by the difficulty: a whole number of hearts, the fraction paid by a roll that belongs to the shot
        /// (its serial), so the same shot always rolls the same and a rewind replays it. 0 means the hit is shrugged off.
        /// </summary>
        public static int ScaleHit(int amount, int shotKey)
        {
            float scale = DamageTaken;
            if (Mathf.Approximately(scale, 1f))
                return amount;
            float scaled = amount * scale;
            int whole = Mathf.FloorToInt(scaled);
            float fraction = scaled - whole;
            if (fraction > 0.001f && Roll(shotKey) < fraction)
                whole++;
            return whole;
        }

        static float Roll(int key)
        {
            uint h = (uint)key * 2654435761u ^ 0x9E3779B9u;
            h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }
}

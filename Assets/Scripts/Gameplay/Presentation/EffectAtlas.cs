using UnityEngine;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// Gen-made bullet art (Assets/Resources/Effects): an atlas of three orbs (cyan, magenta, white-hot core, 128 px each, 1 world unit
    /// across) and the hero's amber rocket. Each getter returns null when the texture is missing, so callers keep their procedural
    /// fallback. Presentation only.
    /// </summary>
    public static class EffectAtlas
    {
        const int Tile = 128;
        static Sprite[] orbs;
        static Sprite rocket;
        static bool orbsTried, rocketTried;

        public static Sprite Cyan => Orb(0);
        public static Sprite Magenta => Orb(1);
        public static Sprite WhiteCore => Orb(2);

        static Sprite Orb(int i)
        {
            if (!orbsTried)
            {
                orbsTried = true;
                var tex = Resources.Load<Texture2D>("Effects/BulletOrbs");
                if (tex != null)
                {
                    orbs = new Sprite[3];
                    for (int k = 0; k < 3; k++)
                        orbs[k] = Sprite.Create(tex, new Rect(k * Tile, 0, Tile, Tile), new Vector2(0.5f, 0.5f), Tile);
                }
            }
            return orbs != null ? orbs[i] : null;
        }

        /// <summary>The hero's rocket, pointing right, about 2 units long.</summary>
        public static Sprite Rocket
        {
            get
            {
                if (!rocketTried)
                {
                    rocketTried = true;
                    var tex = Resources.Load<Texture2D>("Effects/HeroRocket");
                    if (tex != null)
                        rocket = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 104f);
                }
                return rocket;
            }
        }
    }
}

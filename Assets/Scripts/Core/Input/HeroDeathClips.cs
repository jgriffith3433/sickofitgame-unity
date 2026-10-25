using UnityEngine;

namespace SickOfIt.Core.Input
{
    /// <summary>
    /// Which of the hero's four deaths (player.json states DeathBurn, DeathSquash, DeathSpin, DeathShrug) plays next.
    /// They come round in turn, one per death, so a run of deaths shows all four; the level and the death scene
    /// that follows it read the same pick (<see cref="Current"/>). Presentation only.
    /// </summary>
    public static class HeroDeathClips
    {
        public static readonly string[] States = { "DeathBurn", "DeathSquash", "DeathSpin", "DeathShrug" };
        const string TurnKey = "HeroDeathTurn";

        /// <summary>The death the hero is dying now (or last died).</summary>
        public static string Current => States[Mathf.Abs(PlayerPrefs.GetInt(TurnKey, 0)) % States.Length];

        /// <summary>Moves on to the next death and returns it.</summary>
        public static string Next()
        {
            PlayerPrefs.SetInt(TurnKey, (PlayerPrefs.GetInt(TurnKey, 0) + 1) % States.Length);
            return Current;
        }
    }
}

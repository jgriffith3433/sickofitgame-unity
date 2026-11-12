using UnityEngine;
using SickOfIt.Core.Audio;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// The diamond chime climbs: each diamond picked up within 1 s of the last plays one semitone higher (up to an octave and a
    /// half), and the chain starts again after a second without one. Presentation only (real time, nothing saved).
    /// </summary>
    public sealed partial class GameplayHud
    {
        const float DiamondChainWindow = 1f;
        const int DiamondChainMax = 18;
        float lastDiamondChimeAt = -10f;
        int diamondChain;

        /// <summary>Semitones above the base chime for the n-th diamond of a chain (0-based).</summary>
        public static float ChainPitch(int step) => Mathf.Pow(2f, Mathf.Clamp(step, 0, DiamondChainMax) / 12f);

        void PlayDiamondChime()
        {
            float now = Time.unscaledTime;
            diamondChain = now - lastDiamondChimeAt <= DiamondChainWindow ? diamondChain + 1 : 0;
            lastDiamondChimeAt = now;
            float pitch = ChainPitch(diamondChain);
            Sfx.Play(Sfx.Events.pickupDiamond, 1f, pitch, true);
            DiamondChimes?.Invoke(diamondChain, pitch);
        }

        /// <summary>Raised on every diamond chime (chain step, pitch), for the playtest.</summary>
        public static event System.Action<int, float> DiamondChimes;
    }
}

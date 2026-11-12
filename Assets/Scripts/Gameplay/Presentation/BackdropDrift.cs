using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Core;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// Slow motion of a painted backdrop layer (BackgroundLayerConfig.driftSpeed) and the Meadow's raid sky swap (raidSprite / raidTint).
    /// Lives on the layer's parallax pivot and moves the layer's renderers (the tile copies too) by one shared offset that wraps by exactly
    /// the tile width, so the row never shows a seam or a gap. Both are pure functions of the game clock, so a rewind plays them back.
    /// Presentation only: nothing here is saved or touches the simulation.
    /// </summary>
    [DefaultExecutionOrder(101)]
    public sealed class BackdropDrift : MonoBehaviour
    {
        private Transform[] parts;
        private Vector3[] homes;
        private SpriteRenderer[] renderers;
        private float speed;
        private float period;
        private Sprite calmSprite, raidSprite;
        private Color calmColor, raidTint;
        private float raidAt = float.PositiveInfinity;
        private bool showingRaid;

        /// <summary>Call once the pivot's renderers exist. A period of 0 means no drift (the sprite swap still works).</summary>
        public void Setup(float driftSpeed, float tileWidth, Sprite raid, Color tint)
        {
            var found = GetComponentsInChildren<SpriteRenderer>(true);
            renderers = found;
            parts = new Transform[found.Length];
            homes = new Vector3[found.Length];
            for (int i = 0; i < found.Length; i++)
            {
                parts[i] = found[i].transform;
                homes[i] = parts[i].localPosition;
            }
            speed = driftSpeed;
            period = tileWidth;
            raidSprite = raid;
            raidTint = tint;
            if (found.Length > 0)
            {
                calmSprite = found[0].sprite;
                calmColor = found[0].color;
            }
        }

        /// <summary>The raid begins at this game-clock time; a rewind to before it shows the calm sky again.</summary>
        public void BeginRaid(float clockTime)
        {
            raidAt = clockTime;
        }

        /// <summary>Whether the raid sky is showing right now.</summary>
        public bool ShowingRaid => showingRaid;

        private void LateUpdate()
        {
            var clock = GameClock.Instance;
            if (clock == null || parts == null)
                return;
            float t = clock.Time;
            float dx = 0f;
            if (period > 0.01f && !Mathf.Approximately(speed, 0f))
                dx = Mathf.Repeat(t * speed + period * 0.5f, period) - period * 0.5f;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null)
                    continue;
                var h = homes[i];
                parts[i].localPosition = new Vector3(h.x + dx, h.y, h.z);
            }

            bool raid = t >= raidAt && (raidSprite != null || raidTint.a > 0f);
            if (raid == showingRaid)
                return;
            showingRaid = raid;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;
                if (raidSprite != null)
                    renderers[i].sprite = raid ? raidSprite : calmSprite;
                if (raidTint.a > 0f)
                    renderers[i].color = raid ? raidTint : calmColor;
            }
        }
    }
}

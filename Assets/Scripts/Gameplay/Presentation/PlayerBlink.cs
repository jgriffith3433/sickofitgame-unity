using UnityEngine;
using SickOfIt.Combat.Health;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// Flickers the player while hit invincibility lasts, so it's clear you can't be hurt right now.
    /// Only changes sprite alpha, after everything else has set its colors this frame.
    /// </summary>
    public sealed class PlayerBlink : MonoBehaviour
    {
        PlayerHealth health;
        SpriteRenderer[] renderers;
        bool blinking;

        public void Init(PlayerHealth playerHealth, SpriteRenderer[] sprites)
        {
            health = playerHealth;
            renderers = sprites;
        }

        void LateUpdate()
        {
            if (health == null || renderers == null)
                return;
            bool blink = health.IsAlive && health.IsInvincible;
            if (!blink && !blinking)
                return;
            blinking = blink;
            float alpha = blink && Mathf.Repeat(Time.time * 14f, 1f) < 0.5f ? 0.3f : 1f;
            foreach (var r in renderers)
            {
                if (r == null)
                    continue;
                var c = r.color;
                c.a = alpha;
                r.color = c;
            }
        }
    }
}

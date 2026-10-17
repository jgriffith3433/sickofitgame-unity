using UnityEngine;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.UI.Menus
{
    /// <summary>
    /// Animates a menu element's visual (not its layout slot): an entrance when it appears, an idle motion while
    /// it's shown, and an exit when the menu leaves. Runs on unscaled time so it works while the game is paused.
    /// Other components (MenuButton) add their own scale/rotation through <see cref="ExtraScale"/>/<see cref="ExtraRotation"/>.
    /// </summary>
    public sealed class MenuMotionDriver : MonoBehaviour
    {
        public MenuMotion Idle = new MenuMotion();
        public MenuEntrance Entrance;
        public float EntranceDelay;
        public float BaseRotation;
        public float ExtraScale = 1f;
        public float ExtraRotation;

        RectTransform rect;
        CanvasGroup group;
        Vector2 restPosition;
        float startedAt = -1f;
        float exitStartedAt = -1f;
        float exitDuration;
        float seed;

        /// <summary>Where the element rests (its layout position); <see cref="MenuResponsive"/> moves it when the screen turns.</summary>
        public Vector2 RestPosition
        {
            get => restPosition;
            set
            {
                restPosition = value;
                restSet = true;
            }
        }
        bool restSet;

        public bool EntranceDone => Entrance == null || Entrance.type == MenuEntranceType.None
            || (startedAt >= 0f && Time.unscaledTime - startedAt >= EntranceDelay + Entrance.duration);

        void Awake()
        {
            rect = (RectTransform)transform;
            // Not '??': Unity's missing-component placeholder isn't C# null, so '??' would never add it.
            if (!TryGetComponent(out group))
                group = gameObject.AddComponent<CanvasGroup>();
            if (!restSet)
                restPosition = rect.anchoredPosition;
            seed = Random.value * 10f;
        }

        void OnEnable()
        {
            startedAt = Time.unscaledTime;
            exitStartedAt = -1f;
            Apply();
        }

        /// <summary>Plays the entrance again from the start (e.g. when a banner shows new text).</summary>
        public void Restart(float delay = 0f)
        {
            EntranceDelay = delay;
            startedAt = Time.unscaledTime;
            exitStartedAt = -1f;
            Apply();
        }

        /// <summary>Plays the entrance backwards over 'duration' seconds.</summary>
        public void Exit(float duration, float delay)
        {
            exitDuration = Mathf.Max(0.01f, duration);
            exitStartedAt = Time.unscaledTime + delay;
        }

        void LateUpdate() => Apply();

        void Apply()
        {
            if (rect == null)
                return;
            float now = Time.unscaledTime;
            float t = 1f;
            if (Entrance != null && Entrance.type != MenuEntranceType.None)
                t = Mathf.Clamp01((now - startedAt - EntranceDelay) / Mathf.Max(0.01f, Entrance.duration));
            if (exitStartedAt >= 0f && now >= exitStartedAt)
                t = Mathf.Min(t, 1f - Mathf.Clamp01((now - exitStartedAt) / exitDuration));

            Vector2 offset = Vector2.zero;
            float scale = 1f;
            float alpha = 1f;
            float rotation = 0f;
            if (Entrance != null && t < 1f)
                ApplyEntrance(Entrance, t, ref offset, ref scale, ref alpha, ref rotation);
            ApplyIdle(now, ref offset, ref scale, ref rotation);

            rect.anchoredPosition = restPosition + offset;
            rect.localScale = Vector3.one * (scale * ExtraScale);
            rect.localRotation = Quaternion.Euler(0f, 0f, BaseRotation + rotation + ExtraRotation);
            group.alpha = alpha;
            group.blocksRaycasts = t >= 0.99f || exitStartedAt < 0f;
        }

        static void ApplyEntrance(MenuEntrance entrance, float t, ref Vector2 offset, ref float scale, ref float alpha, ref float rotation)
        {
            float eased = EaseOutBack(t);
            float smooth = 1f - (1f - t) * (1f - t) * (1f - t);
            alpha = Mathf.Clamp01(t * 3f);
            float d = entrance.distance * (1f - smooth);
            switch (entrance.type)
            {
                case MenuEntranceType.Fade: alpha = smooth; break;
                case MenuEntranceType.Pop: scale = Mathf.LerpUnclamped(0.3f, 1f, eased); break;
                case MenuEntranceType.SlideLeft: offset.x = -d; break;
                case MenuEntranceType.SlideRight: offset.x = d; break;
                case MenuEntranceType.SlideUp: offset.y = -d; break;
                case MenuEntranceType.SlideDown: offset.y = d; break;
                case MenuEntranceType.Drop:
                    offset.y = entrance.distance * (1f - EaseOutBounce(t));
                    rotation = (1f - smooth) * 12f;
                    alpha = Mathf.Clamp01(t * 6f);
                    break;
            }
        }

        void ApplyIdle(float now, ref Vector2 offset, ref float scale, ref float rotation)
        {
            if (Idle == null || Idle.type == MenuMotionType.None)
                return;
            float phase = (now * Idle.speed + Idle.phase) * Mathf.PI * 2f;
            switch (Idle.type)
            {
                case MenuMotionType.Bob: offset.y += Mathf.Sin(phase) * Idle.amount; break;
                case MenuMotionType.Pulse: scale *= 1f + Mathf.Sin(phase) * Idle.amount; break;
                case MenuMotionType.Wobble: rotation += Mathf.Sin(phase) * Idle.amount; break;
                case MenuMotionType.Sway:
                    rotation += Mathf.Sin(phase) * Idle.amount;
                    offset.x += Mathf.Sin(phase * 0.5f) * Idle.amount * 1.5f;
                    break;
                case MenuMotionType.Spin: rotation += now * Idle.amount; break;
                case MenuMotionType.Jitter:
                    float step = Mathf.Floor(now * Mathf.Max(1f, Idle.speed * 12f));
                    offset += new Vector2(Mathf.Sin(step * 12.9898f + seed), Mathf.Sin(step * 78.233f + seed)) * Idle.amount * 0.5f;
                    break;
            }
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        static float EaseOutBounce(float t)
        {
            const float n = 7.5625f;
            const float d = 2.75f;
            if (t < 1f / d) return n * t * t;
            if (t < 2f / d) return n * (t -= 1.5f / d) * t + 0.75f;
            if (t < 2.5f / d) return n * (t -= 2.25f / d) * t + 0.9375f;
            return n * (t -= 2.625f / d) * t + 0.984375f;
        }
    }
}

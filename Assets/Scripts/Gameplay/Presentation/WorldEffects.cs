using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Core.Input;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// The camera (static, follow, or scroll, with shake), screen effects, and level audio for a scripted level.
    /// The camera's position is simulation state (saved for rewind); shake, flash, and slow motion are
    /// presentation and run on real time.
    /// </summary>
    public sealed class WorldEffects : MonoBehaviour
    {
        GameplayWorld world;
        AudioSource music;
        WorldDef worldDef;
        Transform cameraTransform;
        float cameraZ;

        // ─── Saved state ──────────────────────────────────────────────
        Vector2 cameraBase;

        float shakeIntensity;
        float shakeEndsAt;
        float slowMotionEndsAt = -1f;
        bool movesCamera;

        public Vector2 CameraBase => cameraBase;
        public CameraMode Mode => worldDef != null ? worldDef.camera : CameraMode.Static;

        public void Init(GameplayWorld owner, AudioSource musicSource, WorldDef def, Transform backgroundRoot)
        {
            world = owner;
            music = musicSource;
            worldDef = def;
            if (world.Camera != null)
            {
                cameraTransform = world.Camera.transform;
                cameraBase = cameraTransform.position;
                cameraZ = cameraTransform.position.z;
            }

            if (Mode != CameraMode.Static && cameraTransform != null)
            {
                // Backgrounds are full-screen videos; keep them on screen as the camera moves.
                if (backgroundRoot != null)
                    backgroundRoot.SetParent(cameraTransform, true);
                BuildMovingBarriers();
            }
        }

        void BuildMovingBarriers()
        {
            if (world.Camera == null || !world.Camera.orthographic)
                return;
            float h = world.Camera.orthographicSize;
            float w = h * world.Camera.aspect;
            int layer = LayerMask.NameToLayer("Boundary");
            if (Mode == CameraMode.Scroll)
            {
                // Edges travel with the camera, so the player can't fall behind or run ahead of the screen.
                Barrier(cameraTransform, "ScrollEdge_Left", new Vector2(-w - 0.5f, 0f), new Vector2(1f, h * 4f), layer);
                Barrier(cameraTransform, "ScrollEdge_Right", new Vector2(w + 0.5f, 0f), new Vector2(1f, h * 4f), layer);
            }
            else if (worldDef.boundsSize.x > 0f && worldDef.boundsSize.y > 0f)
            {
                var root = new GameObject("WorldBounds").transform;
                root.SetParent(transform, false);
                Vector2 c = worldDef.boundsCenter;
                Vector2 s = worldDef.boundsSize;
                Barrier(root, "Bound_Left", c + new Vector2(-s.x * 0.5f - 0.5f, 0f), new Vector2(1f, s.y + 2f), layer);
                Barrier(root, "Bound_Right", c + new Vector2(s.x * 0.5f + 0.5f, 0f), new Vector2(1f, s.y + 2f), layer);
                Barrier(root, "Bound_Top", c + new Vector2(0f, s.y * 0.5f + 0.5f), new Vector2(s.x + 2f, 1f), layer);
                Barrier(root, "Bound_Bottom", c + new Vector2(0f, -s.y * 0.5f - 0.5f), new Vector2(s.x + 2f, 1f), layer);
            }
        }

        static void Barrier(Transform parent, string name, Vector2 localPosition, Vector2 size, int layer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
            if (layer >= 0)
                go.layer = layer;
            go.AddComponent<BoxCollider2D>().size = size;
        }

        // ─── Simulation ───────────────────────────────────────────────

        public void Tick(float now, float dt)
        {
            if (cameraTransform == null || Mode == CameraMode.Static)
                return;

            Vector2 next = cameraBase;
            if (Mode == CameraMode.Scroll)
            {
                next += worldDef.scrollSpeed * dt;
            }
            else if (world.Player != null)
            {
                float t = 1f - Mathf.Exp(-Mathf.Max(0.1f, worldDef.followSharpness) * dt);
                next = Vector2.Lerp(cameraBase, world.Player.Position, t);
            }
            cameraBase = ClampToBounds(next);
            movesCamera = true;
        }

        Vector2 ClampToBounds(Vector2 center)
        {
            if (worldDef.boundsSize.x <= 0f || worldDef.boundsSize.y <= 0f || world.Camera == null)
                return center;
            float h = world.Camera.orthographicSize;
            float w = h * world.Camera.aspect;
            Vector2 min = worldDef.boundsCenter - worldDef.boundsSize * 0.5f + new Vector2(w, h);
            Vector2 max = worldDef.boundsCenter + worldDef.boundsSize * 0.5f - new Vector2(w, h);
            return new Vector2(
                min.x > max.x ? worldDef.boundsCenter.x : Mathf.Clamp(center.x, min.x, max.x),
                min.y > max.y ? worldDef.boundsCenter.y : Mathf.Clamp(center.y, min.y, max.y));
        }

        void LateUpdate()
        {
            if (slowMotionEndsAt >= 0f && Time.unscaledTime >= slowMotionEndsAt)
            {
                slowMotionEndsAt = -1f;
                var clock = GameClock.Instance;
                if (clock != null && !clock.IsRewinding)
                    clock.SetTimeScale(1f);
            }

            if (cameraTransform == null)
                return;
            bool shaking = Time.unscaledTime < shakeEndsAt;
            if (!shaking && !movesCamera)
                return;
            Vector2 offset = shaking ? Random.insideUnitCircle * shakeIntensity : Vector2.zero;
            Vector2 p = cameraBase + offset;
            cameraTransform.position = new Vector3(p.x, p.y, cameraZ);
            if (!shaking && Mode == CameraMode.Static)
                movesCamera = false;
        }

        // ─── Effects ──────────────────────────────────────────────────

        public void Shake(float intensity, float duration)
        {
            shakeIntensity = intensity;
            shakeEndsAt = Time.unscaledTime + duration;
            movesCamera = true;
        }

        public void Flash(Color color, float duration) => world.Hud.Flash(color, duration);

        /// <summary>Floats a short text up from a world point and fades it (cosmetic, real time).</summary>
        public void PopText(Vector2 at, string text, Color color, float size = 1f)
        {
            if (string.IsNullOrEmpty(text))
                return;
            var theme = SickOfIt.Data.GameDefinition.Instance != null ? SickOfIt.Data.GameDefinition.Instance.menuTheme : null;
            var go = new GameObject("PopText");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(at.x, at.y, -0.5f);
            var mesh = go.AddComponent<TMPro.TextMeshPro>();
            // Same font and ink outline as the menus and HUD.
            mesh.font = SickOfIt.Core.UI.Menus.MenuGraphics.FontAsset(theme != null ? theme.font : null);
            mesh.text = text;
            mesh.alignment = TMPro.TextAlignmentOptions.Center;
            mesh.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            mesh.fontSize = 3.2f * Mathf.Max(0.2f, size);
            mesh.fontStyle = TMPro.FontStyles.Bold | TMPro.FontStyles.UpperCase;
            mesh.color = color;
            mesh.outlineWidth = 0.28f;
            mesh.outlineColor = theme != null ? (Color32)theme.title.outlineColor : new Color32(30, 18, 32, 255);
            mesh.rectTransform.sizeDelta = new Vector2(8f, 2f);
            mesh.sortingLayerID = SortingLayer.NameToID(SickOfIt.Data.SpriteLayer.Player.ToString());
            mesh.sortingOrder = 500;
            go.AddComponent<PopTextMotion>().Begin(mesh);
        }

        sealed class PopTextMotion : MonoBehaviour
        {
            const float Duration = 0.9f;
            TMPro.TextMeshPro mesh;
            Color start;
            float age;
            float tilt;

            public void Begin(TMPro.TextMeshPro text)
            {
                mesh = text;
                start = text.color;
                tilt = (Random.value - 0.5f) * 16f;
            }

            void Update()
            {
                age += Time.unscaledDeltaTime;
                transform.position += Vector3.up * (0.9f * Time.unscaledDeltaTime);
                // Pop in big, settle, then fade.
                float pop = age < 0.12f ? Mathf.Lerp(0.4f, 1.25f, age / 0.12f) : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((age - 0.12f) / 0.15f));
                transform.localScale = Vector3.one * pop;
                transform.localRotation = Quaternion.Euler(0f, 0f, tilt * Mathf.Clamp01(1f - age * 2f));
                var c = start;
                c.a = start.a * Mathf.Clamp01((Duration - age) / 0.35f);
                mesh.color = c;
                if (age >= Duration)
                    Destroy(gameObject);
            }
        }

        public void SlowMotion(float scale, float duration)
        {
            var clock = GameClock.Instance;
            if (clock == null || clock.IsRewinding)
                return;
            clock.SetTimeScale(Mathf.Clamp(scale, 0.05f, 4f));
            slowMotionEndsAt = Time.unscaledTime + duration;
        }

        public void PlaySound(AudioClip clip, float volume)
        {
            if (clip == null)
                return;
            Vector3 at = cameraTransform != null ? cameraTransform.position : Vector3.zero;
            AudioSource.PlayClipAtPoint(clip, at, volume);
        }

        public void PlayMusic(AudioClip clip, float volume)
        {
            if (music == null || clip == null)
                return;
            music.clip = clip;
            music.volume = volume;
            music.loop = true;
            music.Play();
        }

        public void MusicVolume(float volume)
        {
            if (music != null)
                music.volume = volume;
        }

        // ─── Rewind ───────────────────────────────────────────────────

        public void SaveState(StateWriter w)
        {
            w.Float(cameraBase.x);
            w.Float(cameraBase.y);
        }

        public void LoadState(StateReader r)
        {
            cameraBase = new Vector2(r.Float(), r.Float());
            if (Mode != CameraMode.Static && cameraTransform != null)
                cameraTransform.position = new Vector3(cameraBase.x, cameraBase.y, cameraZ);
        }
    }
}

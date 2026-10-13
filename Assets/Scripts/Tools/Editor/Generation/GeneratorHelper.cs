#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Animations;
using SickOfIt.Core.Rewind;
using SickOfIt.Combat.Health;
using SickOfIt.Data.Config;

namespace SickOfIt.Tools.Editor.Generation
{
    /// <summary>
    /// Shared utility methods used by all prefab generators.
    /// Eliminates duplicate patterns: sprite detection, animation generation,
    /// rewind component attachment, layer/tag assignment, SerializedObject helpers.
    /// </summary>
    public static class GeneratorHelper
    {
        // ─── Sprite Helpers ──────────────────────────────────────────

        /// <summary>
        /// Get the first Sprite sub-asset from a Texture2D (sliced sprite sheet).
        /// Returns null if texture is null or has no sprite sub-assets.
        /// </summary>
        public static Sprite GetFirstSprite(Texture2D spriteSheet)
        {
            if (spriteSheet == null) return null;
            var assets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(spriteSheet));
            Sprite first = null;
            foreach (var asset in assets)
            {
                if (asset is Sprite sprite &&
                    (first == null || string.Compare(sprite.name, first.name, System.StringComparison.Ordinal) < 0))
                    first = sprite;
            }
            return first;
        }

        /// <summary>
        /// Get all Sprite sub-assets from a Texture2D.
        /// </summary>
        public static Sprite[] GetAllSprites(Texture2D spriteSheet)
        {
            if (spriteSheet == null) return System.Array.Empty<Sprite>();
            var assets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(spriteSheet));
            var list = new System.Collections.Generic.List<Sprite>();
            foreach (var asset in assets)
            {
                if (asset is Sprite sprite)
                    list.Add(sprite);
            }
            return list.ToArray();
        }

        // ─── Animation Helpers ───────────────────────────────────────

        /// <summary>
        /// Generate animation from config or spriteSheet with fallback to simple idle.
        /// Returns the RuntimeAnimatorController, or null if no animation can be made.
        /// </summary>
        public static RuntimeAnimatorController GenerateAnimation(
            AnimationConfig animConfig, string name, string animFolder,
            Texture2D spriteSheet, int fps)
        {
            AnimationGenerator.DeleteFolder(animFolder);
            ProjectGenerator.EnsureFolder(animFolder);

            if (animConfig != null && animConfig.states.Count > 0)
            {
                var result = AnimationGenerator.Generate(animConfig, name, animFolder, spriteSheet, fps);
                return result?.controller;
            }

            if (spriteSheet != null)
            {
                var result = AnimationGenerator.GenerateSimpleIdle(name, animFolder, spriteSheet, fps);
                return result?.controller;
            }

            return null;
        }

        // ─── Rewind Component Helpers ────────────────────────────────

        /// <summary>
        /// Add standard rewind components to a GameObject.
        /// Always adds: RewindableTransform, RewindableGameObject.
        /// Optionally adds RewindableRigidbody2D, RewindableHealth, RewindableAnimator
        /// based on the provided component references.
        /// </summary>
        public static void AddRewindComponents(
            GameObject go,
            Rigidbody2D rb = null,
            Animator animator = null,
            SickOfIt.Combat.Health.PlayerHealth playerHealth = null,
            SickOfIt.Combat.Health.EnemyHealth enemyHealth = null)
        {
            go.AddComponent<RewindableTransform>();
            go.AddComponent<RewindableGameObject>();

            if (rb != null)
                go.AddComponent<RewindableRigidbody2D>();

            if (playerHealth != null || enemyHealth != null)
            {
                var rh = go.AddComponent<RewindableHealth>();
                var rhSo = new SerializedObject(rh);
                if (playerHealth != null)
                    rhSo.FindProperty("playerHealth").objectReferenceValue = playerHealth;
                if (enemyHealth != null)
                    rhSo.FindProperty("enemyHealth").objectReferenceValue = enemyHealth;
                rhSo.ApplyModifiedPropertiesWithoutUndo();
            }

            if (animator != null)
            {
                var ra = go.AddComponent<RewindableAnimator>();
                var raSo = new SerializedObject(ra);
                var animProp = raSo.FindProperty("animator");
                if (animProp != null)
                    animProp.objectReferenceValue = animator;
                raSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>
        /// Orthographic camera that clears the color buffer every frame.
        /// Without this, the previous frame stays on screen and UI smears.
        /// </summary>
        public static void ConfigureSceneCamera(Camera cam, float orthoSize)
        {
            ConfigureSceneCamera(cam, orthoSize, setBackground: false, background: default);
        }

        public static void ConfigureSceneCamera(Camera cam, float orthoSize, Color background)
        {
            ConfigureSceneCamera(cam, orthoSize, setBackground: true, background: background);
        }

        private static void ConfigureSceneCamera(Camera cam, float orthoSize, bool setBackground, Color background)
        {
            if (cam == null) return;

            cam.orthographic = true;
            if (orthoSize > 0f)
                cam.orthographicSize = orthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            if (setBackground)
                cam.backgroundColor = background;

            var aspect = cam.GetComponent<SickOfIt.Core.Device.AspectRatioController>();
            if (aspect == null)
                aspect = cam.gameObject.AddComponent<SickOfIt.Core.Device.AspectRatioController>();
            if (setBackground)
                aspect.letterboxColor = background;
        }

        // ─── Layer/Tag Helpers ───────────────────────────────────────

        /// <summary>
        /// Safely set a layer on a GameObject. Logs warning if layer doesn't exist.
        /// </summary>
        public static void SetLayer(GameObject go, string layerName)
        {
            if (string.IsNullOrEmpty(layerName)) return;
            int layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0)
                go.layer = layer;
            else
                Debug.LogWarning($"[GeneratorHelper] Layer '{layerName}' not found in TagManager");
        }

        /// <summary>
        /// Safely set a tag on a GameObject. Validates tag exists.
        /// </summary>
        public static void SetTag(GameObject go, string tag)
        {
            if (string.IsNullOrEmpty(tag) || tag == "Untagged") return;
            bool exists = false;
            foreach (var t in UnityEditorInternal.InternalEditorUtility.tags)
            {
                if (t == tag) { exists = true; break; }
            }
            if (exists)
                go.tag = tag;
            else
                Debug.LogWarning($"[GeneratorHelper] Tag '{tag}' not defined in TagManager");
        }

        // ─── SerializedObject Helpers ────────────────────────────────

        /// <summary>
        /// Safely set a float property on a SerializedObject. Logs warning if property not found.
        /// </summary>
        public static void SetFloat(SerializedObject so, string propName, float value)
        {
            var prop = so.FindProperty(propName);
            if (prop != null)
                prop.floatValue = value;
            else
                Debug.LogWarning($"[GeneratorHelper] Property '{propName}' not found on {so.targetObject?.GetType().Name}");
        }

        /// <summary>
        /// Safely set an int property on a SerializedObject.
        /// </summary>
        public static void SetInt(SerializedObject so, string propName, int value)
        {
            var prop = so.FindProperty(propName);
            if (prop != null)
                prop.intValue = value;
            else
                Debug.LogWarning($"[GeneratorHelper] Property '{propName}' not found on {so.targetObject?.GetType().Name}");
        }

        /// <summary>
        /// Safely set a bool property on a SerializedObject.
        /// </summary>
        public static void SetBool(SerializedObject so, string propName, bool value)
        {
            var prop = so.FindProperty(propName);
            if (prop != null)
                prop.boolValue = value;
            else
                Debug.LogWarning($"[GeneratorHelper] Property '{propName}' not found on {so.targetObject?.GetType().Name}");
        }

        /// <summary>
        /// Safely set an object reference property on a SerializedObject.
        /// </summary>
        public static void SetRef(SerializedObject so, string propName, Object value)
        {
            var prop = so.FindProperty(propName);
            if (prop != null)
                prop.objectReferenceValue = value;
            else
                Debug.LogWarning($"[GeneratorHelper] Property '{propName}' not found on {so.targetObject?.GetType().Name}");
        }

        /// <summary>
        /// Safely set a Vector2 property on a SerializedObject.
        /// </summary>
        public static void SetVector2(SerializedObject so, string propName, Vector2 value)
        {
            var prop = so.FindProperty(propName);
            if (prop != null)
                prop.vector2Value = value;
            else
                Debug.LogWarning($"[GeneratorHelper] Property '{propName}' not found on {so.targetObject?.GetType().Name}");
        }

        /// <summary>
        /// Safely set a Color property on a SerializedObject.
        /// </summary>
        public static void SetColor(SerializedObject so, string propName, Color value)
        {
            var prop = so.FindProperty(propName);
            if (prop != null)
                prop.colorValue = value;
            else
                Debug.LogWarning($"[GeneratorHelper] Property '{propName}' not found on {so.targetObject?.GetType().Name}");
        }

        // ─── Prefab Helpers ──────────────────────────────────────────

        /// <summary>
        /// Save a GameObject as a prefab asset, destroy the scene instance, and return the prefab.
        /// </summary>
        public static GameObject SavePrefab(GameObject go, string prefabPath)
        {
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        /// <summary>
        /// Writes the controller through the serialized property and saves it with the prefab.
        /// Assigning runtimeAnimatorController alone can leave the loaded prefab on a missing reference.
        /// </summary>
        public static void AssignAnimatorController(Animator animator, RuntimeAnimatorController controller)
        {
            if (animator == null || controller == null)
                return;

            var serialized = new SerializedObject(animator);
            serialized.FindProperty("m_Controller").objectReferenceValue = controller;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(animator);
        }

        /// <summary>
        /// Create and configure an AudioSource component with common defaults.
        /// </summary>
        public static AudioSource AddAudioSource(GameObject go, bool playOnAwake = false, bool loop = false, float spatialBlend = 0f)
        {
            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = playOnAwake;
            audio.loop = loop;
            audio.spatialBlend = spatialBlend;
            return audio;
        }

        /// <summary>
        /// Create a pool GameObject with configured ObjectPool component.
        /// Returns the pool GameObject, or null if prefab not found.
        /// </summary>
        public static GameObject CreatePool(Transform parent, string poolName, string prefabPath, int initialSize)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[GeneratorHelper] Pool prefab not found: {prefabPath}");
                return null;
            }

            var go = new GameObject(poolName);
            go.transform.SetParent(parent);
            var pool = go.AddComponent<SickOfIt.Core.ObjectPool>();

            var poolSo = new SerializedObject(pool);
            poolSo.FindProperty("prefab").objectReferenceValue = prefab;
            poolSo.FindProperty("initialSize").intValue = initialSize;
            poolSo.ApplyModifiedPropertiesWithoutUndo();

            return go;
        }

        // ─── Transport buttons ───────────────────────────────────────

        const string RewindIconPath = "Assets/Images/UI/IconRewind.png";
        const string SkipIconPath = "Assets/Images/UI/IconSkip.png";

        /// <summary>
        /// Gives the rewind control a rewind mark (two triangles pointing left).
        /// Video and death scenes also get a skip control (triangle and bar) beside it.
        /// </summary>
        public static void StyleTransportControls(GameObject uiRoot, bool includeSkip)
        {
            if (uiRoot == null) return;

            // Build both icons before touching a scene. The video scene is generated
            // first, and assigning a sprite before its import finishes leaves the old arrow.
            GetTransportIcon(rewindIcon: true);
            if (includeSkip)
                GetTransportIcon(rewindIcon: false);

            var rewind = FindNamedButton(uiRoot, "rewind");
            if (rewind == null) return;

            ApplyTransportIcon(rewind.gameObject, rewindIcon: true);

            if (!includeSkip) return;

            var skip = FindNamedButton(uiRoot, "skip");
            if (skip == null)
            {
                var skipGo = Object.Instantiate(rewind.gameObject, rewind.transform.parent);
                skipGo.name = "(Prb)ButtonSkip";
                foreach (var behaviour in skipGo.GetComponents<MonoBehaviour>())
                {
                    if (behaviour != null && behaviour.GetType().Name == "MusicCycler")
                        Object.DestroyImmediate(behaviour);
                }

                var source = rewind.GetComponent<RectTransform>();
                var rect = skipGo.GetComponent<RectTransform>();
                float gap = Mathf.Max(source.sizeDelta.x, 80f) + 16f;
                rect.anchoredPosition = source.anchoredPosition + new Vector2(gap, 0f);
                skip = skipGo.GetComponent<MobileControllerFramework.Button>();
            }

            if (skip != null)
                ApplyTransportIcon(skip.gameObject, rewindIcon: false);
        }

        static MobileControllerFramework.Button FindNamedButton(GameObject uiRoot, string namePart)
        {
            foreach (var button in uiRoot.GetComponentsInChildren<MobileControllerFramework.Button>(true))
            {
                if (button.gameObject.name.ToLowerInvariant().Contains(namePart))
                    return button;
            }
            return null;
        }

        static void ApplyTransportIcon(GameObject button, bool rewindIcon)
        {
            var sprite = GetTransportIcon(rewindIcon);
            if (sprite == null) return;

            foreach (var image in button.GetComponentsInChildren<Image>(true))
            {
                if (image.gameObject.name != "_iconImg") continue;
                image.sprite = sprite;
                image.color = Color.white;
                image.preserveAspect = true;
                image.enabled = true;
                EditorUtility.SetDirty(image);
                if (PrefabUtility.IsPartOfPrefabInstance(image))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(image);
            }
        }

        static Sprite GetTransportIcon(bool rewindIcon)
        {
            string path = rewindIcon ? RewindIconPath : SkipIconPath;
            var existing = LoadSprite(path);
            if (existing != null) return existing;

            const int size = 128;
            const int samples = 4;
            var pixels = new Color32[size * size];
            int sampleCount = samples * samples;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < samples; sy++)
                    {
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float px = x + (sx + 0.5f) / samples;
                            float py = y + (sy + 0.5f) / samples;
                            if (rewindIcon ? InsideRewind(px, py) : InsideSkip(px, py))
                                hit++;
                        }
                    }
                    byte alpha = (byte)(hit * 255 / sampleCount);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, png);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            var sprite = LoadSprite(path);
            if (sprite == null)
                Debug.LogWarning($"[GeneratorHelper] Transport icon did not import: {path}");
            return sprite;
        }

        static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Sprite found)
                    return found;
            }
            return null;
        }

        static bool InsideRewind(float x, float y)
        {
            return PointInTriangle(x, y, 16f, 64f, 54f, 26f, 54f, 102f)
                || PointInTriangle(x, y, 62f, 64f, 108f, 26f, 108f, 102f);
        }

        static bool InsideSkip(float x, float y)
        {
            bool triangle = PointInTriangle(x, y, 78f, 64f, 20f, 26f, 20f, 102f);
            bool bar = x >= 96f && x <= 112f && y >= 26f && y <= 102f;
            return triangle || bar;
        }

        static bool PointInTriangle(float x, float y, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float d1 = (x - bx) * (ay - by) - (ax - bx) * (y - by);
            float d2 = (x - cx) * (by - cy) - (bx - cx) * (y - cy);
            float d3 = (x - ax) * (cy - ay) - (cx - ax) * (y - ay);
            bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNeg && hasPos);
        }
    }
}
#endif

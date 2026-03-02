#if UNITY_EDITOR
using UnityEngine;
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
            foreach (var asset in assets)
            {
                if (asset is Sprite sprite)
                    return sprite;
            }
            return null;
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
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            return prefab;
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
    }
}
#endif

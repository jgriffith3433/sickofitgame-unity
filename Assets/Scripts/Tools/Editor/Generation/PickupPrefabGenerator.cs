#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using SickOfIt.Combat;
using SickOfIt.Core.Rewind;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Tools.Editor.Generation
{
    /// <summary>
    /// Generates the Diamond (points pickup) prefab from PickupConfig.
    ///
    /// Called by ProjectGenerator during the prefab generation phase.
    /// Prefab is generated to Assets/Prefabs/Combat/Diamond.prefab
    ///
    /// Created prefab has:
    /// - SpriteRenderer
    /// - CircleCollider2D (trigger)
    /// - PointsPickup
    /// - Rewind: RewindableTransform, RewindableGameObject
    ///
    /// All serialized fields are wired via SerializedObject.
    /// </summary>
    public static class PickupPrefabGenerator
    {
        private const string OUTPUT_PATH = "Assets/Prefabs";
        private const string PREFAB_NAME = "Diamond";

        /// <summary>
        /// Delete the generated Diamond prefab.
        /// </summary>
        public static void Delete()
        {
            string prefabPath = $"{OUTPUT_PATH}/{PREFAB_NAME}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                AssetDatabase.DeleteAsset(prefabPath);
                Debug.Log($"[PickupPrefabGen] Deleted: {prefabPath}");
            }
        }

        /// <summary>
        /// Generate the Diamond prefab from GameDefinition.pickupConfig.
        /// Updates GameDefinition.pickupPrefab with the generated prefab.
        /// </summary>
        public static GameObject Generate(GameDefinition gd)
        {
            if (gd == null)
            {
                Debug.LogError("[PickupPrefabGen] No GameDefinition provided!");
                return null;
            }

            var config = gd.pickupConfig;
            if (config == null)
            {
                Debug.LogWarning("[PickupPrefabGen] No PickupConfig in GameDefinition — using defaults");
                config = new PickupConfig();
            }

            ProjectGenerator.EnsureFolder(OUTPUT_PATH);
            string prefabPath = $"{OUTPUT_PATH}/{PREFAB_NAME}.prefab";

            // ─── Create root GameObject ───────────────────────────────────
            var go = new GameObject(PREFAB_NAME);

            // ─── SpriteRenderer ───────────────────────────────────────────
            var sr = go.AddComponent<SpriteRenderer>();
            if (config.sprite != null)
                sr.sprite = config.sprite;
            sr.sortingOrder = config.sortingOrder;
            go.transform.localScale = Vector3.one * config.spriteScale;

            // ─── CircleCollider2D (trigger) ───────────────────────────────
            var collider = go.AddComponent<CircleCollider2D>();
            collider.radius = config.colliderRadius;
            collider.isTrigger = true;

            // ─── PointsPickup component ───────────────────────────────────
            var pickup = go.AddComponent<PointsPickup>();
            var so = new SerializedObject(pickup);
            so.FindProperty("pointsValue").intValue = 1; // Default; overridden at runtime by SetPointsValue()
            so.FindProperty("lifetime").floatValue = config.lifetime;
            so.FindProperty("popForce").floatValue = config.popForce;
            so.FindProperty("gravity").floatValue = config.gravity;
            so.FindProperty("spreadRange").floatValue = config.spreadRange;
            so.FindProperty("bobAmplitude").floatValue = config.bobAmplitude;
            so.FindProperty("bobSpeed").floatValue = config.bobSpeed;
            so.FindProperty("col").objectReferenceValue = collider;
            so.ApplyModifiedPropertiesWithoutUndo();

            // ─── Layer / Tag ──────────────────────────────────────────────
            if (!string.IsNullOrEmpty(config.layerName))
            {
                int layer = LayerMask.NameToLayer(config.layerName);
                if (layer >= 0) go.layer = layer;
            }
            if (!string.IsNullOrEmpty(config.tag) && config.tag != "Untagged")
            {
                try { go.tag = config.tag; }
                catch { /* Tag doesn't exist */ }
            }

            // ─── Animation (from config) ──────────────────────────────
            RuntimeAnimatorController animController = null;
            string animFolder = "Assets/Animations/Pickups";
            AnimationGenerator.DeleteFolder(animFolder);

            if (config.animationConfig != null && config.animationConfig.states.Count > 0)
            {
                var animResult = AnimationGenerator.Generate(
                    config.animationConfig, PREFAB_NAME, animFolder,
                    config.spriteSheet, config.animationFps);
                if (animResult != null)
                    animController = animResult.controller;
            }
            else if (config.spriteSheet != null)
            {
                var animResult = AnimationGenerator.GenerateSimpleIdle(
                    PREFAB_NAME, animFolder, config.spriteSheet, config.animationFps);
                if (animResult != null)
                    animController = animResult.controller;
            }

            if (animController != null)
            {
                var animator = go.AddComponent<Animator>();
                animator.runtimeAnimatorController = animController;
            }

            // ─── Rewind components ────────────────────────────────────────
            go.AddComponent<RewindableTransform>();
            go.AddComponent<RewindableGameObject>();

            // ─── Save as prefab ───────────────────────────────────────────
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);

            // Update GameDefinition with generated prefab
            gd.pickupPrefab = prefab;
            EditorUtility.SetDirty(gd);

            Debug.Log($"[PickupPrefabGen] Created: {prefabPath}");
            return prefab;
        }
    }
}
#endif

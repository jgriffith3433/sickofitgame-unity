#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using SickOfIt.Core.Input;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Tools.Editor.Generation
{
    /// <summary>
    /// Generates the Platform prefab from GameDefinition.
    ///
    /// Called by ProjectGenerator during the prefab generation phase.
    /// Prefab is generated to Assets/Prefabs/Platform.prefab
    ///
    /// Created prefab has:
    /// - SpriteRenderer (default grey)
    /// - BoxCollider2D (thin collision surface)
    /// - PlatformEffector2D (one-way, surface arc 180)
    /// - DropThroughPlatform script (for drop-through interaction)
    ///
    /// LevelRuntime instantiates this prefab at runtime for each PlatformConfig entry,
    /// then configures position, width, scale, and wires the PlayerController reference.
    ///
    /// All serialized fields are wired via SerializedObject.
    /// </summary>
    public static class PlatformPrefabGenerator
    {
        private const string OUTPUT_PATH = "Assets/Prefabs";
        private const string PREFAB_NAME = "Platform";

        /// <summary>
        /// Delete the generated Platform prefab.
        /// </summary>
        public static void Delete()
        {
            string prefabPath = $"{OUTPUT_PATH}/{PREFAB_NAME}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                AssetDatabase.DeleteAsset(prefabPath);
                ////Debug.Log($"[PlatformPrefabGen] Deleted: {prefabPath}");
            }
        }

        /// <summary>
        /// Generate the Platform prefab.
        /// Updates GameDefinition.platformPrefab with the generated prefab.
        /// </summary>
        public static GameObject Generate(GameDefinition gd)
        {
            if (gd == null)
            {
                Debug.LogError("[PlatformPrefabGen] No GameDefinition provided!");
                return null;
            }

            ProjectGenerator.EnsureFolder(OUTPUT_PATH);
            string prefabPath = $"{OUTPUT_PATH}/{PREFAB_NAME}.prefab";

            // ─── Create root GameObject ───────────────────────────────────
            var go = new GameObject(PREFAB_NAME);

            // ─── SpriteRenderer ───────────────────────────────────────────
            var sr = go.AddComponent<SpriteRenderer>();
            sr.color = new Color(0.4f, 0.4f, 0.4f);
            SpriteLayers.Apply(sr, SpriteLayer.Player, SpriteLayers.Platform);

            // ─── PlatformEffector2D (one-way) ─────────────────────────────
            var effector = go.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 180f;
            effector.useColliderMask = true;

            // ─── BoxCollider2D ────────────────────────────────────────────
            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1f, gd.platformColliderHeight > 0 ? gd.platformColliderHeight : 0.1f);
            collider.usedByEffector = true;

            // Set platform layer
            GeneratorHelper.SetLayer(go, "Platform");
            if (go.layer == 0) // Fallback if Platform layer doesn't exist
                GeneratorHelper.SetLayer(go, "Ground");

            // ─── DropThroughPlatform script ───────────────────────────────
            var dropThrough = go.AddComponent<DropThroughPlatform>();
            var dtSo = new SerializedObject(dropThrough);
            dtSo.FindProperty("platformCollider").objectReferenceValue = collider;
            dtSo.ApplyModifiedPropertiesWithoutUndo();

            // ─── Save as prefab ───────────────────────────────────────────
            var prefab = GeneratorHelper.SavePrefab(go, prefabPath);

            // Update GameDefinition with generated prefab
            gd.platformPrefab = prefab;
            EditorUtility.SetDirty(gd);

            ////Debug.Log($"[PlatformPrefabGen] Created: {prefabPath}");
            return prefab;
        }
    }
}
#endif

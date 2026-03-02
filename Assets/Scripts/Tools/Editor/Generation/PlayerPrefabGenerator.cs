#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Core.Input;
using SickOfIt.Core.Rewind;
using SickOfIt.Combat.Effects;
using SickOfIt.Combat.Health;

namespace SickOfIt.Tools.Editor.Generation
{
    /// <summary>
    /// Generates the Character prefab from PlayerConfig.
    /// 
    /// Called by ProjectGenerator during the prefab generation phase.
    /// Prefab is generated to Assets/Prefabs/Character.prefab
    /// 
    /// Created prefab has:
    /// - Root "Character"
    ///   - Graphics child (SpriteRenderer, Animator)
    ///   - Shadow child (SpriteRenderer)
    /// - PlayerController (all movement/combat/shadow/input settings wired from config)
    /// - PlayerHealth (invincibility settings wired from config)
    /// - GlitchEffect (wired to PlayerHealth)
    /// - Rigidbody2D, CircleCollider2D
    /// - Rewind components: RewindableTransform, RewindableRigidbody2D, RewindableGameObject, RewindableHealth, RewindableAnimator
    /// 
    /// All serialized fields are wired via SerializedObject.
    /// </summary>
    public static class PlayerPrefabGenerator
    {
        private const string OUTPUT_PATH = "Assets/Prefabs";
        private const string PREFAB_NAME = "Character";
        private const string ANIM_PATH = "Assets/Animations/Player";

        /// <summary>
        /// Delete the generated Character prefab.
        /// </summary>
        public static void Delete()
        {
            string prefabPath = $"{OUTPUT_PATH}/{PREFAB_NAME}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                AssetDatabase.DeleteAsset(prefabPath);
                Debug.Log($"[PlayerPrefabGen] Deleted: {prefabPath}");
            }
        }

        /// <summary>
        /// Generate the Character prefab from GameDefinition.player.
        /// </summary>
        public static GameObject Generate(GameDefinition gd)
        {
            if (gd == null)
            {
                Debug.LogError("[PlayerPrefabGen] No GameDefinition provided!");
                return null;
            }

            var config = gd.player;
            if (config == null)
            {
                Debug.LogError("[PlayerPrefabGen] No PlayerConfig in GameDefinition!");
                return null;
            }

            ProjectGenerator.EnsureFolder(OUTPUT_PATH);
            ProjectGenerator.EnsureFolder(ANIM_PATH);

            string prefabPath = $"{OUTPUT_PATH}/{PREFAB_NAME}.prefab";

            // ─── Create root GameObject ───────────────────────────────────
            var root = new GameObject(PREFAB_NAME);
            root.tag = "Player";

            // ─── Physics ──────────────────────────────────────────────────
            var rb = root.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = config.gravityScale;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var collider = root.AddComponent<CircleCollider2D>();
            collider.radius = config.colliderRadius > 0 ? config.colliderRadius : 0.3f;

            // ─── Graphics child ───────────────────────────────────────────
            var graphics = new GameObject("Graphics");
            graphics.transform.SetParent(root.transform);
            graphics.transform.localPosition = Vector3.zero;

            var sr = graphics.AddComponent<SpriteRenderer>();
            // Auto-detect default sprite from sprite sheet
            if (config.spriteSheet != null)
            {
                var sprites = AssetDatabase.LoadAllAssetsAtPath(
                    AssetDatabase.GetAssetPath(config.spriteSheet));
                foreach (var asset in sprites)
                {
                    if (asset is Sprite sprite)
                    {
                        sr.sprite = sprite;
                        break;
                    }
                }
            }
            sr.sortingLayerName = "Default";
            sr.sortingOrder = 5;

            // ─── Auto-populate animation config if missing ────────────────
            if (config.animationConfig == null || config.animationConfig.states.Count == 0)
            {
                config.animationConfig = AnimationConfigPopulator.CreatePlayerAnimConfig(config);
                if (config.animationConfig != null)
                {
                    EditorUtility.SetDirty(gd);
                    Debug.Log("[PlayerPrefabGen] Auto-populated player animation config");
                }
            }

            // ─── Animation generation via AnimationGenerator ──────────────
            Animator animator = null;
            RuntimeAnimatorController animController = null;

            // Clean old animation assets
            AnimationGenerator.DeleteFolder(ANIM_PATH);
            ProjectGenerator.EnsureFolder(ANIM_PATH);

            if (config.animationConfig != null && config.animationConfig.states.Count > 0)
            {
                // Full multi-state animation from config
                var animResult = AnimationGenerator.Generate(
                    config.animationConfig,
                    "Player",
                    ANIM_PATH,
                    config.spriteSheet,
                    config.animationFps);

                if (animResult != null)
                    animController = animResult.controller;
            }
            else if (config.spriteSheet != null)
            {
                // Simple idle fallback
                var animResult = AnimationGenerator.GenerateSimpleIdle(
                    "Player", ANIM_PATH, config.spriteSheet, config.animationFps);

                if (animResult != null)
                    animController = animResult.controller;
            }

            if (animController != null)
            {
                animator = graphics.AddComponent<Animator>();
                animator.runtimeAnimatorController = animController;
            }

            // ─── Shadow child ─────────────────────────────────────────────
            var shadow = new GameObject("Shadow");
            shadow.transform.SetParent(root.transform);
            shadow.transform.localPosition = config.shadowOffset;

            var shadowSr = shadow.AddComponent<SpriteRenderer>();
            if (config.shadowSprite != null)
            {
                shadowSr.sprite = config.shadowSprite;
            }
            shadowSr.sortingLayerName = "Default";
            shadowSr.sortingOrder = 0;
            shadowSr.color = config.shadowColor;

            // Shadow animator (generated from sprite sheet if available)
            Animator shadowAnimator = null;
            RuntimeAnimatorController shadowAnimCtrl = null;

            if (config.shadowSpriteSheet != null)
            {
                string shadowAnimFolder = "Assets/Animations/Shadow";
                AnimationGenerator.DeleteFolder(shadowAnimFolder);
                var shadowAnimResult = AnimationGenerator.GenerateSimpleIdle(
                    "Shadow", shadowAnimFolder, config.shadowSpriteSheet, config.shadowAnimationFps);
                if (shadowAnimResult != null)
                    shadowAnimCtrl = shadowAnimResult.controller;
            }

            if (shadowAnimCtrl != null)
            {
                shadowAnimator = shadow.AddComponent<Animator>();
                shadowAnimator.runtimeAnimatorController = shadowAnimCtrl;
            }

            // ─── PlayerHealth ─────────────────────────────────────────────
            var playerHealth = root.AddComponent<PlayerHealth>();

            // ─── PlayerController ─────────────────────────────────────────
            var playerController = root.AddComponent<PlayerController>();

            // Wire PlayerController serialized fields
            var pcSo = new SerializedObject(playerController);
            pcSo.FindProperty("rb").objectReferenceValue = rb;
            pcSo.FindProperty("col").objectReferenceValue = collider;
            pcSo.FindProperty("graphicsTransform").objectReferenceValue = graphics.transform;
            if (animator != null)
                pcSo.FindProperty("animator").objectReferenceValue = animator;
            pcSo.FindProperty("shadowSprite").objectReferenceValue = shadow.transform;
            
            // Movement settings from config
            pcSo.FindProperty("moveSpeed").floatValue = config.moveSpeed;
            pcSo.FindProperty("jumpForce").floatValue = config.jumpForce;
            pcSo.FindProperty("gravityScale").floatValue = config.gravityScale;
            pcSo.FindProperty("groundCheckDistance").floatValue = config.groundCheckDistance;
            pcSo.FindProperty("groundCheckWidth").floatValue = config.groundCheckWidth;
            
            // Rotation settings from config
            pcSo.FindProperty("normalRotationSpeed").floatValue = config.normalRotationSpeed;
            pcSo.FindProperty("firingRotationSpeed").floatValue = config.firingRotationSpeed;

            // Drop-through settings from config
            pcSo.FindProperty("dropThroughForce").floatValue = config.dropThroughForce;
            pcSo.FindProperty("dropThroughThreshold").floatValue = config.dropThroughThreshold;

            // Shadow settings from config
            pcSo.FindProperty("shadowMaxHeight").floatValue = config.shadowMaxHeight;
            pcSo.FindProperty("shadowMinScale").floatValue = config.shadowMinScale;
            pcSo.FindProperty("shadowRaycastDistance").floatValue = config.shadowRaycastDistance;

            // Input settings from config
            pcSo.FindProperty("jumpDetectVelocityThreshold").floatValue = config.jumpDetectVelocityThreshold;
            pcSo.FindProperty("inputDeadzone").floatValue = config.inputDeadzone;
            pcSo.FindProperty("mobileDropThreshold").floatValue = config.mobileDropThreshold;

            // Air jumps from config
            pcSo.FindProperty("maxAirJumps").intValue = config.maxExtraJumps;

            // Combat settings from config
            pcSo.FindProperty("enableShooting").boolValue = config.enableShooting;
            pcSo.FindProperty("fireRate").floatValue = config.fireRate;
            pcSo.FindProperty("projectileSpeed").floatValue = config.projectileSpeed;
            pcSo.FindProperty("projectileLifetime").floatValue = config.projectileLifetime;
            
            pcSo.ApplyModifiedPropertiesWithoutUndo();

            // Wire PlayerHealth serialized fields
            var phSo = new SerializedObject(playerHealth);
            phSo.FindProperty("controller").objectReferenceValue = playerController;
            phSo.FindProperty("hasInvincibilityFrames").boolValue = config.hasInvincibilityFrames;
            phSo.FindProperty("invincibilityDuration").floatValue = config.invincibilityDuration;
            phSo.ApplyModifiedPropertiesWithoutUndo();

            // ─── GlitchEffect ──────────────────────────────────────────
            var glitchEffect = root.AddComponent<GlitchEffect>();
            var geSo = new SerializedObject(glitchEffect);
            geSo.FindProperty("playerHealth").objectReferenceValue = playerHealth;
            geSo.ApplyModifiedPropertiesWithoutUndo();

            // ─── Rewind components ────────────────────────────────────────
            root.AddComponent<RewindableTransform>();
            root.AddComponent<RewindableRigidbody2D>();
            root.AddComponent<RewindableGameObject>();
            
            var rewindableHealth = root.AddComponent<RewindableHealth>();
            var rhSo = new SerializedObject(rewindableHealth);
            rhSo.FindProperty("playerHealth").objectReferenceValue = playerHealth;
            rhSo.ApplyModifiedPropertiesWithoutUndo();

            // RewindableAnimator (if animator exists on Graphics child)
            if (animator != null)
            {
                var rewindableAnimator = root.AddComponent<RewindableAnimator>();
                var raSo = new SerializedObject(rewindableAnimator);
                var animRefProp = raSo.FindProperty("animator");
                if (animRefProp != null)
                    animRefProp.objectReferenceValue = animator;
                raSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ─── Save as prefab ───────────────────────────────────────────
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

            // Clean up scene object
            Object.DestroyImmediate(root);

            // Update GameDefinition with generated prefab
            gd.player.playerPrefab = prefab;
            EditorUtility.SetDirty(gd);

            Debug.Log($"[PlayerPrefabGen] Created: {prefabPath}");

            return prefab;
        }
    }
}
#endif

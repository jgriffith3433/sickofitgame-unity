#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Core.Input;
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
            root.layer = LayerMask.NameToLayer("Player");
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
            
            // Apply sprite scale (default to 0.33 if not set to compensate for large sprites)
            float spriteScale = config.spriteScale > 0 ? config.spriteScale : 0.15f;
            graphics.transform.localScale = Vector3.one * spriteScale;

            var sr = graphics.AddComponent<SpriteRenderer>();
            // Auto-detect default sprite from sprite sheet
            sr.sprite = GeneratorHelper.GetFirstSprite(config.spriteSheet);
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

            // ─── AudioSource for SFX ──────────────────────────────────────
            var audioSource = GeneratorHelper.AddAudioSource(root);

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
            pcSo.FindProperty("use2DFlip").boolValue = config.use2DFlip;
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

            // Audio
            pcSo.FindProperty("audioSource").objectReferenceValue = audioSource;
            pcSo.FindProperty("pitchVariation").floatValue = config.pitchVariation;
            WireSFXArray(pcSo, "hurtSounds", config.hurtSounds);
            WireSFXArray(pcSo, "deathSounds", config.deathSounds);
            if (config.jumpSound != null)
                pcSo.FindProperty("jumpSound").objectReferenceValue = config.jumpSound;
            if (config.fireSound != null)
                pcSo.FindProperty("fireSound").objectReferenceValue = config.fireSound;
            if (config.landSound != null)
                pcSo.FindProperty("landSound").objectReferenceValue = config.landSound;

            // ─── Create ProjectileSpawnPoint child ────────────────────────
            var spawnPoint = new GameObject("ProjectileSpawnPoint");
            spawnPoint.transform.SetParent(root.transform);
            spawnPoint.transform.localPosition = new Vector3(0.5f, 0f, 0f);
            pcSo.FindProperty("projectileSpawnPoint").objectReferenceValue = spawnPoint.transform;

            // ─── Set LayerMasks ───────────────────────────────────────────
            // Ground uses Platform layer, boundaries use Boundary layer
            pcSo.FindProperty("groundLayer").intValue = LayerMask.GetMask("Platform", "Boundary");
            pcSo.FindProperty("platformLayer").intValue = LayerMask.GetMask("Platform");

            // ─── Wire InputActionReferences ───────────────────────────────
            var inputAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            if (inputAsset != null)
            {
                WireInputAction(pcSo, "moveAction", inputAsset, "Character", "Move");
                WireInputAction(pcSo, "jumpAction", inputAsset, "Character", "Jump");
                WireInputAction(pcSo, "fireAction", inputAsset, "Character", "Attack");
                WireInputAction(pcSo, "lookAction", inputAsset, "Character", "Look");
            }
            
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
            GeneratorHelper.AddRewindComponents(root, rb: rb, animator: animator, playerHealth: playerHealth);

            // Graphics child also needs RewindableTransform for sprite flip/rotation during rewind
            graphics.AddComponent<SickOfIt.Core.Rewind.RewindableTransform>();

            // ─── Save as prefab ───────────────────────────────────────────
            var prefab = GeneratorHelper.SavePrefab(root, prefabPath);

            // Update GameDefinition with generated prefab
            gd.player.playerPrefab = prefab;
            EditorUtility.SetDirty(gd);

            Debug.Log($"[PlayerPrefabGen] Created: {prefabPath}");

            return prefab;
        }

        /// <summary>
        /// Wire an AudioClip array into a SerializedProperty.
        /// </summary>
        private static void WireSFXArray(SerializedObject so, string propertyName, AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return;
            var prop = so.FindProperty(propertyName);
            if (prop == null) return;
            prop.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null)
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            }
        }

        /// <summary>
        /// Wires an InputActionReference to a SerializedProperty.
        /// </summary>
        private static void WireInputAction(SerializedObject so, string propertyName, InputActionAsset asset, string mapName, string actionName)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null) return;

            var map = asset.FindActionMap(mapName);
            if (map == null) return;

            var action = map.FindAction(actionName);
            if (action == null) return;

            // Create or find InputActionReference asset
            var refPath = $"Assets/InputSystem_Actions.inputactions";
            var refs = AssetDatabase.LoadAllAssetsAtPath(refPath);
            foreach (var r in refs)
            {
                if (r is InputActionReference iar && iar.action?.id == action.id)
                {
                    prop.objectReferenceValue = iar;
                    return;
                }
            }
            // If no reference found, create one inline (won't persist but works at runtime)
            var newRef = InputActionReference.Create(action);
            prop.objectReferenceValue = newRef;
        }
    }
}
#endif

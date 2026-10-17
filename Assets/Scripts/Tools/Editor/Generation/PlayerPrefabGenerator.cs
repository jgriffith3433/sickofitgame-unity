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
            // Character.prefab is instanced by the hand-authored MainMenu scene.
            // Deleting it assigns a new GUID and new component ids, which breaks that instance.
            // Generate() overwrites the existing prefab in place instead.
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
            var animController = BuildPlayerAnimator(config, gd);
            var shadowAnimCtrl = BuildShadowAnimator(config);

            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                return UpdateExistingPrefab(gd, config, prefabPath, animController, shadowAnimCtrl);

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
            float spriteScale = config.spriteScale > 0 ? config.spriteScale : 0.25f;
            graphics.transform.localScale = new Vector3(-spriteScale, spriteScale, spriteScale);

            var sr = graphics.AddComponent<SpriteRenderer>();
            sr.sprite = DefaultSprite(config);
            SpriteLayers.Apply(sr, SpriteLayer.Player, SpriteLayers.Character);

            Animator animator = null;
            if (animController != null)
            {
                animator = graphics.AddComponent<Animator>();
                GeneratorHelper.AssignAnimatorController(animator, animController);
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
            SpriteLayers.Apply(shadowSr, SpriteLayer.Player, SpriteLayers.Shadow);
            shadowSr.color = config.shadowColor;

            if (shadowAnimCtrl != null)
            {
                var shadowAnimator = shadow.AddComponent<Animator>();
                GeneratorHelper.AssignAnimatorController(shadowAnimator, shadowAnimCtrl);
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
            spawnPoint.transform.localPosition = new Vector3(config.projectileSpawnOffset.x, config.projectileSpawnOffset.y, 0f);
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
                WireInputAction(pcSo, "fireAction", inputAsset, "Character", "Fire");
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

            ////Debug.Log($"[PlayerPrefabGen] Created: {prefabPath}");

            return prefab;
        }

        /// <summary>
        /// Sprite shown before the Animator plays. Uses the first frame of Idle.
        /// </summary>
        static Sprite DefaultSprite(PlayerConfig config)
        {
            Texture2D sheet = config.spriteSheet;
            if (config.animationConfig != null && config.animationConfig.states != null)
            {
                foreach (var state in config.animationConfig.states)
                {
                    if (state != null && state.stateName == "Idle" && state.spriteSheet != null)
                    {
                        sheet = state.spriteSheet;
                        break;
                    }
                }
            }

            return GeneratorHelper.GetFirstSprite(sheet);
        }

        private static RuntimeAnimatorController BuildPlayerAnimator(PlayerConfig config, GameDefinition gd)
        {
            if (config.animationConfig == null || config.animationConfig.states.Count == 0)
            {
                config.animationConfig = AnimationConfigPopulator.CreatePlayerAnimConfig(config);
                if (config.animationConfig != null)
                {
                    EditorUtility.SetDirty(gd);
                    ////Debug.Log("[PlayerPrefabGen] Auto-populated player animation config");
                }
            }

            AnimationGenerator.DeleteFolder(ANIM_PATH);
            ProjectGenerator.EnsureFolder(ANIM_PATH);

            if (config.animationConfig != null && config.animationConfig.states.Count > 0)
            {
                var animResult = AnimationGenerator.Generate(
                    config.animationConfig,
                    "Player",
                    ANIM_PATH,
                    config.spriteSheet,
                    config.animationFps);
                return animResult != null ? animResult.controller : null;
            }

            if (config.spriteSheet != null)
            {
                var animResult = AnimationGenerator.GenerateSimpleIdle(
                    "Player", ANIM_PATH, config.spriteSheet, config.animationFps);
                return animResult != null ? animResult.controller : null;
            }

            return null;
        }

        private static RuntimeAnimatorController BuildShadowAnimator(PlayerConfig config)
        {
            if (config.shadowSpriteSheet == null)
                return null;

            string shadowAnimFolder = "Assets/Animations/Shadow";
            AnimationGenerator.DeleteFolder(shadowAnimFolder);
            var shadowAnimResult = AnimationGenerator.GenerateSimpleIdle(
                "Shadow", shadowAnimFolder, config.shadowSpriteSheet, config.shadowAnimationFps);
            return shadowAnimResult != null ? shadowAnimResult.controller : null;
        }

        /// <summary>
        /// Refresh config-driven fields on the existing Character prefab without
        /// replacing it. MainMenu keeps a valid prefab instance.
        /// </summary>
        private static GameObject UpdateExistingPrefab(
            GameDefinition gd,
            PlayerConfig config,
            string prefabPath,
            RuntimeAnimatorController animController,
            RuntimeAnimatorController shadowAnimCtrl)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            var playerController = root.GetComponent<PlayerController>();
            var rb = root.GetComponent<Rigidbody2D>();
            var collider = root.GetComponent<CircleCollider2D>();
            var graphics = root.transform.Find("Graphics");
            var shadow = root.transform.Find("Shadow");

            if (playerController == null || rb == null || collider == null || graphics == null)
            {
                PrefabUtility.UnloadPrefabContents(root);
                Debug.LogWarning("[PlayerPrefabGen] Existing Character prefab is missing components. Rebuilding it.");
                AssetDatabase.DeleteAsset(prefabPath);
                return Generate(gd);
            }

            rb.gravityScale = config.gravityScale;
            collider.radius = config.colliderRadius > 0 ? config.colliderRadius : 0.3f;

            float spriteScale = config.spriteScale > 0 ? config.spriteScale : 0.25f;
            graphics.localScale = new Vector3(-spriteScale, spriteScale, spriteScale);

            var spriteRenderer = graphics.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = DefaultSprite(config);
                SpriteLayers.Apply(spriteRenderer, SpriteLayer.Player, SpriteLayers.Character);
            }

            var animator = graphics.GetComponent<Animator>();
            if (animController != null)
            {
                if (animator == null)
                    animator = graphics.gameObject.AddComponent<Animator>();
                GeneratorHelper.AssignAnimatorController(animator, animController);
            }

            if (shadow != null)
            {
                shadow.localPosition = config.shadowOffset;
                var shadowRenderer = shadow.GetComponent<SpriteRenderer>();
                if (shadowRenderer != null)
                    SpriteLayers.Apply(shadowRenderer, SpriteLayer.Player, SpriteLayers.Shadow);
                var shadowAnimator = shadow.GetComponent<Animator>();
                if (shadowAnimCtrl != null)
                {
                    if (shadowAnimator == null)
                        shadowAnimator = shadow.gameObject.AddComponent<Animator>();
                    GeneratorHelper.AssignAnimatorController(shadowAnimator, shadowAnimCtrl);
                }
            }

            var audioSource = root.GetComponent<AudioSource>();
            var spawnPoint = root.transform.Find("ProjectileSpawnPoint");
            if (spawnPoint != null)
                spawnPoint.localPosition = new Vector3(config.projectileSpawnOffset.x, config.projectileSpawnOffset.y, 0f);

            var pcSo = new SerializedObject(playerController);
            pcSo.FindProperty("rb").objectReferenceValue = rb;
            pcSo.FindProperty("col").objectReferenceValue = collider;
            pcSo.FindProperty("graphicsTransform").objectReferenceValue = graphics;
            if (animator != null)
                pcSo.FindProperty("animator").objectReferenceValue = animator;
            if (shadow != null)
                pcSo.FindProperty("shadowSprite").objectReferenceValue = shadow;
            ApplyControllerConfig(pcSo, config, audioSource, spawnPoint);
            pcSo.ApplyModifiedPropertiesWithoutUndo();

            var playerHealth = root.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                var phSo = new SerializedObject(playerHealth);
                phSo.FindProperty("controller").objectReferenceValue = playerController;
                phSo.FindProperty("hasInvincibilityFrames").boolValue = config.hasInvincibilityFrames;
                phSo.FindProperty("invincibilityDuration").floatValue = config.invincibilityDuration;
                phSo.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            gd.player.playerPrefab = prefab;
            EditorUtility.SetDirty(gd);
            ////Debug.Log($"[PlayerPrefabGen] Updated existing prefab: {prefabPath}");
            return prefab;
        }

        private static void ApplyControllerConfig(SerializedObject pcSo, PlayerConfig config, AudioSource audioSource, Transform spawnPoint)
        {
            pcSo.FindProperty("moveSpeed").floatValue = config.moveSpeed;
            pcSo.FindProperty("jumpForce").floatValue = config.jumpForce;
            pcSo.FindProperty("gravityScale").floatValue = config.gravityScale;
            pcSo.FindProperty("groundCheckDistance").floatValue = config.groundCheckDistance;
            pcSo.FindProperty("groundCheckWidth").floatValue = config.groundCheckWidth;
            pcSo.FindProperty("use2DFlip").boolValue = config.use2DFlip;
            pcSo.FindProperty("normalRotationSpeed").floatValue = config.normalRotationSpeed;
            pcSo.FindProperty("firingRotationSpeed").floatValue = config.firingRotationSpeed;
            pcSo.FindProperty("dropThroughForce").floatValue = config.dropThroughForce;
            pcSo.FindProperty("dropThroughThreshold").floatValue = config.dropThroughThreshold;
            pcSo.FindProperty("shadowMaxHeight").floatValue = config.shadowMaxHeight;
            pcSo.FindProperty("shadowMinScale").floatValue = config.shadowMinScale;
            pcSo.FindProperty("shadowRaycastDistance").floatValue = config.shadowRaycastDistance;
            pcSo.FindProperty("jumpDetectVelocityThreshold").floatValue = config.jumpDetectVelocityThreshold;
            pcSo.FindProperty("inputDeadzone").floatValue = config.inputDeadzone;
            pcSo.FindProperty("mobileDropThreshold").floatValue = config.mobileDropThreshold;
            pcSo.FindProperty("maxAirJumps").intValue = config.maxExtraJumps;
            pcSo.FindProperty("enableShooting").boolValue = config.enableShooting;
            pcSo.FindProperty("fireRate").floatValue = config.fireRate;
            pcSo.FindProperty("projectileSpeed").floatValue = config.projectileSpeed;
            pcSo.FindProperty("projectileLifetime").floatValue = config.projectileLifetime;
            pcSo.FindProperty("pitchVariation").floatValue = config.pitchVariation;

            if (audioSource != null)
                pcSo.FindProperty("audioSource").objectReferenceValue = audioSource;
            if (spawnPoint != null)
                pcSo.FindProperty("projectileSpawnPoint").objectReferenceValue = spawnPoint;

            WireSFXArray(pcSo, "hurtSounds", config.hurtSounds);
            WireSFXArray(pcSo, "deathSounds", config.deathSounds);
            if (config.jumpSound != null)
                pcSo.FindProperty("jumpSound").objectReferenceValue = config.jumpSound;
            if (config.fireSound != null)
                pcSo.FindProperty("fireSound").objectReferenceValue = config.fireSound;
            if (config.landSound != null)
                pcSo.FindProperty("landSound").objectReferenceValue = config.landSound;

            pcSo.FindProperty("groundLayer").intValue = LayerMask.GetMask("Platform", "Boundary");
            pcSo.FindProperty("platformLayer").intValue = LayerMask.GetMask("Platform");

            var inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            if (inputAsset != null)
            {
                WireInputAction(pcSo, "moveAction", inputAsset, "Character", "Move");
                WireInputAction(pcSo, "jumpAction", inputAsset, "Character", "Jump");
                WireInputAction(pcSo, "fireAction", inputAsset, "Character", "Fire");
                WireInputAction(pcSo, "lookAction", inputAsset, "Character", "Look");
            }
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

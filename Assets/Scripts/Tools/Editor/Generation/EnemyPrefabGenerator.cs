#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using SickOfIt.Combat.Enemies;
using SickOfIt.Combat.Health;
using SickOfIt.Data.Config;

namespace SickOfIt.Tools.Editor.Generation
{
    /// <summary>
    /// Generates a complete enemy prefab from a sprite sheet.
    /// 
    /// Given:
    ///   - A sprite sheet texture (already sliced in Multiple mode)
    ///   - An EnemyType (Chaser, Fighter, Sentinel, Shooter)
    ///   - A name
    /// 
    /// Produces:
    ///   - An Animator Controller with Idle loop
    ///   - An AnimationClip from the sprite sheet frames
    ///   - A Prefab with SpriteRenderer, Animator, Collider, Rigidbody, EnemyHealth, EnemyBase subclass
    ///   - (Optionally) an EnemyTemplate ScriptableObject
    /// 
    /// Usage via MCP:
    ///   execute_menu_item menuPath="Tools/Claw/Generate Enemy Prefab"
    /// Or call directly:
    ///   EnemyPrefabGenerator.Generate("SlimeEnemy", EnemyType.Chaser, "Assets/Art/Enemies/slime_sheet.png")
    /// </summary>
    public static class EnemyPrefabGenerator
    {
        private const string ENEMIES_ART_ROOT = "Assets/Art/Enemies";
        private const string ENEMIES_PREFAB_ROOT = "Assets/Prefabs/Enemies";
        private const string ENEMIES_ANIM_ROOT = "Assets/Animations/Enemies";

        /// <summary>
        /// Generate a complete enemy prefab from a sprite sheet.
        /// </summary>
        /// <param name="enemyName">Name of the enemy (e.g., "SlimeEnemy")</param>
        /// <param name="type">The EnemyType to use</param>
        /// <param name="spriteSheetPath">Path to the sprite sheet texture (must be imported as Multiple sprite mode)</param>
        /// <param name="fps">Animation playback speed</param>
        /// <param name="pixelsPerUnit">Sprite pixels per unit</param>
        /// <returns>The generated prefab, or null on failure</returns>
        public static GameObject Generate(string enemyName, EnemyType type, string spriteSheetPath, int fps = 8, int pixelsPerUnit = 100)
        {
            // Validate sprite sheet
            var sprites = AssetDatabase.LoadAllAssetsAtPath(spriteSheetPath);
            var spriteList = new System.Collections.Generic.List<Sprite>();
            foreach (var asset in sprites)
            {
                if (asset is Sprite sprite)
                    spriteList.Add(sprite);
            }

            if (spriteList.Count == 0)
            {
                Debug.LogError($"[EnemyPrefabGen] No sprites found in '{spriteSheetPath}'. Is it set to Multiple sprite mode?");
                return null;
            }

            // Sort sprites by name (frame_0, frame_1, etc.)
            spriteList.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

            Debug.Log($"[EnemyPrefabGen] Found {spriteList.Count} sprites in '{spriteSheetPath}'");

            // Ensure output folders
            GameBuilder.EnsureFolder(ENEMIES_PREFAB_ROOT);
            GameBuilder.EnsureFolder(ENEMIES_ANIM_ROOT);
            GameBuilder.EnsureFolder($"{ENEMIES_ANIM_ROOT}/{enemyName}");

            // Create animation clip
            var clip = CreateIdleClip(enemyName, spriteList, fps);

            // Create animator controller
            var controller = CreateAnimatorController(enemyName, clip);

            // Create prefab
            var prefab = CreatePrefab(enemyName, type, spriteList[0], controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[EnemyPrefabGen] ✅ Generated enemy '{enemyName}': " +
                $"Prefab={ENEMIES_PREFAB_ROOT}/{enemyName}.prefab, " +
                $"Anim={spriteList.Count} frames @ {fps}fps");

            return prefab;
        }

        /// <summary>
        /// Generate a prefab AND create an EnemyTemplate ScriptableObject for it.
        /// </summary>
        public static (GameObject prefab, EnemyTemplate template) GenerateWithTemplate(
            string enemyName, EnemyType type, string spriteSheetPath,
            int defaultHealth = 3, float defaultMoveSpeed = 2f, int contactDamage = 1,
            int fps = 8)
        {
            var prefab = Generate(enemyName, type, spriteSheetPath, fps);
            if (prefab == null) return (null, null);

            // Create EnemyTemplate SO
            GameBuilder.EnsureFolder("Assets/Data/EnemyTemplates");
            string templatePath = $"Assets/Data/EnemyTemplates/{enemyName}Template.asset";

            var template = ScriptableObject.CreateInstance<EnemyTemplate>();
            template.type = type;
            template.displayName = enemyName;
            template.prefab = prefab;
            template.defaultHealth = defaultHealth;
            template.defaultMoveSpeed = defaultMoveSpeed;
            template.contactDamage = contactDamage;

            AssetDatabase.CreateAsset(template, templatePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[EnemyPrefabGen] ✅ Created EnemyTemplate: {templatePath}");
            return (prefab, template);
        }

        private static AnimationClip CreateIdleClip(string enemyName, System.Collections.Generic.List<Sprite> sprites, int fps)
        {
            var clip = new AnimationClip();
            clip.name = $"{enemyName}_Idle";
            clip.frameRate = fps;

            // Set loop
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            // Create keyframes for sprite swap
            var keyframes = new ObjectReferenceKeyframe[sprites.Count];
            float frameDuration = 1f / fps;

            for (int i = 0; i < sprites.Count; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframe
                {
                    time = i * frameDuration,
                    value = sprites[i]
                };
            }

            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

            string clipPath = $"{ENEMIES_ANIM_ROOT}/{enemyName}/{enemyName}_Idle.anim";
            AssetDatabase.CreateAsset(clip, clipPath);

            return clip;
        }

        private static AnimatorController CreateAnimatorController(string enemyName, AnimationClip idleClip)
        {
            string controllerPath = $"{ENEMIES_ANIM_ROOT}/{enemyName}/{enemyName}.controller";
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            // Add Idle state
            var rootStateMachine = controller.layers[0].stateMachine;
            var idleState = rootStateMachine.AddState("Idle");
            idleState.motion = idleClip;
            rootStateMachine.defaultState = idleState;

            return controller;
        }

        private static GameObject CreatePrefab(string enemyName, EnemyType type, Sprite defaultSprite, AnimatorController controller)
        {
            // Create root GameObject
            var go = new GameObject(enemyName);

            // SpriteRenderer
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = defaultSprite;

            // Animator
            var animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            // Physics
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0;

            var collider = go.AddComponent<CircleCollider2D>();
            collider.radius = 0.4f;

            // Audio
            go.AddComponent<AudioSource>();

            // Health
            go.AddComponent<EnemyHealth>();

            // Enemy behavior based on type
            switch (type)
            {
                case EnemyType.Chaser:
                    go.AddComponent<EnemyChaser>();
                    break;
                case EnemyType.Fighter:
                    go.AddComponent<EnemyFighter>();
                    break;
                case EnemyType.Sentinel:
                    go.AddComponent<EnemySentinel>();
                    break;
                case EnemyType.Shooter:
                    go.AddComponent<EnemyShooter>();
                    break;
            }

            // Create health bar child (optional, can be configured later)
            var healthBarGo = new GameObject("HealthBar");
            healthBarGo.transform.SetParent(go.transform);
            healthBarGo.transform.localPosition = new Vector3(0, 0.6f, 0);

            // Save as prefab
            string prefabPath = $"{ENEMIES_PREFAB_ROOT}/{enemyName}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);

            // Clean up scene object
            Object.DestroyImmediate(go);

            return prefab;
        }
    }
}
#endif

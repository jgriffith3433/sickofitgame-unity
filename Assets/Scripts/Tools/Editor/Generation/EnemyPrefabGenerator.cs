#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Animations;
using SickOfIt.Combat.Effects;
using SickOfIt.Combat.Enemies;
using SickOfIt.Combat.Health;
using SickOfIt.Core.Rewind;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Tools.Editor.Generation
{
    /// <summary>
    /// Generates enemy prefabs from EnemyTemplate configs.
    /// 
    /// Called by ProjectGenerator during the prefab generation phase.
    /// Prefabs are generated to Assets/Prefabs/Combat/Enemy_{type}.prefab
    /// 
    /// Created prefab has:
    /// - SpriteRenderer, Animator
    /// - Rigidbody2D, CircleCollider2D
    /// - EnemyHealth (with healthBarContainer, healthBarColor, healthBarSize wired)
    /// - EnemyBase subclass (EnemyChaser, EnemyFighter, etc.)
    /// - LeftFirePoint/RightFirePoint children (Shooter/Sentinel only)
    /// - AnimationEventRelay (Fighter/Sentinel/Shooter)
    /// - GlitchEffect (wired to EnemyHealth)
    /// - WorldSpace Canvas health bar (wired to EnemyHealth)
    /// - Rewind components: RewindableTransform, RewindableRigidbody2D, RewindableGameObject, RewindableHealth, RewindableAnimator
    /// 
    /// All serialized fields are wired via SerializedObject.
    /// </summary>
    public static class EnemyPrefabGenerator
    {
        private const string ENEMIES_PREFAB_ROOT = "Assets/Prefabs/Combat";
        private const string ENEMIES_ANIM_ROOT = "Assets/Animations/Enemies";

        /// <summary>
        /// Delete all generated enemy prefabs.
        /// </summary>
        public static void DeleteAll()
        {
            ProjectGenerator.EnsureFolder(ENEMIES_PREFAB_ROOT);

            // Delete all Enemy_*.prefab files
            var guids = AssetDatabase.FindAssets("Enemy_ t:Prefab", new[] { ENEMIES_PREFAB_ROOT });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("Enemy_"))
                {
                    AssetDatabase.DeleteAsset(path);
                    Debug.Log($"[EnemyPrefabGen] Deleted: {path}");
                }
            }
        }

        /// <summary>
        /// Generate all enemy prefabs from GameDefinition.enemyTemplates.
        /// </summary>
        public static void GenerateAll(GameDefinition gd)
        {
            if (gd == null || gd.enemyTemplates == null || gd.enemyTemplates.Count == 0)
            {
                Debug.LogWarning("[EnemyPrefabGen] No enemy templates in GameDefinition");
                return;
            }

            ProjectGenerator.EnsureFolder(ENEMIES_PREFAB_ROOT);
            ProjectGenerator.EnsureFolder(ENEMIES_ANIM_ROOT);

            foreach (var template in gd.enemyTemplates)
            {
                if (template == null)
                {
                    Debug.LogWarning("[EnemyPrefabGen] Skipping null template");
                    continue;
                }

                GenerateFromTemplate(template);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[EnemyPrefabGen] Generated {gd.enemyTemplates.Count} enemy prefabs");
        }

        /// <summary>
        /// Generate a single enemy prefab from an EnemyTemplate.
        /// Updates the template's prefab reference.
        /// </summary>
        public static GameObject GenerateFromTemplate(EnemyTemplate template)
        {
            if (template == null)
            {
                Debug.LogError("[EnemyPrefabGen] Null template provided");
                return null;
            }

            string enemyName = $"Enemy_{template.type}";
            string animFolder = $"{ENEMIES_ANIM_ROOT}/{enemyName}";

            // Clean old animation assets before regenerating
            AnimationGenerator.DeleteFolder(animFolder);
            ProjectGenerator.EnsureFolder(animFolder);

            // ─── Auto-populate animation config if missing ─────────────
            if (template.animationConfig == null || template.animationConfig.states.Count == 0)
            {
                template.animationConfig = AnimationConfigPopulator.CreateEnemyAnimConfig(template);
                if (template.animationConfig != null)
                {
                    EditorUtility.SetDirty(template);
                    Debug.Log($"[EnemyPrefabGen] Auto-populated animation config for {enemyName}");
                }
            }

            // ─── Animation generation via AnimationGenerator ─────────────
            RuntimeAnimatorController animController = null;

            if (template.animationConfig != null && template.animationConfig.states.Count > 0)
            {
                // Full multi-state animation from config
                var animResult = AnimationGenerator.Generate(
                    template.animationConfig,
                    enemyName,
                    animFolder,
                    template.spriteSheet,
                    template.animationFps);

                if (animResult != null)
                    animController = animResult.controller;
            }
            else if (template.spriteSheet != null)
            {
                // Simple idle fallback
                var animResult = AnimationGenerator.GenerateSimpleIdle(
                    enemyName, animFolder, template.spriteSheet, template.animationFps);

                if (animResult != null)
                    animController = animResult.controller;
            }

            // Get default sprite (first sprite from the primary sprite sheet)
            Sprite defaultSprite = null;
            if (template.spriteSheet != null)
            {
                var sprites = AssetDatabase.LoadAllAssetsAtPath(
                    AssetDatabase.GetAssetPath(template.spriteSheet));
                foreach (var asset in sprites)
                {
                    if (asset is Sprite sprite)
                    {
                        defaultSprite = sprite;
                        break;
                    }
                }
            }

            // Create prefab (pass template so all config values are used)
            var prefab = CreatePrefab(enemyName, template, defaultSprite, animController);

            // Update template's prefab reference
            template.prefab = prefab;
            EditorUtility.SetDirty(template);

            return prefab;
        }

        private static GameObject CreatePrefab(string enemyName, EnemyTemplate template, Sprite defaultSprite, RuntimeAnimatorController controller)
        {
            var type = template.type;

            // Create root GameObject
            var go = new GameObject(enemyName);

            // SpriteRenderer
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = defaultSprite;

            // Animator
            Animator animator = null;
            if (controller != null)
            {
                animator = go.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
            }

            // Physics — read from template config
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = template.gravityScale;
            rb.constraints = template.constraints;

            var collider = go.AddComponent<CircleCollider2D>();
            collider.radius = template.colliderRadius > 0 ? template.colliderRadius : 0.4f;

            // Audio
            var audioSource = go.AddComponent<AudioSource>();

            // Health
            var enemyHealth = go.AddComponent<EnemyHealth>();

            // Wire EnemyHealth config from template
            var ehSo = new SerializedObject(enemyHealth);
            var invProp = ehSo.FindProperty("hasInvincibilityFrames");
            if (invProp != null) invProp.boolValue = template.hasInvincibilityFrames;
            var invTimerProp = ehSo.FindProperty("invincibilityDuration");
            if (invTimerProp != null) invTimerProp.floatValue = template.invincibilityDuration;
            ehSo.ApplyModifiedPropertiesWithoutUndo();

            // ─── Fire points for Shooter/Sentinel ────────────────────────
            Transform leftFirePoint = null;
            Transform rightFirePoint = null;
            if (type == EnemyType.Shooter || type == EnemyType.Sentinel)
            {
                var leftFP = new GameObject("LeftFirePoint");
                leftFP.transform.SetParent(go.transform);
                leftFP.transform.localPosition = new Vector3(
                    -Mathf.Abs(template.firePointOffset.x),
                    template.firePointOffset.y,
                    0f);
                leftFirePoint = leftFP.transform;

                var rightFP = new GameObject("RightFirePoint");
                rightFP.transform.SetParent(go.transform);
                rightFP.transform.localPosition = new Vector3(
                    Mathf.Abs(template.firePointOffset.x),
                    template.firePointOffset.y,
                    0f);
                rightFirePoint = rightFP.transform;
            }

            // Enemy behavior based on type
            EnemyBase enemyBase = null;
            switch (type)
            {
                case EnemyType.Chaser:
                    enemyBase = go.AddComponent<EnemyChaser>();
                    break;
                case EnemyType.Fighter:
                    enemyBase = go.AddComponent<EnemyFighter>();
                    break;
                case EnemyType.Sentinel:
                    enemyBase = go.AddComponent<EnemySentinel>();
                    break;
                case EnemyType.Shooter:
                    enemyBase = go.AddComponent<EnemyShooter>();
                    break;
            }

            // Wire all serialized fields on EnemyBase via SerializedObject
            if (enemyBase != null)
            {
                var so = new SerializedObject(enemyBase);
                so.FindProperty("spriteRenderer").objectReferenceValue = sr;
                so.FindProperty("rb").objectReferenceValue = rb;
                so.FindProperty("enemyHealth").objectReferenceValue = enemyHealth;
                so.FindProperty("audioSource").objectReferenceValue = audioSource;

                // Wire animator for types that have one
                var animProp = so.FindProperty("animator");
                if (animProp != null && animator != null)
                    animProp.objectReferenceValue = animator;

                // Wire fire points for Shooter
                if (type == EnemyType.Shooter)
                {
                    var lfpProp = so.FindProperty("leftFirePoint");
                    if (lfpProp != null) lfpProp.objectReferenceValue = leftFirePoint;
                    var rfpProp = so.FindProperty("rightFirePoint");
                    if (rfpProp != null) rfpProp.objectReferenceValue = rightFirePoint;
                }

                // Wire fire points for Sentinel
                if (type == EnemyType.Sentinel)
                {
                    var lfpProp = so.FindProperty("leftProjectileSpawn");
                    if (lfpProp != null) lfpProp.objectReferenceValue = leftFirePoint;
                    var rfpProp = so.FindProperty("rightProjectileSpawn");
                    if (rfpProp != null) rfpProp.objectReferenceValue = rightFirePoint;
                }

                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // ─── Rewind components ────────────────────────────────────────
            go.AddComponent<RewindableTransform>();
            go.AddComponent<RewindableRigidbody2D>();
            go.AddComponent<RewindableGameObject>();

            var rewindableHealth = go.AddComponent<RewindableHealth>();
            var rhSo = new SerializedObject(rewindableHealth);
            rhSo.FindProperty("enemyHealth").objectReferenceValue = enemyHealth;
            rhSo.ApplyModifiedPropertiesWithoutUndo();

            // RewindableAnimator (if animator exists)
            if (animator != null)
            {
                var rewindableAnimator = go.AddComponent<RewindableAnimator>();
                var raSo = new SerializedObject(rewindableAnimator);
                var animRefProp = raSo.FindProperty("animator");
                if (animRefProp != null)
                    animRefProp.objectReferenceValue = animator;
                raSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ─── GlitchEffect ──────────────────────────────────────────
            var glitchEffect = go.AddComponent<GlitchEffect>();
            var geSo = new SerializedObject(glitchEffect);
            geSo.FindProperty("enemyHealth").objectReferenceValue = enemyHealth;
            geSo.ApplyModifiedPropertiesWithoutUndo();

            // ─── AnimationEventRelay (for types that use animation-driven attacks) ──
            if (type == EnemyType.Fighter || type == EnemyType.Sentinel || type == EnemyType.Shooter)
            {
                var relay = go.AddComponent<AnimationEventRelay>();
                var relaySo = new SerializedObject(relay);
                // Wire the appropriate enemy subclass
                if (type == EnemyType.Fighter)
                {
                    var prop = relaySo.FindProperty("_fighter");
                    if (prop != null) prop.objectReferenceValue = enemyBase as EnemyFighter;
                }
                else if (type == EnemyType.Sentinel)
                {
                    var prop = relaySo.FindProperty("_sentinel");
                    if (prop != null) prop.objectReferenceValue = enemyBase as EnemySentinel;
                }
                else if (type == EnemyType.Shooter)
                {
                    var prop = relaySo.FindProperty("_shooter");
                    if (prop != null) prop.objectReferenceValue = enemyBase as EnemyShooter;
                }
                relaySo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ─── Health bar (WorldSpace Canvas + Images) ──────────────────
            var healthBarGo = new GameObject("HealthBar");
            healthBarGo.transform.SetParent(go.transform);
            healthBarGo.transform.localPosition = new Vector3(
                template.healthBarOffset.x,
                template.healthBarOffset.y,
                0f);

            // Add a WorldSpace Canvas so UI Images render in-world
            var canvas = healthBarGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 10;

            var canvasRect = healthBarGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = template.healthBarSize;
            canvasRect.localScale = new Vector3(0.01f, 0.01f, 1f); // world-space scaling

            // Wire healthBarContainer on EnemyHealth
            var ehSo2 = new SerializedObject(enemyHealth);
            ehSo2.FindProperty("healthBarContainer").objectReferenceValue = healthBarGo.transform;
            ehSo2.FindProperty("healthBarColor").colorValue = template.healthBarColor;
            ehSo2.FindProperty("healthBarSize").vector2Value = template.healthBarSize;
            ehSo2.ApplyModifiedPropertiesWithoutUndo();

            // Save as prefab
            string prefabPath = $"{ENEMIES_PREFAB_ROOT}/{enemyName}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);

            // Clean up scene object
            Object.DestroyImmediate(go);

            Debug.Log($"[EnemyPrefabGen] Created: {prefabPath}");

            return prefab;
        }
    }
}
#endif

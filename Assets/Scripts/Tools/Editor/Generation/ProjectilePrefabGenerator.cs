#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Combat.Damage;

namespace SickOfIt.Tools.Editor.Generation
{
    /// <summary>
    /// Generates projectile prefabs from ProjectileConfig.
    /// 
    /// Called by ProjectGenerator during the prefab generation phase.
    /// Prefabs are generated to Assets/Prefabs/Combat/Projectile_{id}.prefab
    /// 
    /// Created prefab has:
    /// - SpriteRenderer
    /// - Rigidbody2D (Kinematic)
    /// - CircleCollider2D (trigger)
    /// - DamageDealer
    /// 
    /// Includes ProjectileLifetime and rewind components so shots scrub instead of vanishing.
    /// </summary>
    public static class ProjectilePrefabGenerator
    {
        private const string OUTPUT_PATH = "Assets/Prefabs/Combat";

        /// <summary>
        /// Delete all generated projectile prefabs.
        /// </summary>
        public static void DeleteAll()
        {
            ProjectGenerator.EnsureFolder(OUTPUT_PATH);

            // Find and delete all Projectile_*.prefab files
            var guids = AssetDatabase.FindAssets("Projectile_ t:Prefab", new[] { OUTPUT_PATH });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("Projectile_"))
                {
                    AssetDatabase.DeleteAsset(path);
                    ////Debug.Log($"[ProjectilePrefabGen] Deleted: {path}");
                }
            }
        }

        /// <summary>
        /// Generate all projectile prefabs from GameDefinition.projectileConfigs.
        /// </summary>
        public static void GenerateAll(GameDefinition gd)
        {
            if (gd == null || gd.projectileConfigs == null || gd.projectileConfigs.Count == 0)
            {
                Debug.LogWarning("[ProjectilePrefabGen] No projectile configs in GameDefinition. Creating defaults.");
                GenerateDefaults();
                return;
            }

            ProjectGenerator.EnsureFolder(OUTPUT_PATH);

            foreach (var config in gd.projectileConfigs)
            {
                if (config == null || string.IsNullOrEmpty(config.id))
                {
                    Debug.LogWarning("[ProjectilePrefabGen] Skipping null or unnamed projectile config");
                    continue;
                }

                Generate(config);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            ////Debug.Log($"[ProjectilePrefabGen] Generated {gd.projectileConfigs.Count} projectile prefabs");
        }

        /// <summary>
        /// Generate default projectile prefabs if no configs exist.
        /// </summary>
        public static void GenerateDefaults()
        {
            ProjectGenerator.EnsureFolder(OUTPUT_PATH);

            // Player projectile
            GenerateDefault("Player", Color.cyan, 1, 10f, 5f);

            // Enemy projectile
            GenerateDefault("Enemy", Color.red, 1, 5f, 5f);

            // Laser projectile
            GenerateDefault("Laser", Color.yellow, 1, 15f, 3f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            ////Debug.Log("[ProjectilePrefabGen] Generated default projectile prefabs");
        }

        private static void GenerateDefault(string id, Color color, int damage, float speed, float lifetime)
        {
            var config = new ProjectileConfig
            {
                id = id,
                displayName = $"{id} Projectile",
                sprite = null,  // Will use fallback
                scale = 1f,
                color = color,
                colliderRadius = 1f,
                isTrigger = true,
                damage = damage,
                speed = speed,
                lifetime = lifetime,
                layerName = "Default",
                tag = "Untagged"
            };

            Generate(config);
        }

        /// <summary>
        /// Generate a single projectile prefab from config.
        /// </summary>
        public static GameObject Generate(ProjectileConfig config)
        {
            string prefabName = $"Projectile_{config.id}";
            string prefabPath = $"{OUTPUT_PATH}/{prefabName}.prefab";

            // Create root GameObject
            var go = new GameObject(prefabName);

            // SpriteRenderer
            var sr = go.AddComponent<SpriteRenderer>();
            if (config.sprite != null)
            {
                sr.sprite = config.sprite;
            }
            else
            {
                // Create a simple circle sprite as fallback
                sr.sprite = CreateFallbackSprite();
            }
            
            // Default color to white if not set (alpha 0 means invisible)
            Color renderColor = config.color.a > 0 ? config.color : Color.white;
            sr.color = renderColor;
            string sortingLayer = string.IsNullOrEmpty(config.sortingLayer)
                ? SpriteLayer.Player.ToString()
                : config.sortingLayer;
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = config.sortingOrder > 0 ? config.sortingOrder : SpriteLayers.Projectile;
            
            // Default scale to 0.333 if 0 or less (compensate for large sprites)
            float scale = config.scale > 0 ? config.scale : 0.15f;
            go.transform.localScale = Vector3.one * scale;

            // ─── Animation (from config) ──────────────────────────────
            string animFolder = $"Assets/Animations/Projectiles/{prefabName}";
            var animCtrl = GeneratorHelper.GenerateAnimation(
                config.animationConfig, prefabName, animFolder,
                config.spriteSheet, config.animationFps);

            Animator animator = null;
            if (animCtrl != null)
            {
                animator = go.AddComponent<Animator>();
                GeneratorHelper.AssignAnimatorController(animator, animCtrl);
            }

            // Rigidbody2D
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;

            // CircleCollider2D
            var collider = go.AddComponent<CircleCollider2D>();
            collider.radius = config.colliderRadius > 0 ? config.colliderRadius : 1f;
            collider.isTrigger = true; // Always trigger for projectiles

            // ProjectileLifetime (pre-attached so ObjectPool doesn't need AddComponent)
            var projectileLifetime = go.AddComponent<SickOfIt.Core.Rewind.ProjectileLifetime>();

            // Wire lifetime from config
            var plSo = new SerializedObject(projectileLifetime);
            var lifetimeProp = plSo.FindProperty("lifetime");
            if (lifetimeProp != null)
                lifetimeProp.floatValue = config.lifetime;
            plSo.ApplyModifiedPropertiesWithoutUndo();

            // DamageDealer
            var damageDealer = go.AddComponent<DamageDealer>();
            
            // Wire DamageDealer via SerializedObject
            var ddSo = new SerializedObject(damageDealer);
            var damageProp = ddSo.FindProperty("damageAmount");
            if (damageProp != null)
                damageProp.intValue = config.damage;

            // Set targetTags based on projectile type
            var tagsProp = ddSo.FindProperty("targetTags");
            if (tagsProp != null)
            {
                if (config.id == "Player")
                {
                    tagsProp.arraySize = 1;
                    tagsProp.GetArrayElementAtIndex(0).stringValue = "Enemy";
                }
                else // Enemy, Laser
                {
                    tagsProp.arraySize = 1;
                    tagsProp.GetArrayElementAtIndex(0).stringValue = "Player";
                }
            }
            ddSo.ApplyModifiedPropertiesWithoutUndo();

            // Wire SFX from config
            if (config.hitSound != null)
            {
                var ddSo2 = new SerializedObject(damageDealer);
                ddSo2.FindProperty("hitSound").objectReferenceValue = config.hitSound;
                ddSo2.ApplyModifiedPropertiesWithoutUndo();
            }

            // Shots scrub with rewind instead of vanishing when time runs backward.
            GeneratorHelper.AddRewindComponents(go, rb, animator);

            // Set layer
            GeneratorHelper.SetLayer(go, config.layerName);

            // Set tag
            GeneratorHelper.SetTag(go, config.tag);

            // Save as prefab
            var prefab = GeneratorHelper.SavePrefab(go, prefabPath);

            ////Debug.Log($"[ProjectilePrefabGen] Created: {prefabPath}");

            return prefab;
        }

        private static Sprite CreateFallbackSprite()
        {
            // Try to load an existing sprite asset as fallback
            var existingSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/projectile.png");
            if (existingSprite != null)
                return existingSprite;

            // Return null if no sprite available — renderer will show pink
            return null;
        }
    }
}
#endif

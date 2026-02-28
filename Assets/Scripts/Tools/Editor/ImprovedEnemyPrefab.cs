#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Linq;
using SickOfIt.Tools.Editor;

namespace SickOfIt.Tools.Editor
{
    public class ImprovedEnemyPrefab : EditorWindow
    {
        [MenuItem("Tools/OpenClaw/Create Improved Slime Enemy")]
        public static void CreateImprovedSlimeEnemy()
        {
            Debug.Log("🎯 Creating improved slime enemy with proper structure...");
            
            try
            {
                // Create root GameObject
                var root = new GameObject("SlimeEnemy_Improved");
                root.tag = "Enemy";
                
                // Set layer to Enemy (assuming it exists)
                int enemyLayer = LayerMask.NameToLayer("Enemy");
                if (enemyLayer != -1)
                {
                    root.layer = enemyLayer;
                }
                else
                {
                    Debug.LogWarning("Enemy layer not found - using Default");
                }
                
                // Add root components
                var rb = root.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                
                var collider = root.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(3f, 3f); // Larger collider based on feedback
                
                // Create Sprite child
                var spriteChild = new GameObject("Sprite");
                spriteChild.transform.SetParent(root.transform);
                spriteChild.transform.localPosition = Vector3.zero;
                
                var spriteRenderer = spriteChild.AddComponent<SpriteRenderer>();
                spriteRenderer.sortingLayerName = "Default";
                spriteRenderer.sortingOrder = 1;
                
                // Load and assign first sprite
                string spriteSheetPath = "Assets/Art/Sprites/Enemies/slime_idle.png";
                var sprites = AssetDatabase.LoadAllAssetsAtPath(spriteSheetPath)
                    .OfType<Sprite>()
                    .OrderBy(s => s.name)
                    .ToArray();
                    
                if (sprites.Length > 0)
                {
                    spriteRenderer.sprite = sprites[0];
                    Debug.Log($"✅ Assigned sprite: {sprites[0].name}");
                }
                
                // Add Animator to sprite child
                var animator = spriteChild.AddComponent<Animator>();
                string controllerPath = "Assets/Animations/SlimeController.controller";
                var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
                if (controller != null)
                {
                    animator.runtimeAnimatorController = controller;
                    Debug.Log("✅ Animator controller assigned");
                }
                
                // Create Shadow child
                var shadowChild = new GameObject("Shadow");
                shadowChild.transform.SetParent(root.transform);
                shadowChild.transform.localPosition = new Vector3(0, -1.5f, 0);
                
                var shadowRenderer = shadowChild.AddComponent<SpriteRenderer>();
                shadowRenderer.color = new Color(0, 0, 0, 0.3f); // Semi-transparent black
                shadowRenderer.sortingOrder = -1;
                
                // Try to load a shadow sprite (create simple oval if none exists)
                // For now, use the slime sprite but make it dark and stretched
                if (sprites.Length > 0)
                {
                    shadowRenderer.sprite = sprites[0];
                    shadowChild.transform.localScale = new Vector3(1.2f, 0.3f, 1f); // Oval shadow
                }
                
                // Create HealthBarContainer child
                var healthBarContainer = new GameObject("HealthBarContainer");
                healthBarContainer.transform.SetParent(root.transform);
                healthBarContainer.transform.localPosition = new Vector3(0, 2f, 0);
                
                // Add Canvas for health bar UI
                var canvas = healthBarContainer.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = Camera.main;
                
                var canvasScaler = healthBarContainer.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasScaler.dynamicPixelsPerUnit = 100f;
                
                // Try to add enemy script component
                // Look for EnemyShooter or similar in the project
                System.Type enemyScriptType = null;
                string[] possibleTypes = { "EnemyShooter", "EnemyFighter", "EnemyChaser", "Enemy" };
                
                foreach (string typeName in possibleTypes)
                {
                    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        enemyScriptType = asm.GetType($"SickOfIt.Combat.{typeName}") ?? 
                                         asm.GetType($"SickOfIt.Combat.Enemies.{typeName}") ??
                                         asm.GetType(typeName);
                        if (enemyScriptType != null) break;
                    }
                    if (enemyScriptType != null) break;
                }
                
                if (enemyScriptType != null && typeof(MonoBehaviour).IsAssignableFrom(enemyScriptType))
                {
                    var enemyScript = root.AddComponent(enemyScriptType);
                    Debug.Log($"✅ Added enemy script: {enemyScriptType.Name}");
                    
                    // Try to set PlayerLayer field if it exists
                    var playerLayerField = enemyScriptType.GetField("PlayerLayer", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (playerLayerField != null && playerLayerField.FieldType == typeof(LayerMask))
                    {
                        int playerLayer = LayerMask.NameToLayer("Player");
                        if (playerLayer != -1)
                        {
                            LayerMask playerLayerMask = 1 << playerLayer;
                            playerLayerField.SetValue(enemyScript, playerLayerMask);
                            Debug.Log("✅ Set PlayerLayer field");
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("Could not find enemy script - prefab created without combat component");
                }
                
                // Save as prefab
                string prefabPath = "Assets/Prefabs/Enemies/SlimeEnemy_Improved.prefab";
                prefabPath = AssetDatabase.GenerateUniqueAssetPath(prefabPath);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Object.DestroyImmediate(root);
                
                Debug.Log($"🎉 IMPROVED SLIME ENEMY CREATED: {prefabPath}");
                Debug.Log("Structure: Root(Enemy tag/layer) → Sprite(SpriteRenderer+Animator) + Shadow + HealthBarContainer");
                
                // Focus on the created prefab
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab != null)
                {
                    EditorGUIUtility.PingObject(prefab);
                    Selection.activeObject = prefab;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Failed to create improved enemy: {e.Message}");
                Debug.LogError($"Stack trace: {e.StackTrace}");
            }
        }
    }
}
#endif
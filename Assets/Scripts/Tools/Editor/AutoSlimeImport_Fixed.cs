#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using SickOfIt.Tools.Editor;

namespace SickOfIt.Tools.Editor
{
    public class AutoSlimeImport_Fixed : EditorWindow
    {
        [MenuItem("Tools/OpenClaw/Import Slime Character (Fixed)")]
        public static void ExecuteFixedSlimePipeline()
        {
            Debug.Log("🎮 AUTO-EXECUTING FIXED SLIME PIPELINE!");
            
            try 
            {
                // Step 1: Import sprite sheet with grid slicing
                Debug.Log("Step 1: Importing sprite sheet...");
                ClawCharacterPipeline.ImportSpriteSheet(
                    "Assets/Art/Sprites/Enemies/slime_idle.png", 
                    columns: 4, 
                    rows: 2,
                    pixelsPerUnit: 100f
                );
                Debug.Log("✅ Sprite sheet imported with 4x2 grid");
                
                // Step 2: Create animation clip  
                Debug.Log("Step 2: Creating animation clip...");
                string animPath = ClawCharacterPipeline.CreateAnimClip(
                    "Assets/Art/Sprites/Enemies/slime_idle.png",
                    "SlimeIdle_Fixed",
                    fps: 12,
                    loop: true,
                    outputDir: "Assets/Animations"
                );
                Debug.Log($"✅ Animation clip created: {animPath}");
                
                // Step 3: Create animator controller
                Debug.Log("Step 3: Creating animator controller...");
                var clipPaths = new Dictionary<string, string>
                {
                    { "Idle", animPath }
                };
                string controllerPath = ClawCharacterPipeline.CreateAnimatorController(
                    "SlimeController_Fixed",
                    clipPaths,
                    "Idle",
                    "Assets/Animations"
                );
                Debug.Log($"✅ Animator controller created: {controllerPath}");
                
                // Step 4: Create improved enemy prefab (now with proper structure)
                Debug.Log("Step 4: Creating improved enemy prefab...");
                string prefabPath = ClawCharacterPipeline.CreateEnemyPrefab(
                    "SlimeEnemy_Fixed",
                    controllerPath,
                    "Assets/Art/Sprites/Enemies/slime_idle.png",
                    "shooter", // Default to shooter type
                    "Assets/Prefabs/Enemies/"
                );
                Debug.Log($"✅ Improved enemy prefab created: {prefabPath}");
                
                Debug.Log("🎉 FIXED SLIME PIPELINE COMPLETE!");
                Debug.Log("✅ Proper structure: Root(Enemy tag/layer) → Sprite + Shadow + HealthBarContainer");
                Debug.Log("✅ Default sprite assigned to SpriteRenderer");
                Debug.Log("✅ Larger BoxCollider2D (3x3)");
                Debug.Log("✅ PlayerLayer field automatically configured");
                
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
                Debug.LogError($"❌ Fixed pipeline failed: {e.Message}");
                Debug.LogError($"Stack trace: {e.StackTrace}");
            }
        }
    }
}
#endif
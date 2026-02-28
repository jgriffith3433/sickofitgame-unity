#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using SickOfIt.Tools.Editor;

namespace SickOfIt.Tools.Editor
{
    public class AutoSlimeImport : EditorWindow
    {
        [MenuItem("Tools/OpenClaw/Import Slime Character Now")]
        public static void ExecuteSlimePipeline()
        {
            Debug.Log("🎮 AUTO-EXECUTING SLIME PIPELINE via MCP!");
            
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
                    "SlimeIdle",
                    fps: 12,
                    loop: true,
                    outputDir: "Assets/Animations"
                );
                Debug.Log($"✅ Animation clip created: {animPath}");
                
                // Step 3: Create animator controller
                Debug.Log("Step 3: Creating animator controller...");
                var clipPaths = new System.Collections.Generic.Dictionary<string, string>
                {
                    { "Idle", animPath }
                };
                string controllerPath = ClawCharacterPipeline.CreateAnimatorController(
                    "SlimeController",
                    clipPaths,
                    "Idle",
                    "Assets/Animations"
                );
                Debug.Log($"✅ Animator controller created: {controllerPath}");
                
                // Step 4: Create enemy prefab
                Debug.Log("Step 4: Creating enemy prefab...");
                string prefabPath = ClawCharacterPipeline.CreateEnemyPrefab(
                    "SlimeEnemy",
                    controllerPath,
                    "Assets/Prefabs/Enemies/"
                );
                Debug.Log($"✅ Enemy prefab created: {prefabPath}");
                
                Debug.Log("🎉 SLIME PIPELINE COMPLETE! Check Project window for new assets!");
                
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
                Debug.LogError($"❌ Pipeline failed: {e.Message}");
                Debug.LogError($"Stack trace: {e.StackTrace}");
            }
        }
    }
}
#endif
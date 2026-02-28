#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace SickOfIt.Tools.Editor
{
    /// <summary>
    /// Editor utility methods for automated character creation pipeline.
    /// Called via Unity MCP execute_menu_item or directly from editor scripts.
    /// 
    /// Workflow:
    /// 1. ImportSpriteSheet - configures texture as Multiple sprite with grid slicing
    /// 2. CreateAnimClip - creates .anim from sprite sheet sub-sprites
    /// 3. CreateAnimatorController - wires up states with transitions
    /// 4. CreateEnemyPrefab - creates a prefab with SpriteRenderer + Animator + enemy script
    /// </summary>
    public static class ClawCharacterPipeline
    {
        // =====================================================================
        // 1. Import Sprite Sheet
        // =====================================================================

        /// <summary>
        /// Configure a texture as a sprite sheet with grid slicing.
        /// </summary>
        /// <param name="assetPath">Unity asset path (e.g., "Assets/Images/BlobEnemy_idle.png")</param>
        /// <param name="columns">Number of columns in the grid</param>
        /// <param name="rows">Number of rows in the grid</param>
        /// <param name="pixelsPerUnit">Pixels per unit (default 100)</param>
        public static void ImportSpriteSheet(string assetPath, int columns, int rows, float pixelsPerUnit = 100f)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[ClawPipeline] No texture importer at: {assetPath}");
                return;
            }

            // Set basic import settings
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point; // Pixel art style
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            // Read texture dimensions
            importer.GetWidthAndHeight(out int texWidth, out int texHeight);

            int cellWidth = texWidth / columns;
            int cellHeight = texHeight / rows;
            int totalCells = columns * rows;

            // Create sprite metadata for grid slicing
            var spriteMetaData = new List<SpriteMetaData>();
            string baseName = Path.GetFileNameWithoutExtension(assetPath);

            int index = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    var meta = new SpriteMetaData
                    {
                        name = $"{baseName}_{index}",
                        rect = new Rect(
                            c * cellWidth,
                            texHeight - (r + 1) * cellHeight, // Unity Y is bottom-up
                            cellWidth,
                            cellHeight
                        ),
                        alignment = (int)SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f)
                    };
                    spriteMetaData.Add(meta);
                    index++;
                }
            }

            importer.spritesheet = spriteMetaData.ToArray();
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();

            Debug.Log($"[ClawPipeline] Imported sprite sheet: {assetPath} ({columns}x{rows}, {totalCells} sprites, {cellWidth}x{cellHeight}px cells)");
        }

        // =====================================================================
        // 2. Create Animation Clip
        // =====================================================================

        /// <summary>
        /// Create an animation clip from a sprite sheet's sub-sprites.
        /// </summary>
        /// <param name="spriteSheetPath">Path to the sprite sheet (e.g., "Assets/Images/BlobEnemy_idle.png")</param>
        /// <param name="clipName">Name for the .anim file</param>
        /// <param name="fps">Frames per second (default 12)</param>
        /// <param name="loop">Whether the animation loops</param>
        /// <param name="outputDir">Output directory for .anim (default: Assets/Animations/)</param>
        /// <returns>Path to created .anim asset</returns>
        public static string CreateAnimClip(string spriteSheetPath, string clipName, int fps = 12, bool loop = true, string outputDir = "Assets/Animations")
        {
            // Load all sub-sprites from the sheet
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(spriteSheetPath);
            var sprites = allAssets
                .OfType<Sprite>()
                .OrderBy(s => s.name, System.StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (sprites.Count == 0)
            {
                Debug.LogError($"[ClawPipeline] No sprites found in: {spriteSheetPath}. Did you run ImportSpriteSheet first?");
                return null;
            }

            // Create the animation clip
            var clip = new AnimationClip();
            clip.frameRate = fps;

            // Create keyframes for sprite swap
            var keyframes = new ObjectReferenceKeyframe[sprites.Count];
            for (int i = 0; i < sprites.Count; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframe
                {
                    time = i / (float)fps,
                    value = sprites[i]
                };
            }

            // Set the sprite renderer binding
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

            // Set loop
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            // Save the clip
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
                AssetDatabase.Refresh();
            }

            string clipPath = $"{outputDir}/{clipName}.anim";
            // Avoid overwriting
            clipPath = AssetDatabase.GenerateUniqueAssetPath(clipPath);

            AssetDatabase.CreateAsset(clip, clipPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[ClawPipeline] Created animation clip: {clipPath} ({sprites.Count} frames at {fps} FPS, loop={loop})");
            return clipPath;
        }

        // =====================================================================
        // 3. Create Animator Controller
        // =====================================================================

        /// <summary>
        /// Create an Animator Controller with states for each animation clip.
        /// </summary>
        /// <param name="controllerName">Name for the .controller file</param>
        /// <param name="clipPaths">Dictionary of state name → .anim asset path</param>
        /// <param name="defaultState">Which state is the default (usually "Idle")</param>
        /// <param name="outputDir">Output directory (default: Assets/Animations/)</param>
        /// <returns>Path to created .controller asset</returns>
        public static string CreateAnimatorController(
            string controllerName,
            Dictionary<string, string> clipPaths,
            string defaultState = "Idle",
            string outputDir = "Assets/Animations")
        {
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
                AssetDatabase.Refresh();
            }

            string controllerPath = $"{outputDir}/{controllerName}.controller";
            controllerPath = AssetDatabase.GenerateUniqueAssetPath(controllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var rootStateMachine = controller.layers[0].stateMachine;

            AnimatorState defaultAnimState = null;

            foreach (var kvp in clipPaths)
            {
                string stateName = kvp.Key;
                string clipPath = kvp.Value;

                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null)
                {
                    Debug.LogWarning($"[ClawPipeline] Clip not found: {clipPath}");
                    continue;
                }

                var state = rootStateMachine.AddState(stateName);
                state.motion = clip;

                if (stateName == defaultState)
                {
                    defaultAnimState = state;
                }

                // Add a bool parameter for this state (e.g., "IsAttacking", "IsWalking")
                if (stateName != defaultState)
                {
                    string paramName = $"Is{stateName}";
                    controller.AddParameter(paramName, AnimatorControllerParameterType.Bool);
                }
            }

            // Set default state
            if (defaultAnimState != null)
            {
                rootStateMachine.defaultState = defaultAnimState;
            }

            // Create transitions: default ↔ each other state
            var states = rootStateMachine.states;
            var defaultStateEntry = states.FirstOrDefault(s => s.state == defaultAnimState);

            foreach (var stateEntry in states)
            {
                if (stateEntry.state == defaultAnimState) continue;

                string paramName = $"Is{stateEntry.state.name}";

                // Default → This state (when param is true)
                if (defaultAnimState != null)
                {
                    var toTransition = defaultAnimState.AddTransition(stateEntry.state);
                    toTransition.AddCondition(AnimatorConditionMode.If, 0, paramName);
                    toTransition.duration = 0;
                    toTransition.hasExitTime = false;

                    // This state → Default (when param is false)
                    var backTransition = stateEntry.state.AddTransition(defaultAnimState);
                    backTransition.AddCondition(AnimatorConditionMode.IfNot, 0, paramName);
                    backTransition.duration = 0;
                    backTransition.hasExitTime = true;
                    backTransition.exitTime = 1f;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[ClawPipeline] Created animator controller: {controllerPath} ({clipPaths.Count} states)");
            return controllerPath;
        }

        // =====================================================================
        // 4. Create Enemy Prefab
        // =====================================================================

        /// <summary>
        /// Create an enemy prefab with SpriteRenderer, Animator, and enemy script.
        /// </summary>
        /// <param name="enemyName">Name of the enemy</param>
        /// <param name="controllerPath">Path to the .controller asset</param>
        /// <param name="idleSpriteSheetPath">Path to the idle sprite sheet (for initial sprite)</param>
        /// <param name="enemyType">Enemy type: "shooter", "fighter", "chaser", "sentinel"</param>
        /// <param name="outputDir">Output directory (default: Assets/Prefabs/Enemies/)</param>
        /// <returns>Path to created prefab</returns>
        public static string CreateEnemyPrefab(
            string enemyName,
            string controllerPath,
            string idleSpriteSheetPath,
            string enemyType = "shooter",
            string outputDir = "Assets/Prefabs/Enemies")
        {
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
                AssetDatabase.Refresh();
            }

            // Create root GameObject
            var go = new GameObject(enemyName);

            // Add SpriteRenderer
            var sr = go.AddComponent<SpriteRenderer>();

            // Set initial sprite from idle sheet
            var sprites = AssetDatabase.LoadAllAssetsAtPath(idleSpriteSheetPath)
                .OfType<Sprite>()
                .OrderBy(s => s.name)
                .ToArray();
            if (sprites.Length > 0)
            {
                sr.sprite = sprites[0];
            }

            // Add Animator
            var animator = go.AddComponent<Animator>();
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
            if (controller != null)
            {
                animator.runtimeAnimatorController = controller;
            }

            // Add Collider2D
            var collider = go.AddComponent<BoxCollider2D>();

            // Add Rigidbody2D
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            // Add enemy script by type name (avoids assembly reference to SickOfIt.Combat)
            string[] searchNamespaces = {
                "SickOfIt.Combat.Enemies",
                "SickOfIt.Combat",
                ""
            };
            string targetTypeName = enemyType.ToLower() switch
            {
                "shooter" => "EnemyShooter",
                "fighter" => "EnemyFighter",
                "chaser"  => "EnemyChaser",
                "sentinel"=> "EnemySentinel",
                _ => enemyType // allow full type name passthrough
            };

            System.Type enemyScriptType = null;
            foreach (var ns in searchNamespaces)
            {
                string fullName = string.IsNullOrEmpty(ns) ? targetTypeName : $"{ns}.{targetTypeName}";
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    enemyScriptType = asm.GetType(fullName);
                    if (enemyScriptType != null) break;
                }
                if (enemyScriptType != null) break;
            }

            if (enemyScriptType != null && typeof(MonoBehaviour).IsAssignableFrom(enemyScriptType))
            {
                go.AddComponent(enemyScriptType);
            }
            else
            {
                Debug.LogWarning($"[ClawPipeline] Could not find enemy script type: {targetTypeName}. Prefab created without enemy script.");
            }

            // Save as prefab
            string prefabPath = $"{outputDir}/{enemyName}.prefab";
            prefabPath = AssetDatabase.GenerateUniqueAssetPath(prefabPath);
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);

            Debug.Log($"[ClawPipeline] Created enemy prefab: {prefabPath} (type={enemyType})");
            return prefabPath;
        }

        // =====================================================================
        // Menu Items (for manual testing / MCP execute_menu_item)
        // =====================================================================

        /// <summary>
        /// Import a sprite sheet from a JSON metadata file.
        /// Reads the metadata JSON next to the sprite sheet and auto-configures it.
        /// Usage: Select a *_meta.json file in the Project window, then run this.
        /// </summary>
        [MenuItem("Tools/Claw/Import Sprite Sheet From Metadata")]
        public static void ImportSpriteSheetFromMetadata()
        {
            var selected = Selection.activeObject;
            if (selected == null)
            {
                Debug.LogError("[ClawPipeline] Select a metadata JSON file first.");
                return;
            }

            string metaPath = AssetDatabase.GetAssetPath(selected);
            if (!metaPath.EndsWith("_meta.json"))
            {
                Debug.LogError("[ClawPipeline] Selected file must be a *_meta.json file.");
                return;
            }

            string fullPath = Path.GetFullPath(metaPath);
            string jsonText = File.ReadAllText(fullPath);

            // Simple JSON parsing (no external dependencies)
            // Expected format: {"name": "X", "animation": "idle", "columns": 4, "rows": 3, ...}
            var meta = JsonUtility.FromJson<CharacterMeta>(jsonText);
            if (meta == null)
            {
                Debug.LogError("[ClawPipeline] Failed to parse metadata JSON.");
                return;
            }

            string sheetDir = Path.GetDirectoryName(metaPath);
            string sheetPath = $"{sheetDir}/{meta.name}_{meta.animation}.png";

            if (!File.Exists(Path.GetFullPath(sheetPath)))
            {
                Debug.LogError($"[ClawPipeline] Sprite sheet not found: {sheetPath}");
                return;
            }

            ImportSpriteSheet(sheetPath, meta.columns, meta.rows);
            Debug.Log($"[ClawPipeline] Imported {meta.name}/{meta.animation} from metadata.");
        }

        /// <summary>
        /// Full pipeline: reads all *_meta.json files for a character, imports sheets,
        /// creates animation clips, creates animator controller, creates prefab.
        /// Usage: Select a character's *_meta.json files, then run this.
        /// </summary>
        [MenuItem("Tools/Claw/Create Character From Metadata")]
        public static void CreateCharacterFromMetadata()
        {
            var selectedObjects = Selection.objects;
            var metaFiles = new List<string>();

            foreach (var obj in selectedObjects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (path.EndsWith("_meta.json"))
                {
                    metaFiles.Add(path);
                }
            }

            if (metaFiles.Count == 0)
            {
                Debug.LogError("[ClawPipeline] Select one or more *_meta.json files.");
                return;
            }

            string characterName = null;
            var clipPaths = new Dictionary<string, string>();
            string idleSheetPath = null;

            foreach (string metaPath in metaFiles)
            {
                string fullPath = Path.GetFullPath(metaPath);
                string jsonText = File.ReadAllText(fullPath);
                var meta = JsonUtility.FromJson<CharacterMeta>(jsonText);
                if (meta == null) continue;

                characterName = characterName ?? meta.name;

                string sheetDir = Path.GetDirectoryName(metaPath);
                string sheetPath = $"{sheetDir}/{meta.name}_{meta.animation}.png";

                // Import sprite sheet
                ImportSpriteSheet(sheetPath, meta.columns, meta.rows);

                // Create animation clip
                string stateName = char.ToUpper(meta.animation[0]) + meta.animation.Substring(1);
                bool shouldLoop = meta.loop;
                string clipPath = CreateAnimClip(sheetPath, $"{meta.name}_{meta.animation}", meta.fps, shouldLoop);

                if (clipPath != null)
                {
                    clipPaths[stateName] = clipPath;
                }

                if (meta.animation.ToLower() == "idle")
                {
                    idleSheetPath = sheetPath;
                }
            }

            if (characterName == null || clipPaths.Count == 0)
            {
                Debug.LogError("[ClawPipeline] Failed to process metadata files.");
                return;
            }

            // Create animator controller
            string controllerPath = CreateAnimatorController(characterName, clipPaths);

            // Create prefab
            if (idleSheetPath != null)
            {
                CreateEnemyPrefab(characterName, controllerPath, idleSheetPath);
            }

            Debug.Log($"[ClawPipeline] Character '{characterName}' created with {clipPaths.Count} animations!");
        }

        // =====================================================================
        // Helper types
        // =====================================================================

        [System.Serializable]
        private class CharacterMeta
        {
            public string name;
            public string animation;
            public int frameCount;
            public int columns;
            public int rows;
            public int frameWidth;
            public int frameHeight;
            public int sheetWidth;
            public int sheetHeight;
            public string spriteSheetFile;
            public int fps = 12;
            public bool loop = true;
        }
    }

    // Extension method for TextureImporter to get dimensions
    public static class TextureImporterExtensions
    {
        public static void GetWidthAndHeight(this TextureImporter importer, out int width, out int height)
        {
            var method = typeof(TextureImporter).GetMethod("GetWidthAndHeight",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (method != null)
            {
                var args = new object[] { 0, 0 };
                method.Invoke(importer, args);
                width = (int)args[0];
                height = (int)args[1];
            }
            else
            {
                // Fallback
                width = 512;
                height = 512;
            }
        }
    }
}
#endif

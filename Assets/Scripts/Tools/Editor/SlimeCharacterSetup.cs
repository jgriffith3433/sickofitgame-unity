#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using System.Linq;

namespace SickOfIt.Tools.Editor
{
    public static class SlimeCharacterSetup
    {
        private const string SpriteSheetPath = "Assets/Art/Sprites/Enemies/slime_idle.png";
        private const string AnimationFolder = "Assets/Art/Animations/Enemies";
        private const string PrefabFolder = "Assets/Prefabs/Enemies";
        private const int Columns = 4;
        private const int Rows = 2;
        private const int FrameWidth = 400;
        private const int FrameHeight = 400;
        private const int PixelsPerUnit = 100;
        private const float FrameRate = 8f;

        [MenuItem("Tools/Sick Of It/Setup Slime Character")]
        public static void Setup()
        {
            // 1. Configure sprite sheet import settings
            ConfigureSpriteSheet();

            // 2. Create animation clip
            var clip = CreateAnimationClip();

            // 3. Create animator controller
            var controller = CreateAnimatorController(clip);

            // 4. Create prefab
            CreatePrefab(controller);

            Debug.Log("[SlimeSetup] ✅ Slime character setup complete!");
        }

        private static void ConfigureSpriteSheet()
        {
            var importer = AssetImporter.GetAtPath(SpriteSheetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[SlimeSetup] Sprite sheet not found at {SpriteSheetPath}. Refreshing AssetDatabase...");
                AssetDatabase.Refresh();
                importer = AssetImporter.GetAtPath(SpriteSheetPath) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogError("[SlimeSetup] Still can't find sprite sheet. Aborting.");
                    return;
                }
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;

            // Set up sprite sheet slicing (4x2 grid)
            var spriteSheet = new SpriteMetaData[Columns * Rows];
            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Columns; col++)
                {
                    int index = row * Columns + col;
                    spriteSheet[index] = new SpriteMetaData
                    {
                        name = $"slime_idle_{index}",
                        rect = new Rect(
                            col * FrameWidth,
                            (Rows - 1 - row) * FrameHeight, // Unity UV starts from bottom
                            FrameWidth,
                            FrameHeight
                        ),
                        alignment = (int)SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f)
                    };
                }
            }

            importer.spritesheet = spriteSheet;
            importer.SaveAndReimport();
            Debug.Log($"[SlimeSetup] Sprite sheet configured: {Columns * Rows} frames at {FrameWidth}x{FrameHeight}");
        }

        private static AnimationClip CreateAnimationClip()
        {
            EnsureFolder(AnimationFolder);

            // Load all sprites from the sliced sheet
            var sprites = AssetDatabase.LoadAllAssetsAtPath(SpriteSheetPath)
                .OfType<Sprite>()
                .OrderBy(s => s.name)
                .ToArray();

            if (sprites.Length == 0)
            {
                Debug.LogError("[SlimeSetup] No sprites found in sheet!");
                return null;
            }

            var clip = new AnimationClip();
            clip.frameRate = FrameRate;
            clip.wrapMode = WrapMode.Loop;

            // Create sprite keyframes
            var keyframes = new ObjectReferenceKeyframe[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframe
                {
                    time = i / FrameRate,
                    value = sprites[i]
                };
            }

            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

            // Set loop
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            string clipPath = $"{AnimationFolder}/SlimeIdle.anim";
            AssetDatabase.CreateAsset(clip, clipPath);
            Debug.Log($"[SlimeSetup] Animation clip created: {clipPath} ({sprites.Length} frames at {FrameRate} fps)");

            return clip;
        }

        private static AnimatorController CreateAnimatorController(AnimationClip idleClip)
        {
            EnsureFolder(AnimationFolder);

            string controllerPath = $"{AnimationFolder}/SlimeAnimator.controller";
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            // Add idle state
            var rootStateMachine = controller.layers[0].stateMachine;
            var idleState = rootStateMachine.AddState("Idle");
            idleState.motion = idleClip;
            rootStateMachine.defaultState = idleState;

            Debug.Log($"[SlimeSetup] Animator controller created: {controllerPath}");
            return controller;
        }

        private static void CreatePrefab(RuntimeAnimatorController controller)
        {
            EnsureFolder(PrefabFolder);

            // Load first sprite for the default visual
            var sprites = AssetDatabase.LoadAllAssetsAtPath(SpriteSheetPath)
                .OfType<Sprite>()
                .OrderBy(s => s.name)
                .ToArray();

            // Create root GameObject
            var root = new GameObject("SlimeEnemy");

            // Add SpriteRenderer
            var sr = root.AddComponent<SpriteRenderer>();
            if (sprites.Length > 0)
                sr.sprite = sprites[0];
            sr.sortingLayerName = "Default";

            // Add Animator
            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            // Add CircleCollider2D for the blob shape
            var collider = root.AddComponent<CircleCollider2D>();
            collider.radius = 1.5f;
            collider.offset = new Vector2(0f, 0.2f);

            // Add Rigidbody2D
            var rb = root.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.freezeRotation = true;

            // Scale to game size (400px at 100ppu = 4 units, we want ~2 units)
            root.transform.localScale = new Vector3(0.5f, 0.5f, 1f);

            // Save as prefab
            string prefabPath = $"{PrefabFolder}/SlimeEnemy.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            Debug.Log($"[SlimeSetup] Prefab created: {prefabPath}");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif

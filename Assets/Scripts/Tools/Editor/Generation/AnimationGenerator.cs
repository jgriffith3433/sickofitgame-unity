#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using SickOfIt.Data.Config;

namespace SickOfIt.Tools.Editor.Generation
{
    /// <summary>
    /// Generates animator controllers and animation clips from AnimationConfig.
    /// 
    /// Pipeline:
    ///   1. For each AnimationStateConfig, load/slice the sprite sheet
    ///   2. Create a sprite-swap .anim clip with keyframes + events
    ///   3. Build an AnimatorController with states, parameters, and transitions
    ///   4. Save all assets to the specified output folder
    ///
    /// Used by EnemyPrefabGenerator and PlayerPrefabGenerator.
    /// </summary>
    public static class AnimationGenerator
    {
        /// <summary>
        /// Result returned after generating animations.
        /// </summary>
        public class GenerationResult
        {
            public AnimatorController controller;
            public Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();
        }

        /// <summary>
        /// Generate a complete animator controller from an AnimationConfig.
        /// </summary>
        /// <param name="config">The animation config describing states, transitions, parameters</param>
        /// <param name="entityName">Name prefix for assets (e.g., "Enemy_Fighter")</param>
        /// <param name="outputFolder">Folder to save .controller and .anim files (e.g., "Assets/Animations/Enemies/Enemy_Fighter")</param>
        /// <param name="fallbackSpriteSheet">Sprite sheet to use if a state doesn't specify one</param>
        /// <param name="fallbackFps">FPS to use if a state doesn't specify one</param>
        /// <returns>The generated controller, or null on failure</returns>
        public static GenerationResult Generate(
            AnimationConfig config,
            string entityName,
            string outputFolder,
            Texture2D fallbackSpriteSheet = null,
            int fallbackFps = 12)
        {
            if (config == null || config.states == null || config.states.Count == 0)
            {
                Debug.LogWarning($"[AnimationGenerator] No states in config for {entityName}");
                return null;
            }

            ProjectGenerator.EnsureFolder(outputFolder);

            var result = new GenerationResult();

            // ─── Step 1: Create clips for each state ─────────────────────
            foreach (var stateConfig in config.states)
            {
                if (string.IsNullOrEmpty(stateConfig.stateName)) continue;

                var spriteSheet = stateConfig.spriteSheet != null ? stateConfig.spriteSheet : fallbackSpriteSheet;
                int fps = stateConfig.fps > 0 ? stateConfig.fps : fallbackFps;

                var sprites = LoadSprites(spriteSheet, stateConfig.spriteNameFilter);
                if (sprites == null || sprites.Count == 0)
                {
                    Debug.LogWarning($"[AnimationGenerator] No sprites for state '{stateConfig.stateName}' of {entityName}");
                    continue;
                }

                var clip = CreateClip(stateConfig, sprites, fps, entityName, outputFolder);
                if (clip != null)
                    result.clips[stateConfig.stateName] = clip;
            }

            if (result.clips.Count == 0)
            {
                Debug.LogWarning($"[AnimationGenerator] No clips created for {entityName}");
                return null;
            }

            // ─── Step 2: Create animator controller ──────────────────────
            string controllerPath = $"{outputFolder}/{entityName}.controller";

            // Delete existing controller to avoid stale states
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
                AssetDatabase.DeleteAsset(controllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            result.controller = controller;

            // ─── Step 3: Add parameters ──────────────────────────────────
            if (config.parameters != null)
            {
                foreach (var param in config.parameters)
                {
                    if (string.IsNullOrEmpty(param.name)) continue;

                    var unityType = ToUnityParameterType(param.type);
                    controller.AddParameter(param.name, unityType);

                    // Set default values
                    var parameters = controller.parameters;
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (parameters[i].name == param.name)
                        {
                            switch (unityType)
                            {
                                case AnimatorControllerParameterType.Bool:
                                    parameters[i].defaultBool = param.defaultValue > 0.5f;
                                    break;
                                case AnimatorControllerParameterType.Int:
                                    parameters[i].defaultInt = (int)param.defaultValue;
                                    break;
                                case AnimatorControllerParameterType.Float:
                                    parameters[i].defaultFloat = param.defaultValue;
                                    break;
                            }
                            controller.parameters = parameters;
                            break;
                        }
                    }
                }
            }

            // ─── Step 4: Add states ──────────────────────────────────────
            var rootSM = controller.layers[0].stateMachine;
            var stateMap = new Dictionary<string, AnimatorState>();

            foreach (var stateConfig in config.states)
            {
                if (!result.clips.ContainsKey(stateConfig.stateName)) continue;

                var state = rootSM.AddState(stateConfig.stateName);
                state.motion = result.clips[stateConfig.stateName];
                state.speed = stateConfig.speed > 0 ? stateConfig.speed : 1f;
                stateMap[stateConfig.stateName] = state;

                // Set default state
                if (stateConfig.stateName == config.defaultStateName)
                    rootSM.defaultState = state;
            }

            // If default state wasn't found, use first
            if (rootSM.defaultState == null && stateMap.Count > 0)
                rootSM.defaultState = stateMap.Values.First();

            // ─── Step 5: Add transitions ─────────────────────────────────
            if (config.transitions != null)
            {
                foreach (var transConfig in config.transitions)
                {
                    if (string.IsNullOrEmpty(transConfig.toState)) continue;
                    if (!stateMap.ContainsKey(transConfig.toState)) continue;

                    AnimatorStateTransition transition;

                    if (transConfig.fromState == "Any" || transConfig.fromState == "AnyState")
                    {
                        transition = rootSM.AddAnyStateTransition(stateMap[transConfig.toState]);
                    }
                    else if (!string.IsNullOrEmpty(transConfig.fromState) && stateMap.ContainsKey(transConfig.fromState))
                    {
                        transition = stateMap[transConfig.fromState].AddTransition(stateMap[transConfig.toState]);
                    }
                    else
                    {
                        Debug.LogWarning($"[AnimationGenerator] Unknown from-state '{transConfig.fromState}' in transition for {entityName}");
                        continue;
                    }

                    transition.hasExitTime = transConfig.hasExitTime;
                    transition.exitTime = transConfig.exitTime;
                    transition.duration = transConfig.transitionDuration;
                    transition.hasFixedDuration = true;

                    // Add conditions
                    if (transConfig.conditions != null)
                    {
                        foreach (var cond in transConfig.conditions)
                        {
                            if (string.IsNullOrEmpty(cond.parameterName)) continue;

                            var mode = ToUnityConditionMode(cond.mode);
                            transition.AddCondition(mode, cond.threshold, cond.parameterName);
                        }
                    }
                }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log($"[AnimationGenerator] Generated controller for {entityName}: {result.clips.Count} clips, {config.parameters?.Count ?? 0} params, {config.transitions?.Count ?? 0} transitions");

            return result;
        }

        /// <summary>
        /// Simple fallback: create a single idle clip + controller (no AnimationConfig needed).
        /// Used when animationConfig is null but a spriteSheet is provided.
        /// </summary>
        public static GenerationResult GenerateSimpleIdle(
            string entityName,
            string outputFolder,
            Texture2D spriteSheet,
            int fps = 12)
        {
            if (spriteSheet == null) return null;

            var sprites = LoadSprites(spriteSheet, null);
            if (sprites == null || sprites.Count == 0) return null;

            ProjectGenerator.EnsureFolder(outputFolder);

            var result = new GenerationResult();

            // Create idle clip
            var stateConfig = new AnimationStateConfig
            {
                stateName = "Idle",
                fps = fps,
                loop = true
            };
            var clip = CreateClip(stateConfig, sprites, fps, entityName, outputFolder);
            if (clip == null) return null;

            result.clips["Idle"] = clip;

            // Create controller
            string controllerPath = $"{outputFolder}/{entityName}.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
                AssetDatabase.DeleteAsset(controllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var rootSM = controller.layers[0].stateMachine;
            var idleState = rootSM.AddState("Idle");
            idleState.motion = clip;
            rootSM.defaultState = idleState;

            result.controller = controller;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return result;
        }

        /// <summary>
        /// Delete all generated animation assets in a folder.
        /// </summary>
        public static void DeleteFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.DeleteAsset(folderPath);
                Debug.Log($"[AnimationGenerator] Deleted folder: {folderPath}");
            }
        }

        #region Internal

        /// <summary>
        /// Load sprites from a Texture2D asset, optionally filtering by name prefix.
        /// </summary>
        private static List<Sprite> LoadSprites(Texture2D texture, string nameFilter)
        {
            if (texture == null) return null;

            var assetPath = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(assetPath)) return null;

            var allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            var sprites = new List<Sprite>();

            foreach (var asset in allAssets)
            {
                if (asset is Sprite sprite)
                {
                    if (string.IsNullOrEmpty(nameFilter) || sprite.name.StartsWith(nameFilter))
                        sprites.Add(sprite);
                }
            }

            sprites.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            return sprites;
        }

        /// <summary>
        /// Create a single .anim clip from sprites with optional animation events.
        /// </summary>
        private static AnimationClip CreateClip(
            AnimationStateConfig stateConfig,
            List<Sprite> sprites,
            int fps,
            string entityName,
            string outputFolder)
        {
            // Ensure valid fps (fallback to 12 if invalid)
            if (fps <= 0) fps = 12;
            
            var clip = new AnimationClip();
            clip.name = $"{entityName}_{stateConfig.stateName}";
            clip.frameRate = fps;

            // Loop settings
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = stateConfig.loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            // Sprite keyframes
            float frameDuration = 1f / fps;
            var keyframes = new ObjectReferenceKeyframe[sprites.Count];
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

            // Animation events
            if (stateConfig.events != null && stateConfig.events.Count > 0)
            {
                var events = new AnimationEvent[stateConfig.events.Count];
                for (int i = 0; i < stateConfig.events.Count; i++)
                {
                    var evtConfig = stateConfig.events[i];
                    events[i] = new AnimationEvent
                    {
                        time = evtConfig.frame * frameDuration,
                        functionName = evtConfig.functionName,
                        stringParameter = evtConfig.stringParameter ?? "",
                        intParameter = evtConfig.intParameter,
                        floatParameter = evtConfig.floatParameter
                    };
                }
                AnimationUtility.SetAnimationEvents(clip, events);
            }

            // Save clip
            string clipPath = $"{outputFolder}/{clip.name}.anim";
            AssetDatabase.CreateAsset(clip, clipPath);

            return clip;
        }

        private static AnimatorControllerParameterType ToUnityParameterType(AnimatorParameterType type)
        {
            switch (type)
            {
                case AnimatorParameterType.Float: return AnimatorControllerParameterType.Float;
                case AnimatorParameterType.Int: return AnimatorControllerParameterType.Int;
                case AnimatorParameterType.Bool: return AnimatorControllerParameterType.Bool;
                case AnimatorParameterType.Trigger: return AnimatorControllerParameterType.Trigger;
                default: return AnimatorControllerParameterType.Bool;
            }
        }

        private static AnimatorConditionMode ToUnityConditionMode(ConditionMode mode)
        {
            switch (mode)
            {
                case ConditionMode.If: return AnimatorConditionMode.If;
                case ConditionMode.IfNot: return AnimatorConditionMode.IfNot;
                case ConditionMode.Greater: return AnimatorConditionMode.Greater;
                case ConditionMode.Less: return AnimatorConditionMode.Less;
                case ConditionMode.Equals: return AnimatorConditionMode.Equals;
                case ConditionMode.NotEqual: return AnimatorConditionMode.NotEqual;
                default: return AnimatorConditionMode.If;
            }
        }

        #endregion
    }
}
#endif

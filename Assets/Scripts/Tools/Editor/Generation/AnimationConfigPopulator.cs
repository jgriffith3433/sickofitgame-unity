#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using System.Collections.Generic;

namespace SickOfIt.Tools.Editor.Generation
{
    /// <summary>
    /// Builds AnimationConfig data for enemies and player at generation time.
    /// 
    /// NOT a standalone menu tool — called automatically by EnemyPrefabGenerator
    /// and PlayerPrefabGenerator when a template/config has no animationConfig set.
    /// 
    /// This ensures the animator parameters, states, transitions, and events
    /// match exactly what the runtime code expects for each entity type.
    /// </summary>
    public static class AnimationConfigPopulator
    {
        /// <summary>
        /// Create the correct AnimationConfig for an enemy template based on its type.
        /// Returns null if the type is unknown.
        /// </summary>
        public static AnimationConfig CreateEnemyAnimConfig(EnemyTemplate template)
        {
            if (template == null) return null;

            switch (template.type)
            {
                case EnemyType.Chaser:
                    return CreateChaserConfig(template);
                case EnemyType.Fighter:
                    return CreateFighterConfig(template);
                case EnemyType.Shooter:
                    return CreateShooterConfig(template);
                case EnemyType.Sentinel:
                    return CreateSentinelConfig(template);
                default:
                    Debug.LogWarning($"[AnimConfigPopulator] Unknown enemy type: {template.type}");
                    return null;
            }
        }

        /// <summary>
        /// Create the AnimationConfig for the player.
        /// </summary>
        public static AnimationConfig CreatePlayerAnimConfig(PlayerConfig config)
        {
            if (config == null) return null;
            return CreatePlayerConfig(config);
        }

        /// <summary>
        /// Chaser: Simple looping idle animation. No parameters, no events.
        /// </summary>
        private static AnimationConfig CreateChaserConfig(EnemyTemplate template)
        {
            return new AnimationConfig
            {
                defaultStateName = "Idle",
                parameters = new List<AnimatorParameterConfig>(),
                states = new List<AnimationStateConfig>
                {
                    new AnimationStateConfig
                    {
                        stateName = "Idle",
                        fps = template.animationFps,
                        loop = true
                    }
                },
                transitions = new List<AnimationTransitionConfig>()
            };
        }

        /// <summary>
        /// Fighter: Idle, Walk, Attack states.
        /// Parameters: IsWalking (bool), IsAttacking (bool)
        /// Event: OnAttackFrame on the Attack clip
        /// </summary>
        private static AnimationConfig CreateFighterConfig(EnemyTemplate template)
        {
            return new AnimationConfig
            {
                defaultStateName = "Idle",
                parameters = new List<AnimatorParameterConfig>
                {
                    new AnimatorParameterConfig { name = "IsWalking", type = AnimatorParameterType.Bool },
                    new AnimatorParameterConfig { name = "IsAttacking", type = AnimatorParameterType.Bool }
                },
                states = new List<AnimationStateConfig>
                {
                    new AnimationStateConfig
                    {
                        stateName = "Idle",
                        spriteSheet = FindSpriteSheet("FighterIdle"),
                        fps = template.animationFps,
                        loop = true
                    },
                    new AnimationStateConfig
                    {
                        stateName = "Walk",
                        spriteSheet = FindSpriteSheet("FighterWalk"),
                        fps = template.animationFps,
                        loop = true
                    },
                    new AnimationStateConfig
                    {
                        stateName = "Attack",
                        spriteSheet = FindSpriteSheet("FighterAttack"),
                        fps = template.animationFps,
                        loop = false,
                        events = new List<AnimationEventConfig>
                        {
                            new AnimationEventConfig
                            {
                                frame = 2, // Mid-attack frame
                                functionName = "OnAttackFrame"
                            }
                        }
                    }
                },
                transitions = new List<AnimationTransitionConfig>
                {
                    // Idle → Walk when IsWalking == true
                    new AnimationTransitionConfig
                    {
                        fromState = "Idle", toState = "Walk",
                        conditions = new List<TransitionConditionConfig>
                        {
                            new TransitionConditionConfig { parameterName = "IsWalking", mode = ConditionMode.If }
                        }
                    },
                    // Walk → Idle when IsWalking == false
                    new AnimationTransitionConfig
                    {
                        fromState = "Walk", toState = "Idle",
                        conditions = new List<TransitionConditionConfig>
                        {
                            new TransitionConditionConfig { parameterName = "IsWalking", mode = ConditionMode.IfNot }
                        }
                    },
                    // Any → Attack when IsAttacking == true
                    new AnimationTransitionConfig
                    {
                        fromState = "Any", toState = "Attack",
                        conditions = new List<TransitionConditionConfig>
                        {
                            new TransitionConditionConfig { parameterName = "IsAttacking", mode = ConditionMode.If }
                        }
                    },
                    // Attack → Idle when IsAttacking == false (wait for clip to finish)
                    new AnimationTransitionConfig
                    {
                        fromState = "Attack", toState = "Idle",
                        hasExitTime = true,
                        exitTime = 0.9f,
                        conditions = new List<TransitionConditionConfig>
                        {
                            new TransitionConditionConfig { parameterName = "IsAttacking", mode = ConditionMode.IfNot }
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Shooter: Idle, Attack states.
        /// Parameters: IsAttacking (bool)
        /// Event: FireProjectile on the Attack clip
        /// </summary>
        private static AnimationConfig CreateShooterConfig(EnemyTemplate template)
        {
            return new AnimationConfig
            {
                defaultStateName = "Idle",
                parameters = new List<AnimatorParameterConfig>
                {
                    new AnimatorParameterConfig { name = "IsAttacking", type = AnimatorParameterType.Bool }
                },
                states = new List<AnimationStateConfig>
                {
                    new AnimationStateConfig
                    {
                        stateName = "Idle",
                        fps = template.animationFps,
                        loop = true
                    },
                    new AnimationStateConfig
                    {
                        stateName = "Attack",
                        fps = template.animationFps,
                        loop = false,
                        events = new List<AnimationEventConfig>
                        {
                            new AnimationEventConfig
                            {
                                frame = 1,
                                functionName = "FireProjectile"
                            }
                        }
                    }
                },
                transitions = new List<AnimationTransitionConfig>
                {
                    new AnimationTransitionConfig
                    {
                        fromState = "Idle", toState = "Attack",
                        conditions = new List<TransitionConditionConfig>
                        {
                            new TransitionConditionConfig { parameterName = "IsAttacking", mode = ConditionMode.If }
                        }
                    },
                    new AnimationTransitionConfig
                    {
                        fromState = "Attack", toState = "Idle",
                        hasExitTime = true,
                        exitTime = 0.9f,
                        conditions = new List<TransitionConditionConfig>
                        {
                            new TransitionConditionConfig { parameterName = "IsAttacking", mode = ConditionMode.IfNot }
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Sentinel: StandingIdle, StandingAttack, StandingToCrouch, CrouchingIdle, CrouchingAttack, CrouchToStanding.
        /// Parameters: IsCrouching (bool), IsAttacking (bool)
        /// Events: OnStandingAttackFrame, OnCrouchAttackFrame
        /// </summary>
        private static AnimationConfig CreateSentinelConfig(EnemyTemplate template)
        {
            return new AnimationConfig
            {
                defaultStateName = "StandingIdle",
                parameters = new List<AnimatorParameterConfig>
                {
                    new AnimatorParameterConfig { name = "IsCrouching", type = AnimatorParameterType.Bool },
                    new AnimatorParameterConfig { name = "IsAttacking", type = AnimatorParameterType.Bool }
                },
                states = new List<AnimationStateConfig>
                {
                    new AnimationStateConfig
                    {
                        stateName = "StandingIdle",
                        spriteSheet = FindSpriteSheet("Level3Enemy_StandingIdle"),
                        fps = template.animationFps,
                        loop = true
                    },
                    new AnimationStateConfig
                    {
                        stateName = "StandingAttack",
                        spriteSheet = FindSpriteSheet("Level3Enemy_StandingAttack"),
                        fps = template.animationFps,
                        loop = false,
                        events = new List<AnimationEventConfig>
                        {
                            new AnimationEventConfig
                            {
                                frame = 2,
                                functionName = "OnStandingAttackFrame"
                            }
                        }
                    },
                    new AnimationStateConfig
                    {
                        stateName = "StandingToCrouch",
                        spriteSheet = FindSpriteSheet("Level3Enemy_StandingToCrouch"),
                        fps = template.animationFps,
                        loop = false
                    },
                    new AnimationStateConfig
                    {
                        stateName = "CrouchingIdle",
                        spriteSheet = FindSpriteSheet("Level3Enemy_CrouchingIdle"),
                        fps = template.animationFps,
                        loop = true
                    },
                    new AnimationStateConfig
                    {
                        stateName = "CrouchingAttack",
                        spriteSheet = FindSpriteSheet("Level3Enemy_CrouchingAttack"),
                        fps = template.animationFps,
                        loop = false,
                        events = new List<AnimationEventConfig>
                        {
                            new AnimationEventConfig
                            {
                                frame = 2,
                                functionName = "OnCrouchAttackFrame"
                            }
                        }
                    },
                    new AnimationStateConfig
                    {
                        stateName = "CrouchToStanding",
                        spriteSheet = FindSpriteSheet("Level3Enemy_CrouchToStanding"),
                        fps = template.animationFps,
                        loop = false
                    }
                },
                transitions = new List<AnimationTransitionConfig>
                {
                    // Standing → Crouch transition
                    new AnimationTransitionConfig
                    {
                        fromState = "StandingIdle", toState = "StandingToCrouch",
                        conditions = new List<TransitionConditionConfig>
                        {
                            new TransitionConditionConfig { parameterName = "IsCrouching", mode = ConditionMode.If }
                        }
                    },
                    new AnimationTransitionConfig
                    {
                        fromState = "StandingToCrouch", toState = "CrouchingIdle",
                        hasExitTime = true, exitTime = 1f
                    },
                    // Crouch → Standing transition
                    new AnimationTransitionConfig
                    {
                        fromState = "CrouchingIdle", toState = "CrouchToStanding",
                        conditions = new List<TransitionConditionConfig>
                        {
                            new TransitionConditionConfig { parameterName = "IsCrouching", mode = ConditionMode.IfNot }
                        }
                    },
                    new AnimationTransitionConfig
                    {
                        fromState = "CrouchToStanding", toState = "StandingIdle",
                        hasExitTime = true, exitTime = 1f
                    },
                    // Standing attack
                    new AnimationTransitionConfig
                    {
                        fromState = "StandingIdle", toState = "StandingAttack",
                        conditions = new List<TransitionConditionConfig>
                        {
                            new TransitionConditionConfig { parameterName = "IsAttacking", mode = ConditionMode.If },
                            new TransitionConditionConfig { parameterName = "IsCrouching", mode = ConditionMode.IfNot }
                        }
                    },
                    new AnimationTransitionConfig
                    {
                        fromState = "StandingAttack", toState = "StandingIdle",
                        hasExitTime = true, exitTime = 0.9f,
                        conditions = new List<TransitionConditionConfig>
                        {
                            new TransitionConditionConfig { parameterName = "IsAttacking", mode = ConditionMode.IfNot }
                        }
                    },
                    // Crouching attack
                    new AnimationTransitionConfig
                    {
                        fromState = "CrouchingIdle", toState = "CrouchingAttack",
                        conditions = new List<TransitionConditionConfig>
                        {
                            new TransitionConditionConfig { parameterName = "IsAttacking", mode = ConditionMode.If },
                            new TransitionConditionConfig { parameterName = "IsCrouching", mode = ConditionMode.If }
                        }
                    },
                    new AnimationTransitionConfig
                    {
                        fromState = "CrouchingAttack", toState = "CrouchingIdle",
                        hasExitTime = true, exitTime = 0.9f,
                        conditions = new List<TransitionConditionConfig>
                        {
                            new TransitionConditionConfig { parameterName = "IsAttacking", mode = ConditionMode.IfNot }
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Build the player's animation config.
        /// Player uses: OnGround (bool), Velocity_X (float), Velocity_Z (float), Jump (trigger), Fire (trigger)
        /// States: Idle, Walk, Jump, Fall, Fire (based on parameter-driven blend)
        /// </summary>
        private static AnimationConfig CreatePlayerConfig(PlayerConfig config)
        {
            return new AnimationConfig
            {
                defaultStateName = "Idle",
                parameters = new List<AnimatorParameterConfig>
                {
                    new AnimatorParameterConfig { name = "OnGround", type = AnimatorParameterType.Bool, defaultValue = 1 },
                    new AnimatorParameterConfig { name = "Velocity_X", type = AnimatorParameterType.Float },
                    new AnimatorParameterConfig { name = "Velocity_Z", type = AnimatorParameterType.Float },
                    new AnimatorParameterConfig { name = "Jump", type = AnimatorParameterType.Trigger },
                    new AnimatorParameterConfig { name = "Fire", type = AnimatorParameterType.Trigger }
                },
                states = new List<AnimationStateConfig>
                {
                    new AnimationStateConfig
                    {
                        stateName = "Idle",
                        fps = config.animationFps,
                        loop = true
                    },
                    new AnimationStateConfig
                    {
                        stateName = "Walk",
                        fps = config.animationFps,
                        loop = true
                    },
                    new AnimationStateConfig
                    {
                        stateName = "Jump",
                        fps = config.animationFps,
                        loop = false
                    },
                    new AnimationStateConfig
                    {
                        stateName = "Fall",
                        fps = config.animationFps,
                        loop = true
                    },
                    new AnimationStateConfig
                    {
                        stateName = "Fire",
                        fps = config.animationFps,
                        loop = false
                    }
                },
                transitions = new List<AnimationTransitionConfig>
                {
                    // Idle → Walk when moving
                    new AnimationTransitionConfig
                    {
                        fromState = "Idle", toState = "Walk",
                        conditions = new List<TransitionConditionConfig>
                        {
                            new TransitionConditionConfig { parameterName = "Velocity_X", mode = ConditionMode.Greater, threshold = 0.1f },
                            new TransitionConditionConfig { parameterName = "OnGround", mode = ConditionMode.If }
                        }
                    },
                    // Walk → Idle when stopped
                    new AnimationTransitionConfig
                    {
                        fromState = "Walk", toState = "Idle",
                        conditions = new List<TransitionConditionConfig>
                        {
                            new TransitionConditionConfig { parameterName = "Velocity_X", mode = ConditionMode.Less, threshold = 0.1f }
                        }
                    },
                    // Any → Jump on trigger
                    new AnimationTransitionConfig
                    {
                        fromState = "Any", toState = "Jump",
                        conditions = new List<TransitionConditionConfig>
                        {
                            new TransitionConditionConfig { parameterName = "Jump", mode = ConditionMode.If }
                        }
                    },
                    // Jump → Fall when descending
                    new AnimationTransitionConfig
                    {
                        fromState = "Jump", toState = "Fall",
                        conditions = new List<TransitionConditionConfig>
                        {
                            new TransitionConditionConfig { parameterName = "Velocity_Z", mode = ConditionMode.Less, threshold = -0.1f }
                        }
                    },
                    // Fall → Idle when landing
                    new AnimationTransitionConfig
                    {
                        fromState = "Fall", toState = "Idle",
                        conditions = new List<TransitionConditionConfig>
                        {
                            new TransitionConditionConfig { parameterName = "OnGround", mode = ConditionMode.If }
                        }
                    },
                    // Any → Fire on trigger
                    new AnimationTransitionConfig
                    {
                        fromState = "Any", toState = "Fire",
                        conditions = new List<TransitionConditionConfig>
                        {
                            new TransitionConditionConfig { parameterName = "Fire", mode = ConditionMode.If }
                        }
                    },
                    // Fire → Idle after clip plays
                    new AnimationTransitionConfig
                    {
                        fromState = "Fire", toState = "Idle",
                        hasExitTime = true, exitTime = 1f
                    }
                }
            };
        }

        /// <summary>
        /// Helper: find a sprite sheet Texture2D by name in Assets/Images/.
        /// Returns null if not found (the generator will use the template's fallback spriteSheet).
        /// </summary>
        private static Texture2D FindSpriteSheet(string name)
        {
            var guids = AssetDatabase.FindAssets($"{name} t:Texture2D", new[] { "Assets/Images" });
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            return null;
        }
    }
}
#endif

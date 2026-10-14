#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Json;

namespace SickOfIt.Tools.Editor.GameConfig
{
    /// <summary>
    /// One-time conversion of the hand-coded enemies to data:
    ///   - each EnemyTypeConfig becomes content/entities/{type}.json, built from abilities that reproduce its C# behavior
    ///   - the enemy and laser projectiles become projectile entities
    ///   - each level's "enemies" list becomes a "script" with the same spawn times and the same win rules
    /// The legacy enemy classes stay in the project for levels that still use an enemies list.
    /// Safe to rerun: existing entity files are overwritten, levels that already have a script are left alone.
    /// </summary>
    public static class LegacyContentConverter
    {
        static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        [MenuItem("SickOfIt/Game Config/Convert Legacy Enemies to Entities", priority = 200)]
        static void ConvertMenu() => Convert();

        public static GameConfigReport Convert()
        {
            var report = new GameConfigReport("Convert", "legacy enemies and level spawn lists to entities and scripts");
            var definition = GameConfigSync.LoadDefinition();
            var ids = new EditorAssetIdResolver();
            string root = GameConfigLayout.AbsoluteRoot;

            var projectileIds = new Dictionary<string, string>();
            foreach (var projectile in definition.projectileConfigs)
            {
                if (projectile == null || projectile.id == "Player")
                    continue;
                string id = "projectile" + projectile.id;
                projectileIds[projectile.id] = id;
                Write(root, $"content/entities/{id}.json", ProjectileEntity(projectile, ids), report);
            }

            foreach (var enemy in definition.enemyTypes)
            {
                if (enemy == null)
                    continue;
                string id = enemy.type.ToString().ToLowerInvariant();
                Write(root, $"content/entities/{id}.json", EnemyEntity(enemy, ids, projectileIds, report), report);
            }

            var levels = GameConfigLayout.CollectionForField("levels");
            foreach (var level in definition.levels)
            {
                string file = Path.Combine(root, levels.FileFor(GameConfigLayout.SafeFileName(level.levelName)));
                if (!File.Exists(file))
                {
                    report.Issues.Warning(levels.FileFor(level.levelName), "", "Level file not found; skipped.");
                    continue;
                }
                var json = JObject.Parse(File.ReadAllText(file));
                if (json["script"] != null && json["script"].Type != JTokenType.Null)
                {
                    report.Notes.Add($"{level.levelName}: already has a script, left alone.");
                    continue;
                }
                if (level.enemies == null || level.enemies.Count == 0)
                    continue;
                json["script"] = LevelScript(level, report);
                json["enemies"] = new JArray();
                File.WriteAllText(file, JsonText.Format(json), Utf8NoBom);
                report.Notes.Add($"{level.levelName}: {level.enemies.Count} spawns moved into its script.");
            }

            report.Write();
            GameConfigSync.ImportNow("converted legacy enemies");
            return report;
        }

        static void Write(string root, string relative, JObject body, GameConfigReport report)
        {
            var file = new JObject { ["schemaVersion"] = GameConfigMigrations.CurrentVersion };
            foreach (var p in body.Properties())
                file.Add(p.Name, p.Value);
            ShortenFloats(file);
            string path = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonText.Format(file), Utf8NoBom);
            report.Notes.Add("Wrote " + relative);
        }

        // ─── Projectiles ──────────────────────────────────────────────

        static JObject ProjectileEntity(ProjectileConfig p, EditorAssetIdResolver ids)
        {
            float scale = p.scale > 0f ? p.scale : 0.15f;
            var visual = new JObject { ["scale"] = scale, ["layer"] = "Player", ["sortingOrder"] = SpriteLayers.Projectile, ["hurtFlash"] = false };
            var animator = SimpleAnimator(p.animationConfig, p.spriteSheet, p.animationFps > 0 ? p.animationFps : 12, ids);
            if (animator != null)
                visual["animator"] = animator;
            else if (p.sprite != null)
                visual["sprite"] = Id(p.sprite, ids, typeof(Sprite));

            var abilities = new JArray
            {
                new JObject { ["type"] = "projectile", ["damage"] = p.damage, ["hitSound"] = p.hitSound != null ? Id(p.hitSound, ids, typeof(AudioClip)) : null },
                new JObject { ["type"] = "lifetime", ["seconds"] = p.lifetime },
            };
            return new JObject
            {
                ["team"] = "Enemy",
                ["health"] = 0,
                ["body"] = new JObject { ["shape"] = "Circle", ["radius"] = p.colliderRadius > 0f ? p.colliderRadius : 1f, ["solid"] = false },
                ["visual"] = visual,
                ["healthBar"] = new JObject { ["show"] = false },
                ["abilities"] = abilities,
            };
        }

        // ─── Enemies ──────────────────────────────────────────────────

        static JObject EnemyEntity(EnemyTypeConfig c, EditorAssetIdResolver ids, Dictionary<string, string> projectileIds, GameConfigReport report)
        {
            float scale = c.spriteScale > 0f ? c.spriteScale : 0.15f;
            string tag = c.type.ToString().ToLowerInvariant();
            var abilities = new JArray { new JObject { ["type"] = "contactDamage", ["damage"] = c.contactDamage } };
            var states = new JArray();
            var initialStates = new JArray();
            bool facesLeft = false;

            switch (c.type)
            {
                case EnemyType.Chaser:
                    abilities.Add(new JObject { ["type"] = "chase", ["stopDistance"] = c.stopDistance, ["face"] = c.faceMovementDirection });
                    break;

                case EnemyType.Fighter:
                    // Wanders between walking at the player and standing, every 5-10 s; attacks when in range.
                    abilities.Add(new JObject
                    {
                        ["type"] = "melee", ["range"] = c.attackRange, ["damage"] = c.contactDamage, ["cooldown"] = c.attackCooldown,
                        ["window"] = 0.35f, ["hitEvent"] = "OnAttackFrame", ["animBool"] = "IsAttacking",
                    });
                    states.Add(new JObject
                    {
                        ["name"] = "walk",
                        ["abilities"] = new JArray { new JObject { ["type"] = "chase", ["stopDistance"] = 0f, ["face"] = c.faceMovementDirection, ["animBool"] = "IsWalking" } },
                        ["transitions"] = new JArray { new JObject { ["to"] = "idle", ["when"] = new JObject { ["type"] = "randomAfter", ["min"] = c.minStateChangeTime, ["max"] = c.maxStateChangeTime } } },
                    });
                    states.Add(new JObject
                    {
                        ["name"] = "idle",
                        ["onEnter"] = new JArray { new JObject { ["type"] = "setAnimParam", ["target"] = new JObject { ["self"] = true }, ["param"] = "IsWalking", ["value"] = 0f } },
                        ["transitions"] = new JArray { new JObject { ["to"] = "walk", ["when"] = new JObject { ["type"] = "randomAfter", ["min"] = c.minStateChangeTime, ["max"] = c.maxStateChangeTime } } },
                    });
                    initialStates.Add("walk");
                    initialStates.Add("idle");
                    break;

                case EnemyType.Shooter:
                    if (c.faceMovementDirection)
                        abilities.Add(new JObject { ["type"] = "faceTarget" });
                    abilities.Add(new JObject
                    {
                        ["type"] = "shoot",
                        ["projectile"] = ProjectileFor(projectileIds, "Enemy", report),
                        ["speed"] = c.projectileSpeed,
                        ["aim"] = "Player",
                        ["muzzle"] = Muzzle(c, scale, useBodyHeight: true),
                        ["interval"] = c.fireRate,
                        ["initialDelay"] = LegacyShooterFirstShot(c),
                        ["animTrigger"] = "IsAttacking",
                        ["fireOnEvent"] = "FireProjectile",
                        ["damage"] = c.projectileDamage,
                    });
                    break;

                case EnemyType.Sentinel:
                    // Crouches when the player is far; melee up close, laser from a crouch when very far.
                    facesLeft = true;
                    if (c.faceMovementDirection)
                        abilities.Add(new JObject { ["type"] = "faceTarget" });
                    abilities.Add(new JObject { ["type"] = "animParam", ["param"] = "IsCrouching", ["when"] = new JObject { ["type"] = "distanceToPlayer", ["above"] = c.crouchThreshold } });
                    abilities.Add(new JObject
                    {
                        ["type"] = "animParam", ["param"] = "IsAttacking",
                        ["when"] = new JObject
                        {
                            ["type"] = "anyOf",
                            ["triggers"] = new JArray
                            {
                                new JObject { ["type"] = "distanceToPlayer", ["below"] = c.attackRange },
                                new JObject { ["type"] = "distanceToPlayer", ["above"] = c.crouchingAttackRange },
                            },
                        },
                    });
                    abilities.Add(new JObject { ["type"] = "melee", ["autoAttack"] = false, ["hitEvent"] = "OnStandingAttackFrame", ["range"] = c.attackRange, ["damage"] = c.contactDamage });
                    abilities.Add(new JObject
                    {
                        ["type"] = "shoot",
                        ["projectile"] = ProjectileFor(projectileIds, "Laser", report),
                        ["speed"] = c.laserSpeed,
                        ["aim"] = "Player",
                        ["muzzle"] = Muzzle(c, scale, useBodyHeight: false),
                        ["interval"] = 0f,
                        ["fireOnEvent"] = "OnCrouchAttackFrame",
                        ["damage"] = c.projectileDamage,
                    });
                    break;
            }

            var visual = new JObject { ["scale"] = scale, ["facesLeft"] = facesLeft };
            var animator = c.animationConfig != null && c.animationConfig.states.Count > 0
                ? FullAnimator(c.animationConfig, c.spriteSheet, c.animationFps, ids)
                : SimpleAnimator(null, c.spriteSheet, c.animationFps > 0 ? c.animationFps : 8, ids);
            if (animator != null)
                visual["animator"] = animator;

            var entity = new JObject
            {
                ["team"] = "Enemy",
                ["health"] = c.defaultHealth,
                ["moveSpeed"] = c.defaultMoveSpeed,
                ["activationRange"] = c.detectionRange,
                ["tags"] = new JArray(tag),
                ["body"] = new JObject { ["shape"] = "Circle", ["radius"] = c.colliderRadius > 0f ? c.colliderRadius : 1f, ["solid"] = true, ["gravity"] = c.gravityScale },
                ["visual"] = visual,
                ["healthBar"] = new JObject
                {
                    // The old bar was a canvas of healthBarSize pixels at healthBarWorldScale, inside the scaled enemy.
                    ["offset"] = Vec(c.healthBarOffset * scale),
                    ["width"] = c.healthBarSize.x * c.healthBarWorldScale * scale,
                    ["height"] = c.healthBarSize.y * c.healthBarWorldScale * scale,
                    ["color"] = UnityJsonSerializer.ColorToken(c.healthBarColor),
                    ["trackColor"] = UnityJsonSerializer.ColorToken(c.healthBarTrackColor),
                },
                ["sounds"] = new JObject
                {
                    ["hurt"] = Clips(c.hurtSounds, ids),
                    ["death"] = Clips(c.deathSounds, ids),
                    ["hurtThreshold"] = c.hurtSoundHealthThreshold,
                    ["pitchVariation"] = c.pitchVariation,
                },
                ["loot"] = new JObject { ["chance"] = c.dropChance, ["count"] = c.dropCount, ["value"] = c.dropPointsValue },
                ["abilities"] = abilities,
            };
            if (c.hasInvincibilityFrames)
                entity["invincibilityTime"] = c.invincibilityDuration;
            if (states.Count > 0)
            {
                entity["states"] = states;
                entity["initialStates"] = initialStates;
            }
            return entity;
        }

        /// <summary>
        /// EnemyShooter schedules its first attack in Awake, before InitializeFromConfig sets the real fire rate,
        /// so the first shot comes at initialAttackDelay (5) - prefab attackInterval + fireRate. Keep what players see.
        /// </summary>
        static float LegacyShooterFirstShot(EnemyTypeConfig c)
        {
            float prefabInterval = c.fireRate;
            var shooter = c.prefab != null ? c.prefab.GetComponent<SickOfIt.Combat.Enemies.EnemyShooter>() : null;
            if (shooter != null)
            {
                var property = new SerializedObject(shooter).FindProperty("attackInterval");
                if (property != null)
                    prefabInterval = property.floatValue;
            }
            return Mathf.Max(0f, 5f - prefabInterval + c.fireRate);
        }

        static string ProjectileFor(Dictionary<string, string> ids, string legacyId, GameConfigReport report)
        {
            if (ids.TryGetValue(legacyId, out string id))
                return id;
            report.Issues.Warning("", "", $"No projectile config \"{legacyId}\"; shots will reference \"projectile{legacyId}\".");
            return "projectile" + legacyId;
        }

        /// <summary>The old shooters fired from fire points at ±firePointOffset (scaled); the Shooter also lifted shots to a quarter of its body height.</summary>
        static JToken Muzzle(EnemyTypeConfig c, float scale, bool useBodyHeight)
        {
            float x = Mathf.Abs(c.firePointOffset.x) * scale;
            float y = c.firePointOffset.y * scale;
            if (useBodyHeight)
            {
                var sprite = Generation.GeneratorHelper.GetFirstSprite(c.spriteSheet);
                if (sprite != null)
                    y = (sprite.bounds.center.y + sprite.bounds.extents.y * 0.25f) * scale;
            }
            return Vec(new Vector2(x, y));
        }

        // ─── Animation ────────────────────────────────────────────────

        static JObject FullAnimator(AnimationConfig config, Texture2D fallbackSheet, int fallbackFps, EditorAssetIdResolver ids)
        {
            var parameters = new JArray();
            foreach (var p in config.parameters)
                parameters.Add(new JObject { ["name"] = p.name, ["type"] = p.type.ToString(), ["defaultValue"] = p.defaultValue });

            var states = new JArray();
            foreach (var s in config.states)
            {
                var sheet = s.spriteSheet != null ? s.spriteSheet : fallbackSheet;
                var events = new JArray();
                foreach (var e in s.events)
                    events.Add(new JObject { ["frame"] = e.frame, ["functionName"] = e.functionName });
                var state = new JObject
                {
                    ["stateName"] = s.stateName,
                    ["spriteSheet"] = sheet != null ? Id(sheet, ids, typeof(Texture2D)) : null,
                    ["fps"] = s.fps > 0 ? s.fps : (fallbackFps > 0 ? fallbackFps : 12),
                    ["loop"] = s.loop,
                };
                if (!string.IsNullOrEmpty(s.spriteNameFilter))
                    state["spriteNameFilter"] = s.spriteNameFilter;
                if (!Mathf.Approximately(s.speed, 1f))
                    state["speed"] = s.speed;
                if (events.Count > 0)
                    state["events"] = events;
                states.Add(state);
            }

            var transitions = new JArray();
            foreach (var t in config.transitions)
            {
                var conditions = new JArray();
                foreach (var c in t.conditions)
                    conditions.Add(new JObject { ["parameterName"] = c.parameterName, ["mode"] = c.mode.ToString(), ["threshold"] = c.threshold });
                transitions.Add(new JObject
                {
                    ["fromState"] = t.fromState,
                    ["toState"] = t.toState,
                    ["hasExitTime"] = t.hasExitTime,
                    ["exitTime"] = t.exitTime,
                    ["conditions"] = conditions,
                });
            }

            return new JObject
            {
                ["parameters"] = parameters,
                ["states"] = states,
                ["transitions"] = transitions,
                ["defaultStateName"] = string.IsNullOrEmpty(config.defaultStateName) && config.states.Count > 0 ? config.states[0].stateName : config.defaultStateName,
            };
        }

        static JObject SimpleAnimator(AnimationConfig config, Texture2D sheet, int fps, EditorAssetIdResolver ids)
        {
            if (config != null && config.states.Count > 0)
                return FullAnimator(config, sheet, fps, ids);
            if (sheet == null)
                return null;
            return new JObject
            {
                ["states"] = new JArray { new JObject { ["stateName"] = "Idle", ["spriteSheet"] = Id(sheet, ids, typeof(Texture2D)), ["fps"] = fps, ["loop"] = true } },
                ["defaultStateName"] = "Idle",
            };
        }

        // ─── Levels ───────────────────────────────────────────────────

        static JObject LevelScript(LevelConfig level, GameConfigReport report)
        {
            // One rule per spawn time, in order, with every spawn of that moment.
            var rules = new JArray();
            foreach (var group in level.enemies.GroupBy(e => e.spawnTime).OrderBy(g => g.Key))
            {
                var actions = new JArray();
                foreach (var spawn in group)
                {
                    var action = new JObject
                    {
                        ["type"] = "spawn",
                        ["entity"] = spawn.type.ToString().ToLowerInvariant(),
                        ["at"] = Vec(spawn.position),
                        ["tags"] = new JArray("level"),
                    };
                    if (spawn.healthOverride > 0)
                        action["health"] = spawn.healthOverride;
                    if (spawn.speedOverride > 0f)
                        report.Issues.Warning($"levels/{level.levelName}.json", "", "speedOverride isn't converted; add a MoveSpeed modifier to the spawn.");
                    if (spawn.fireRateOverride > 0f || spawn.detectionRangeOverride > 0f || !Mathf.Approximately(spawn.initialAttackDelay, 5f) || !spawn.activeAtStart)
                        report.Issues.Warning($"levels/{level.levelName}.json", "", "A spawn uses an override that isn't converted (fire rate, detection range, attack delay, or inactive at start).");
                    actions.Add(action);
                }
                rules.Add(new JObject
                {
                    ["name"] = $"spawn at {group.Key:0.##}s",
                    ["when"] = new JObject { ["type"] = "atTime", ["seconds"] = group.Key },
                    ["do"] = actions,
                });
            }

            // Same win rules as before: every listed enemy killed, or the player reaches the win zone.
            var complete = new JObject
            {
                ["type"] = "anyOf",
                ["triggers"] = new JArray
                {
                    new JObject { ["type"] = "counter", ["name"] = "kills", ["atLeast"] = level.enemies.Count },
                    new JObject { ["type"] = "playerInZone", ["center"] = Vec(level.winZoneCenter), ["size"] = Vec(level.winZoneHalfSize * 2f) },
                },
            };

            return new JObject
            {
                ["objective"] = new JObject { ["complete"] = complete },
                ["phases"] = new JArray { new JObject { ["name"] = "main", ["rules"] = rules } },
            };
        }

        // ─── Helpers ──────────────────────────────────────────────────

        static JToken Id(UnityEngine.Object asset, EditorAssetIdResolver ids, Type type)
        {
            string id = ids.GetId(asset, type, out _);
            return id != null ? new JValue(id) : JValue.CreateNull();
        }

        static JArray Clips(AudioClip[] clips, EditorAssetIdResolver ids)
        {
            var array = new JArray();
            if (clips == null)
                return array;
            foreach (var clip in clips)
            {
                if (clip != null)
                    array.Add(Id(clip, ids, typeof(AudioClip)));
            }
            return array;
        }

        /// <summary>Values that came from C# floats print as 0.15, not 0.15000000596046448.</summary>
        static void ShortenFloats(JToken token)
        {
            if (token is JContainer container)
            {
                foreach (var child in container.Children().ToList())
                {
                    if (child is JProperty property)
                        ShortenFloats(property.Value);
                    else
                        ShortenFloats(child);
                }
                return;
            }
            if (token is JValue value && value.Type == JTokenType.Float)
            {
                double d = (double)value;
                if ((double)(float)d == d)
                    value.Replace(UnityJsonSerializer.FloatToken((float)d));
            }
        }

        static JArray Vec(Vector2 v) => new JArray(UnityJsonSerializer.FloatToken(v.x), UnityJsonSerializer.FloatToken(v.y));
    }
}
#endif

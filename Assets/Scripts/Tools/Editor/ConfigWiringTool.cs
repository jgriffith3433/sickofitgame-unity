#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Tools.Editor
{
    /// <summary>
    /// One-time tool to wire up sprite sheets and other config references.
    /// Run via SickOfIt → Wire Config References
    /// </summary>
    public static class ConfigWiringTool
    {
        [MenuItem("SickOfIt/Wire Config References")]
        public static void WireConfigReferences()
        {
            var gd = AssetDatabase.LoadAssetAtPath<GameDefinition>("Assets/Resources/GameDefinition.asset");
            if (gd == null)
            {
                Debug.LogError("GameDefinition.asset not found!");
                return;
            }

            var so = new SerializedObject(gd);
            int changesCount = 0;

            // Wire Player sprite sheet
            changesCount += WirePlayerConfig(so);

            // Wire Pickup config
            changesCount += WirePickupConfig(so);

            // Wire Projectile configs
            changesCount += WireProjectileConfigs(so);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(gd);

            // Wire Enemy Templates
            changesCount += WireEnemyTemplates();

            AssetDatabase.SaveAssets();
            Debug.Log($"[ConfigWiringTool] Wired {changesCount} references. Run 'SickOfIt → Generate Project' to regenerate prefabs.");
        }

        private static int WirePlayerConfig(SerializedObject so)
        {
            int count = 0;
            var playerProp = so.FindProperty("player");
            if (playerProp == null) return count;

            // Find player sprite sheet - use Demon1.png
            var spriteSheet = LoadTexture("Assets/Images/Demon1.png");
            if (spriteSheet != null)
            {
                var sheetProp = playerProp.FindPropertyRelative("spriteSheet");
                if (sheetProp != null && sheetProp.objectReferenceValue == null)
                {
                    sheetProp.objectReferenceValue = spriteSheet;
                    Debug.Log("[ConfigWiringTool] Assigned player spriteSheet: Demon1.png");
                    count++;
                }
            }

            return count;
        }

        private static int WirePickupConfig(SerializedObject so)
        {
            int count = 0;
            var pickupProp = so.FindProperty("pickupConfig");
            if (pickupProp == null) return count;

            var spriteSheet = LoadTexture("Assets/Images/Diamond.png");
            if (spriteSheet != null)
            {
                var sheetProp = pickupProp.FindPropertyRelative("spriteSheet");
                if (sheetProp != null && sheetProp.objectReferenceValue == null)
                {
                    sheetProp.objectReferenceValue = spriteSheet;
                    Debug.Log("[ConfigWiringTool] Assigned pickup spriteSheet: Diamond.png");
                    count++;
                }

                // Also assign sprite if empty
                var spriteProp = pickupProp.FindPropertyRelative("sprite");
                if (spriteProp != null && spriteProp.objectReferenceValue == null)
                {
                    var sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Images/Diamond.png");
                    foreach (var s in sprites)
                    {
                        if (s is Sprite sprite)
                        {
                            spriteProp.objectReferenceValue = sprite;
                            Debug.Log("[ConfigWiringTool] Assigned pickup sprite: Diamond");
                            count++;
                            break;
                        }
                    }
                }
            }

            return count;
        }

        private static int WireProjectileConfigs(SerializedObject so)
        {
            int count = 0;
            var projectilesProp = so.FindProperty("projectileConfigs");
            if (projectilesProp == null) return count;

            var fireballSheet = LoadTexture("Assets/Images/Fireball.png");
            if (fireballSheet == null) return count;

            // Check if array is empty - need to add configs
            if (projectilesProp.arraySize == 0)
            {
                // Add 3 projectile configs: Player, Enemy, Laser
                projectilesProp.arraySize = 3;

                // Player projectile
                var playerProj = projectilesProp.GetArrayElementAtIndex(0);
                SetProjectileConfig(playerProj, "Player", fireballSheet, "Enemy", 10f, 2f, 1, 20);
                Debug.Log("[ConfigWiringTool] Added Player projectile config");
                count++;

                // Enemy projectile
                var enemyProj = projectilesProp.GetArrayElementAtIndex(1);
                SetProjectileConfig(enemyProj, "Enemy", fireballSheet, "Player", 8f, 2f, 1, 20);
                Debug.Log("[ConfigWiringTool] Added Enemy projectile config");
                count++;

                // Laser projectile
                var laserProj = projectilesProp.GetArrayElementAtIndex(2);
                SetProjectileConfig(laserProj, "Laser", fireballSheet, "Player", 12f, 1.5f, 1, 10);
                Debug.Log("[ConfigWiringTool] Added Laser projectile config");
                count++;
            }
            else
            {
                // Wire sprite sheets to existing configs if missing
                for (int i = 0; i < projectilesProp.arraySize; i++)
                {
                    var proj = projectilesProp.GetArrayElementAtIndex(i);
                    var sheetProp = proj.FindPropertyRelative("spriteSheet");
                    if (sheetProp != null && sheetProp.objectReferenceValue == null)
                    {
                        sheetProp.objectReferenceValue = fireballSheet;
                        count++;
                    }
                }
            }

            return count;
        }

        private static void SetProjectileConfig(SerializedProperty prop, string id, Texture2D spriteSheet, string targetTag, float speed, float lifetime, int damage, int poolSize)
        {
            prop.FindPropertyRelative("id")?.SetValue(id);
            prop.FindPropertyRelative("spriteSheet").objectReferenceValue = spriteSheet;
            prop.FindPropertyRelative("speed").floatValue = speed;
            prop.FindPropertyRelative("lifetime").floatValue = lifetime;
            prop.FindPropertyRelative("damage").intValue = damage;
            prop.FindPropertyRelative("poolSize").intValue = poolSize;
            
            var targetTagsProp = prop.FindPropertyRelative("targetTags");
            if (targetTagsProp != null)
            {
                targetTagsProp.arraySize = 1;
                targetTagsProp.GetArrayElementAtIndex(0).stringValue = targetTag;
            }
        }

        private static int WireEnemyTemplates()
        {
            int count = 0;
            string[] templatePaths = {
                "Assets/Resources/EnemyTemplates/Chaser.asset",
                "Assets/Resources/EnemyTemplates/Fighter.asset",
                "Assets/Resources/EnemyTemplates/Sentinel.asset",
                "Assets/Resources/EnemyTemplates/Shooter.asset"
            };

            foreach (var path in templatePaths)
            {
                var template = AssetDatabase.LoadAssetAtPath<EnemyTemplate>(path);
                if (template == null) continue;

                var so = new SerializedObject(template);
                var sheetProp = so.FindProperty("spriteSheet");
                
                if (sheetProp != null && sheetProp.objectReferenceValue == null)
                {
                    Texture2D sheet = GetSpriteSheetForEnemy(template.type);
                    if (sheet != null)
                    {
                        sheetProp.objectReferenceValue = sheet;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(template);
                        Debug.Log($"[ConfigWiringTool] Assigned {template.type} spriteSheet: {sheet.name}");
                        count++;
                    }
                }
            }

            return count;
        }

        private static Texture2D GetSpriteSheetForEnemy(EnemyType type)
        {
            return type switch
            {
                EnemyType.Chaser => LoadTexture("Assets/Art/Sprites/Enemies/slime_idle.png") 
                                    ?? LoadTexture("Assets/Images/Demon1.png"),
                EnemyType.Fighter => LoadTexture("Assets/Images/FighterIdle.png"),
                EnemyType.Sentinel => LoadTexture("Assets/Images/Level3Enemy_StandingIdle.png"),
                EnemyType.Shooter => LoadTexture("Assets/Images/Demon2.png"),
                _ => null
            };
        }

        private static Texture2D LoadTexture(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }

    // Extension to set string value on SerializedProperty
    public static class SerializedPropertyExtensions
    {
        public static void SetValue(this SerializedProperty prop, string value)
        {
            if (prop != null && prop.propertyType == SerializedPropertyType.String)
                prop.stringValue = value;
        }
    }
}
#endif

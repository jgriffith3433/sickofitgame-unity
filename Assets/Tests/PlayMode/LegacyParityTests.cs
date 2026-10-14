using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SickOfIt.Combat.Enemies;
using SickOfIt.Combat.Health;
using SickOfIt.Core;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Gameplay;

namespace SickOfIt.Tests.PlayMode
{
    /// <summary>
    /// The converted entities (content/entities) against the original C# enemies, run side by side from the
    /// same starting point: same movement and the same attack timing.
    /// </summary>
    public class LegacyParityTests
    {
        TestLevel level;
        readonly System.Collections.Generic.List<Object> extra = new System.Collections.Generic.List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in extra)
            {
                if (o != null)
                    Object.Destroy(o);
            }
            extra.Clear();
            level?.Destroy();
            level = null;
        }

        static EnemyBase SpawnLegacy(EnemyType type, Vector2 at, TestLevel level, out GameObject go)
        {
            var template = GameDefinition.Instance.GetEnemyType(type);
            go = Object.Instantiate(template.prefab, at, Quaternion.identity);
            var enemy = go.GetComponent<EnemyBase>();
            enemy.InitializeFromConfig(template, new EnemySpawnConfig { type = type, position = at });
            go.GetComponent<EnemyHealth>().SetMaxHealth(template.defaultHealth);
            enemy.SetPlayer(level.PlayerObject.transform, level.PlayerHealth);
            return enemy;
        }

        [UnityTest]
        public IEnumerator ChaserMovesLikeTheOriginal()
        {
            level = TestLevel.Create(GameContent.FromDefinition(GameDefinition.Instance));
            level.PlayerHealth.SetInvincibilityTimer(999f);
            SpawnLegacy(EnemyType.Chaser, new Vector2(5f, 0.5f), level, out var legacy);
            extra.Add(legacy);
            var entity = level.World.Spawn("chaser", new Vector2(5f, 0.5f), new SpawnOptions());

            // Compare the approach only: the original stopped 1.5 away, the chaser now rams the player and bursts.
            for (float t = 0.5f; t <= 1.5f; t += 0.5f)
            {
                yield return level.Until(t);
                Assert.That(Vector2.Distance(legacy.transform.position, entity.Position), Is.LessThan(0.1f),
                    $"at {t}s the original is at {legacy.transform.position}, the entity at {entity.Position}");
            }
        }

        [UnityTest]
        public IEnumerator ShooterFiresOnTheSameSchedule()
        {
            level = TestLevel.Create(GameContent.FromDefinition(GameDefinition.Instance), playerAt: new Vector2(-3f, 0f));
            level.PlayerHealth.SetInvincibilityTimer(999f);

            var projectile = GameDefinition.Instance.GetProjectileConfig("Enemy");
            var poolGo = new GameObject("LegacyEnemyShots");
            extra.Add(poolGo);
            var pool = poolGo.AddComponent<ObjectPool>();
            var projectilePrefab = UnityEditorPrefab("Assets/Prefabs/Combat/Projectile_Enemy.prefab");
            Assume.That(projectilePrefab, Is.Not.Null);
            pool.Configure(projectilePrefab, 4);

            var shooter = (EnemyShooter)SpawnLegacy(EnemyType.Shooter, new Vector2(3f, 1f), level, out var legacy);
            extra.Add(legacy);
            shooter.SetProjectilePool(pool);
            level.World.Spawn("shooter", new Vector2(3f, -1f), new SpawnOptions());

            float legacyFirst = -1f;
            float entityFirst = -1f;
            float started = Time.realtimeSinceStartup;
            while ((legacyFirst < 0f || entityFirst < 0f) && Time.realtimeSinceStartup - started < 20f)
            {
                if (legacyFirst < 0f && pool.ActiveCount > 0)
                    legacyFirst = level.Clock.Time;
                if (entityFirst < 0f && level.World.GetCounter("spawned:projectileEnemy") > 0f)
                    entityFirst = level.Clock.Time;
                yield return null;
            }

            Assert.That(legacyFirst, Is.GreaterThan(0f), "the original shooter fired");
            Assert.That(entityFirst, Is.GreaterThan(0f), "the entity shooter fired");
            Assert.That(Mathf.Abs(legacyFirst - entityFirst), Is.LessThan(0.2f), $"first shot: original {legacyFirst:0.00}s, entity {entityFirst:0.00}s");
        }

        static GameObject UnityEditorPrefab(string path)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
            return null;
#endif
        }
    }
}

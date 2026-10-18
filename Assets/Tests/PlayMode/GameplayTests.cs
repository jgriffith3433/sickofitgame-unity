using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SickOfIt.Gameplay;

namespace SickOfIt.Tests.PlayMode
{
    public class GameplayTests
    {
        TestLevel level;

        [TearDown]
        public void TearDown()
        {
            level?.Destroy();
            level = null;
            Time.timeScale = 1f;
        }

        static readonly Dictionary<string, string> Basics = new Dictionary<string, string>
        {
            ["entities/dummy.json"] = "{ 'schemaVersion': 1, 'health': 3, 'moveSpeed': 2 }",
            ["entities/chaser.json"] = "{ 'schemaVersion': 1, 'health': 3, 'moveSpeed': 2, 'abilities': [ { 'type': 'chase', 'stopDistance': 0.2 } ] }",
            ["entities/bullet.json"] = "{ 'schemaVersion': 1, 'health': 0, 'body': { 'radius': 0.1, 'solid': false }, 'healthBar': { 'show': false }, " +
                                       "'abilities': [ { 'type': 'projectile', 'damage': 1 }, { 'type': 'lifetime', 'seconds': 3 } ] }",
            ["entities/turret.json"] = "{ 'schemaVersion': 1, 'health': 3, 'abilities': [ { 'type': 'shoot', 'projectile': 'bullet', 'speed': 8, 'interval': 0.4, 'initialDelay': 0.1 } ] }",
            ["entities/wanderer.json"] = "{ 'schemaVersion': 1, 'health': 2, 'abilities': [ { 'type': 'wander', 'radius': 1.5, 'speed': 1 } ] }",
            ["entities/bomber.json"] = "{ 'schemaVersion': 1, 'health': 3, 'moveSpeed': 3, 'abilities': [ { 'type': 'chase', 'stopDistance': 0.1 }, " +
                                       "{ 'type': 'explodeNear', 'triggerRadius': 1.0, 'fuse': 0.3, 'blastRadius': 1.5, 'damage': 1 } ], " +
                                       "'onDeath': [ { 'type': 'spawn', 'entity': 'sludge', 'anchor': 'Self' } ] }",
            ["entities/sludge.json"] = "{ 'schemaVersion': 1, 'team': 'Enemy', 'health': 0, 'body': { 'solid': false, 'radius': 0.5 }, " +
                                       "'abilities': [ { 'type': 'aura', 'radius': 1.2, 'damagePerTick': 1, 'interval': 0.5 }, { 'type': 'lifetime', 'seconds': 4 } ] }",
            ["effects/slow.json"] = "{ 'schemaVersion': 1, 'duration': 1, 'modifiers': [ { 'stat': 'MoveSpeed', 'multiply': 0.25 } ] }",
        };

        [UnityTest]
        public IEnumerator ChaserMovesTowardThePlayer()
        {
            level = TestLevel.Create(TestLevel.BuildContent(Basics));
            var chaser = level.World.Spawn("chaser", new Vector2(4f, 0f), new SpawnOptions());
            Assert.That(chaser, Is.Not.Null);
            yield return level.Until(1f);

            float x = chaser.Position.x;
            Assert.That(x, Is.LessThan(2.8f).And.GreaterThan(1.2f), "about 2 units per second toward the player");
            Assert.That(chaser.Facing, Is.EqualTo(-1f));
        }

        [UnityTest]
        public IEnumerator SlowStatusReducesSpeedThenExpires()
        {
            level = TestLevel.Create(TestLevel.BuildContent(Basics));
            var chaser = level.World.Spawn("chaser", new Vector2(6f, 0f), new SpawnOptions());
            chaser.ApplyStatus("slow", null);
            yield return level.Until(0.9f);
            float slowed = 6f - chaser.Position.x;
            yield return level.Until(1.9f);
            float normal = 6f - chaser.Position.x - slowed;

            Assert.That(slowed, Is.LessThan(0.8f), "a quarter speed while slowed");
            Assert.That(normal, Is.GreaterThan(1.4f), "full speed after the status ends");
        }

        [UnityTest]
        public IEnumerator TurretShotsHurtThePlayer()
        {
            level = TestLevel.Create(TestLevel.BuildContent(Basics));
            int startHealth = level.PlayerHealth.CurrentHealth;
            level.World.Spawn("turret", new Vector2(3f, 0f), new SpawnOptions());
            yield return level.Until(1.2f);

            Assert.That(level.World.GetCounter("spawned:bullet"), Is.GreaterThanOrEqualTo(2f));
            Assert.That(level.PlayerHealth.CurrentHealth, Is.LessThan(startHealth), "shots aimed at the player hit it");
        }

        [UnityTest]
        public IEnumerator BomberExplodesAndLeavesSludgeThatHurtsOverTime()
        {
            level = TestLevel.Create(TestLevel.BuildContent(Basics));
            level.PlayerHealth.SetInvincibilityTimer(0f);
            int startHealth = level.PlayerHealth.CurrentHealth;
            level.World.Spawn("bomber", new Vector2(2.5f, 0f), new SpawnOptions());

            yield return level.UntilCondition(() => level.World.GetCounter("spawned:sludge") >= 1f, 5f);
            Assert.That(level.Alive("bomber"), Is.Empty, "the bomber dies when it explodes");
            Assert.That(level.World.GetCounter("kills"), Is.EqualTo(0f), "exploding isn't a kill");
            int afterBlast = level.PlayerHealth.CurrentHealth;
            Assert.That(afterBlast, Is.LessThan(startHealth), "the blast hurts");

            float t = level.Clock.Time;
            yield return level.Until(t + 0.8f);
            Assert.That(level.PlayerHealth.CurrentHealth, Is.LessThan(afterBlast), "standing in sludge keeps hurting, despite hit invincibility");
        }

        [UnityTest]
        public IEnumerator SceneCameraFadeAndRewindLockAreRewound()
        {
            const string script = @"{
                'phases': [ { 'name': 'scene', 'onEnter': [
                    { 'type': 'lockRewind' },
                    { 'type': 'cameraTo', 'at': [1, 0.5], 'size': 2, 'duration': 0.5 },
                    { 'type': 'fade', 'to': 'Black', 'duration': 0.5 } ] } ]
            }";
            level = TestLevel.Create(TestLevel.BuildContent(Basics, script));
            var camera = level.World.Camera;
            Assert.That(level.World.Effects.HomeSize, Is.EqualTo(4f));
            yield return level.Until(0.8f);
            Assert.That(camera.orthographicSize, Is.EqualTo(2f).Within(0.01f));
            Assert.That(Vector2.Distance(camera.transform.position, new Vector2(1f, 0.5f)), Is.LessThan(0.01f));
            Assert.That(level.World.Hud.FadeAlpha, Is.EqualTo(1f).Within(0.001f));
            Assert.That(SickOfIt.Core.Rewind.RewindInput.Locked, Is.True);

            // A rewind replays the moves: back near the start the camera is wide again and the screen nearly clear.
            yield return level.RewindTo(0.1f);
            Assert.That(camera.orthographicSize, Is.GreaterThan(3.4f));
            Assert.That(level.World.Hud.FadeAlpha, Is.LessThan(0.35f));
            Assert.That(SickOfIt.Core.Rewind.RewindInput.Locked, Is.True);
            yield return level.Until(1f);
            Assert.That(camera.orthographicSize, Is.EqualTo(2f).Within(0.01f));

            level.World.SetRewindLocked(false);
            Assert.That(SickOfIt.Core.Rewind.RewindInput.Locked, Is.False);
        }

        [UnityTest]
        public IEnumerator ScriptRunsPhasesAndCompletesOnBossDeath()
        {
            const string script = @"{
                'objective': { 'complete': { 'type': 'allDead', 'target': { 'id': 'boss' } } },
                'phases': [
                    { 'name': 'waves',
                      'onEnter': [ { 'type': 'spawn', 'entity': 'dummy', 'at': [2, 0], 'count': 2, 'tags': ['wave'] } ],
                      'rules': [ { 'when': { 'type': 'allDead', 'target': { 'tag': 'wave' } }, 'do': [ { 'type': 'goToPhase', 'phase': 'boss' } ] } ] },
                    { 'name': 'boss',
                      'onEnter': [ { 'type': 'spawn', 'entity': 'dummy', 'id': 'boss', 'at': [3, 0], 'health': 10 },
                                   { 'type': 'bossBar', 'target': { 'id': 'boss' }, 'label': 'Boss' } ] }
                ]
            }";
            level = TestLevel.Create(TestLevel.BuildContent(Basics, script));
            yield return level.Until(0.2f);
            Assert.That(level.World.Director.PhaseName, Is.EqualTo("waves"));
            Assert.That(level.Alive("dummy").Count, Is.EqualTo(2));

            foreach (var e in level.Alive("dummy"))
                e.Damage(99, null);
            yield return level.Until(level.Clock.Time + 0.1f);
            Assert.That(level.World.Director.PhaseName, Is.EqualTo("boss"));
            var boss = level.Alive().Single(e => e.InstanceId == "boss");
            Assert.That(boss.MaxHealth, Is.EqualTo(10));
            Assert.That(level.Completed, Is.False);

            boss.Damage(99, null);
            yield return level.Until(level.Clock.Time + 0.1f);
            Assert.That(level.Completed, Is.True);
            Assert.That(level.World.GetCounter("kills"), Is.EqualTo(3f));
        }

        [UnityTest]
        public IEnumerator WaveScalingMakesLaterWavesTougher()
        {
            const string script = @"{
                'rules': [ { 'when': { 'type': 'every', 'seconds': 0.3, 'maxTimes': 3 },
                             'do': [ { 'type': 'spawnWave', 'entity': 'dummy', 'count': 1, 'anchor': 'World', 'areaSize': [0, 0],
                                       'modifiers': [ { 'stat': 'MaxHealth', 'multiply': 1, 'multiplyPerRepeat': 1 } ] } ] } ]
            }";
            level = TestLevel.Create(TestLevel.BuildContent(Basics, script));
            yield return level.Until(1f);
            var health = level.Alive("dummy").Select(e => e.MaxHealth).ToList();
            Assert.That(health, Is.EqualTo(new[] { 3, 6, 9 }));
        }

        [UnityTest]
        public IEnumerator RewindReplaysTheSameFuture()
        {
            const string script = @"{
                'rules': [ { 'when': { 'type': 'every', 'seconds': 0.25, 'maxTimes': 8 },
                             'do': [ { 'type': 'spawnWave', 'entity': 'wanderer', 'count': 2, 'anchor': 'World', 'areaCenter': [0, 1], 'areaSize': [8, 3] } ] } ]
            }";
            level = TestLevel.Create(TestLevel.BuildContent(Basics, script));
            yield return level.Until(1.6f);
            var first = level.Alive("wanderer").ToDictionary(e => e.Serial, e => e.Position);
            float spawnedFirst = level.World.GetCounter("spawned");

            yield return level.RewindTo(0.6f);
            Assert.That(level.Clock.Time, Is.LessThanOrEqualTo(0.65f));
            Assert.That(level.World.GetCounter("spawned"), Is.LessThan(spawnedFirst), "counters went back with time");

            yield return level.Until(1.6f);
            var second = level.Alive("wanderer").ToDictionary(e => e.Serial, e => e.Position);

            Assert.That(level.World.GetCounter("spawned"), Is.EqualTo(spawnedFirst));
            Assert.That(second.Keys, Is.EquivalentTo(first.Keys), "the same entities exist after replaying");
            foreach (var serial in first.Keys)
                Assert.That(Vector2.Distance(first[serial], second[serial]), Is.LessThan(0.35f), $"entity {serial} spawned at the same random place");
        }

        [UnityTest]
        public IEnumerator RewindRestoresHealthAndBringsTheDeadBack()
        {
            level = TestLevel.Create(TestLevel.BuildContent(Basics));
            var dummy = level.World.Spawn("dummy", new Vector2(2f, 0f), new SpawnOptions());
            yield return level.Until(0.5f);
            dummy.Damage(1, null);
            yield return level.Until(1.0f);
            dummy.Damage(99, null);
            yield return level.Until(1.3f);
            Assert.That(dummy.IsAlive, Is.False);
            Assert.That(level.World.GetCounter("kills"), Is.EqualTo(1f));

            yield return level.RewindTo(0.7f);
            Assert.That(dummy.IsAlive, Is.True, "rewinding before the kill brings it back");
            Assert.That(dummy.gameObject.activeSelf, Is.True);
            Assert.That(dummy.Health, Is.EqualTo(2), "with the health it had then");
            Assert.That(level.World.GetCounter("kills"), Is.EqualTo(0f));

            yield return level.Until(1.5f);
            Assert.That(dummy.IsAlive, Is.True, "the kill happened in the discarded future and doesn't replay");
        }

        [UnityTest]
        public IEnumerator RewindBeforeCompletionCancelsIt()
        {
            const string script = @"{ 'objective': { 'complete': { 'type': 'atTime', 'seconds': 0.8 } } }";
            level = TestLevel.Create(TestLevel.BuildContent(Basics, script));
            yield return level.Until(1.0f);
            Assert.That(level.Completed, Is.True);
            yield return level.RewindTo(0.5f);
            Assert.That(level.Completed, Is.False);
            Assert.That(level.CancelledCompletions, Is.EqualTo(1));
        }

        /// <summary>
        /// Levels load additively: the level starts while the menu is still the active scene, then the menu
        /// unloads. The world and everything it spawns must live in the level's scene and survive that.
        /// </summary>
        [UnityTest]
        public IEnumerator WorldSurvivesTheMenuSceneUnloading()
        {
            var menu = UnityEngine.SceneManagement.SceneManager.CreateScene("FakeMenu");
            var levelScene = UnityEngine.SceneManagement.SceneManager.CreateScene("FakeLevel");
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(menu);

            level = TestLevel.Create(TestLevel.BuildContent(Basics), scene: levelScene);
            Assert.That(level.World.gameObject.scene, Is.EqualTo(levelScene));
            var early = level.World.Spawn("dummy", new Vector2(2f, 0f), new SpawnOptions());
            Assert.That(early.gameObject.scene, Is.EqualTo(levelScene), "spawned while the menu is active, still in the level");

            UnityEngine.SceneManagement.SceneManager.SetActiveScene(levelScene);
            yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(menu);
            yield return level.Until(0.5f);

            Assert.That(level.World != null, Is.True, "the world outlives the menu scene");
            Assert.That(early != null && early.IsAlive, Is.True);
            level.Destroy();
            level = null;
            yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(levelScene);
        }

        [UnityTest]
        public IEnumerator ScrollCameraMovesAndRewindsWithTime()
        {
            const string script = @"{ 'world': { 'camera': 'Scroll', 'scrollSpeed': [1, 0] } }";
            level = TestLevel.Create(TestLevel.BuildContent(Basics, script));
            yield return level.Until(1.5f);
            float x = level.World.CameraCenter.x;
            Assert.That(x, Is.InRange(1.2f, 1.8f), "scrolls about 1 unit per second");
            Assert.That(Camera.main.transform.position.x, Is.EqualTo(x).Within(0.05f));

            yield return level.RewindTo(0.5f);
            Assert.That(level.World.CameraCenter.x, Is.LessThan(0.8f), "the camera goes back with time");
        }

        [UnityTest]
        public IEnumerator FollowCameraTracksThePlayerInsideBounds()
        {
            const string script = @"{ 'world': { 'camera': 'Follow', 'boundsCenter': [0, 0], 'boundsSize': [20, 8], 'followSharpness': 20 } }";
            level = TestLevel.Create(TestLevel.BuildContent(Basics, script));
            level.PlayerObject.GetComponent<Rigidbody2D>().position = new Vector2(3f, 0f);
            level.PlayerObject.transform.position = new Vector3(3f, 0f, 0f);
            yield return level.Until(0.8f);
            Assert.That(level.World.CameraCenter.x, Is.EqualTo(3f).Within(0.2f));

            level.PlayerObject.GetComponent<Rigidbody2D>().position = new Vector2(9f, 0f);
            level.PlayerObject.transform.position = new Vector3(9f, 0f, 0f);
            yield return level.Until(1.6f);
            float halfWidth = Camera.main.orthographicSize * Camera.main.aspect;
            Assert.That(level.World.CameraCenter.x, Is.EqualTo(10f - halfWidth).Within(0.2f), "stops at the edge of the bounds");
        }

        [UnityTest]
        public IEnumerator HazardRainFallsAndStopsWithItsPhase()
        {
            const string script = @"{
                'phases': [
                    { 'name': 'rain',
                      'onEnter': [ { 'type': 'startHazard', 'id': 'fire', 'projectile': 'bullet', 'pattern': { 'type': 'rain', 'count': 2 }, 'speed': 3, 'interval': 0.2 } ],
                      'rules': [ { 'when': { 'type': 'after', 'seconds': 1 }, 'do': [ { 'type': 'goToPhase', 'phase': 'calm' } ] } ] },
                    { 'name': 'calm' }
                ]
            }";
            level = TestLevel.Create(TestLevel.BuildContent(Basics, script));
            yield return level.Until(0.9f);
            float during = level.World.GetCounter("spawned:bullet");
            Assert.That(during, Is.GreaterThanOrEqualTo(8f), "about 2 shots every 0.2 s");
            var bullet = level.Alive("bullet").First();
            Assert.That(bullet.Body.linearVelocity.y, Is.LessThan(-2f), "rain falls");

            yield return level.Until(1.6f);
            float after = level.World.GetCounter("spawned:bullet");
            yield return level.Until(2.2f);
            Assert.That(level.World.GetCounter("spawned:bullet"), Is.EqualTo(after), "the hazard stopped when its phase ended");
        }
    }
}

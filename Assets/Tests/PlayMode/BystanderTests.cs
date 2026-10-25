using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SickOfIt.Gameplay;

namespace SickOfIt.Tests.PlayMode
{
    /// <summary>Scenes that direct other entities (walkEntityTo, faceEntity) and the witness trigger, with rewind.</summary>
    public class BystanderTests
    {
        TestLevel level;

        [TearDown]
        public void TearDown()
        {
            level?.Destroy();
            level = null;
            Time.timeScale = 1f;
        }

        static readonly Dictionary<string, string> Files = new Dictionary<string, string>
        {
            // A neutral bystander that reports what it notices by emitting a signal per kind.
            ["entities/watcher.json"] = "{ 'schemaVersion': 1, 'team': 'Neutral', 'health': 3, 'body': { 'solid': false, 'radius': 0.3 }, 'healthBar': { 'show': false }, 'abilities': [ " +
                                        "{ 'type': 'react', 'maxTimes': 0, 'when': { 'type': 'witness', 'what': 'ShotPassed', 'within': 3 }, 'do': [ { 'type': 'emit', 'signal': 'saw:shot' } ] }, " +
                                        "{ 'type': 'react', 'maxTimes': 0, 'when': { 'type': 'witness', 'what': 'AllyHurt', 'within': 6 }, 'do': [ { 'type': 'emit', 'signal': 'saw:ally' } ] }, " +
                                        "{ 'type': 'react', 'maxTimes': 0, 'when': { 'type': 'witness', 'what': 'HeroHurt', 'within': 6 }, 'do': [ { 'type': 'emit', 'signal': 'saw:hero' } ] } ] }",
            ["entities/ally.json"] = "{ 'schemaVersion': 1, 'team': 'Neutral', 'health': 3, 'body': { 'solid': false, 'radius': 0.3 }, 'healthBar': { 'show': false } }",
            ["entities/bullet.json"] = "{ 'schemaVersion': 1, 'health': 0, 'body': { 'radius': 0.1, 'solid': false }, 'healthBar': { 'show': false }, " +
                                       "'abilities': [ { 'type': 'projectile', 'damage': 1 }, { 'type': 'lifetime', 'seconds': 3 } ] }",
        };

        const string Script = @"{
            'phases': [ { 'name': 'bench',
                'onEnter': [
                    { 'type': 'spawn', 'entity': 'watcher', 'id': 'w', 'at': [2, 0], 'facePlayer': false },
                    { 'type': 'spawn', 'entity': 'ally', 'id': 'a', 'at': [4, 0] } ],
                'rules': [
                    { 'when': { 'type': 'signal', 'name': 'go' }, 'do': [ { 'type': 'walkEntityTo', 'target': 'w', 'x': 5, 'speed': 2, 'emit': 'arrived' } ] },
                    { 'when': { 'type': 'signal', 'name': 'arrived' }, 'do': [ { 'type': 'addCounter', 'name': 'arrivals' } ] },
                    { 'when': { 'type': 'signal', 'name': 'turn' }, 'do': [ { 'type': 'faceEntity', 'target': 'w', 'toward': 'player' } ] },
                    { 'when': { 'type': 'signal', 'name': 'away' }, 'do': [ { 'type': 'faceEntity', 'target': 'w', 'awayFrom': 'player' } ] },
                    { 'when': { 'type': 'signal', 'name': 'saw:shot' }, 'do': [ { 'type': 'addCounter', 'name': 'shots' } ] },
                    { 'when': { 'type': 'signal', 'name': 'saw:ally' }, 'do': [ { 'type': 'addCounter', 'name': 'allies' } ] },
                    { 'when': { 'type': 'signal', 'name': 'saw:hero' }, 'do': [ { 'type': 'addCounter', 'name': 'heroes' } ] }
                ] } ]
        }";

        static Entity Find(TestLevel l, string id)
        {
            foreach (var e in l.World.Live)
            {
                if (e.IsAlive && e.InstanceId == id)
                    return e;
            }
            return null;
        }

        [UnityTest]
        public IEnumerator ASceneWalksAndTurnsAnotherEntityAndRewindsIt()
        {
            level = TestLevel.Create(TestLevel.BuildContent(Files, Script));
            yield return level.Until(0.3f);
            var w = Find(level, "w");
            Assert.That(w, Is.Not.Null);
            Assert.That(w.Facing, Is.EqualTo(1f), "spawned facing right");
            level.World.Emit("away");
            level.World.Emit("go");
            yield return level.Until(1.0f);
            Assert.That(w.ScriptWalking, Is.True);
            Assert.That(w.Position.x, Is.GreaterThan(2.5f).And.LessThan(4.5f), "on its way");
            float midway = level.Clock.Time;

            yield return level.Until(2.8f);
            Assert.That(w.ScriptWalking, Is.False, "it stopped");
            Assert.That(w.Position.x, Is.EqualTo(5f).Within(0.15f), "at the x the scene gave");
            Assert.That(level.World.GetCounter("arrivals"), Is.EqualTo(1f), "and emitted its signal once");

            level.World.Emit("turn");
            yield return level.Until(level.Clock.Time + 0.2f);
            Assert.That(w.Facing, Is.EqualTo(-1f), "faceEntity toward the hero (he is to its left)");
            level.World.Emit("away");
            yield return level.Until(level.Clock.Time + 0.2f);
            Assert.That(w.Facing, Is.EqualTo(1f), "faceEntity away from the hero");

            // A rewind back to mid-walk restores the walk, its signal and its place.
            yield return level.RewindTo(midway);
            Assert.That(w.Position.x, Is.LessThan(4.5f), "back on the way");
            Assert.That(w.ScriptWalking, Is.True, "the walk was still going then");
            Assert.That(level.World.GetCounter("arrivals"), Is.EqualTo(0f), "it had not arrived yet");
            yield return level.Until(midway + 2.2f);
            Assert.That(w.Position.x, Is.EqualTo(5f).Within(0.15f), "the walk finishes again");
            Assert.That(level.World.GetCounter("arrivals"), Is.EqualTo(1f), "and arrives once, not twice");
        }

        [UnityTest]
        public IEnumerator AShotPassingIsNoticedByAnEntityOfAnotherTeam()
        {
            level = TestLevel.Create(TestLevel.BuildContent(Files, Script));
            yield return level.Until(0.3f);
            Assert.That(level.World.GetCounter("shots"), Is.EqualTo(0f));
            // A shot that crosses the bystander's x at 1.5 above it (within 3), flying left toward the hero.
            level.World.Spawn("bullet", new Vector2(7f, 1.5f), new SpawnOptions { Velocity = new Vector2(-4f, 0f), FacePlayer = false });
            yield return level.Until(1.2f);
            Assert.That(level.World.GetCounter("shots"), Is.GreaterThanOrEqualTo(1f), "the bystander saw the shot pass");
        }

        [UnityTest]
        public IEnumerator AnAllyHurtAndTheHeroHurtAreNoticedAndRewindForgetsThem()
        {
            level = TestLevel.Create(TestLevel.BuildContent(Files, Script));
            yield return level.Until(0.3f);
            Assert.That(level.World.GetCounter("allies"), Is.EqualTo(0f));
            Assert.That(level.World.GetCounter("heroes"), Is.EqualTo(0f));

            var ally = Find(level, "a");
            Assert.That(ally.Damage(1, null), Is.True);
            yield return level.Until(0.7f);
            Assert.That(level.World.GetCounter("allies"), Is.GreaterThanOrEqualTo(1f), "the other neutral being hurt was seen");
            Assert.That(level.World.GetCounter("heroes"), Is.EqualTo(0f), "an ally is not the hero");

            Assert.That(level.World.Player.Damage(1, null, true), Is.True);
            yield return level.Until(1.1f);
            Assert.That(level.World.GetCounter("heroes"), Is.GreaterThanOrEqualTo(1f), "the hero being hurt was seen");

            // Back before either hurt: nothing was seen yet, and nothing replays (the hurts were in the discarded future).
            yield return level.RewindTo(0.35f);
            Assert.That(level.World.GetCounter("allies"), Is.EqualTo(0f));
            Assert.That(level.World.GetCounter("heroes"), Is.EqualTo(0f));
            yield return level.Until(1.4f);
            Assert.That(level.World.GetCounter("allies"), Is.EqualTo(0f), "nobody is hurt in the new future");
            Assert.That(level.World.GetCounter("heroes"), Is.EqualTo(0f));
        }

        [UnityTest]
        public IEnumerator ABystanderFarFromTheHurtDoesNotNotice()
        {
            level = TestLevel.Create(TestLevel.BuildContent(Files, Script));
            yield return level.Until(0.3f);
            var w = Find(level, "w");
            w.TeleportTo(new Vector2(30f, 0f));
            level.World.Player.Damage(1, null, true);
            yield return level.Until(0.8f);
            Assert.That(level.World.GetCounter("heroes"), Is.EqualTo(0f), "30 units away it saw nothing");
        }
    }
}

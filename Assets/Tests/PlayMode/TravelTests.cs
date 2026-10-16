using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SickOfIt.Gameplay;

namespace SickOfIt.Tests.PlayMode
{
    /// <summary>Travel stages, the floor counter and the floor generator, including their rewinds.</summary>
    public class TravelTests
    {
        TestLevel level;

        [TearDown]
        public void TearDown()
        {
            level?.Destroy();
            level = null;
            Time.timeScale = 1f;
        }

        static readonly Dictionary<string, string> Content = new Dictionary<string, string>
        {
            ["entities/dummy.json"] = "{ 'schemaVersion': 1, 'health': 3, 'body': { 'gravity': 0 } }",
            ["entities/wanderer.json"] = "{ 'schemaVersion': 1, 'health': 2, 'body': { 'gravity': 0 }, 'abilities': [ { 'type': 'wander', 'radius': 1.5, 'speed': 1 } ] }",
            ["circles/test.json"] = "{ 'schemaVersion': 1, 'firstFloor': 1000, 'lastFloor': 901, " +
                                    "'track': { 'segmentLength': 4, 'platformChance': 1, 'clearStart': 0 }, " +
                                    "'props': [ { 'shape': 'Spire', 'parallax': 0.5, 'spacing': 2, 'chance': 1 } ], " +
                                    "'enemies': [ { 'entity': 'wanderer', 'areaCenter': [2, 1], 'areaSize': [2, 2] } ], " +
                                    "'easiest': { 'maxAlive': 2, 'pushSize': 3, 'spawnInterval': 0.3, 'breather': 0.5, 'hazardChance': 0 }, " +
                                    "'hardest': { 'maxAlive': 2, 'pushSize': 3, 'spawnInterval': 0.3, 'breather': 0.5, 'hazardChance': 0 }, " +
                                    "'milestones': [ { 'floor': 997, 'text': 'Three down' } ] }",
        };

        void MovePlayer(float x)
        {
            level.PlayerObject.GetComponent<Rigidbody2D>().position = new Vector2(x, 0f);
            level.PlayerObject.transform.position = new Vector3(x, 0f, 0f);
        }

        [UnityTest]
        public IEnumerator TravelCameraOnlyGoesForwardCountsFloorsAndRewinds()
        {
            const string script = @"{
                'floors': { 'start': 1000, 'end': 990, 'unitsPerFloor': 2, 'circle': 'test' },
                'world': { 'camera': 'Travel', 'travelLead': 1, 'followSharpness': 30 },
                'rules': [ { 'when': { 'type': 'floorReached', 'floor': 997 }, 'do': [ { 'type': 'setCounter', 'name': 'reached', 'value': 1 } ] } ]
            }";
            level = TestLevel.Create(TestLevel.BuildContent(Content, script));
            Assert.That(level.World.Stage.Floor, Is.EqualTo(1000));
            Assert.That(level.World.Effects.Track, Is.Not.Null, "a travel stage builds its track");

            yield return level.Until(0.5f);
            MovePlayer(6f);
            yield return level.Until(1.5f);
            Assert.That(level.World.CameraCenter.x, Is.EqualTo(7f).Within(0.2f), "the camera leads the player");
            Assert.That(level.World.Stage.Distance, Is.EqualTo(7f).Within(0.2f));
            Assert.That(level.World.Stage.Floor, Is.EqualTo(997), "a floor every 2 units");
            Assert.That(level.World.GetCounter("reached"), Is.EqualTo(1f), "floorReached fired");
            Assert.That(level.World.Effects.Track.PlatformCount, Is.GreaterThan(0), "platforms streamed in");
            Assert.That(level.World.Effects.Track.SignCount, Is.GreaterThan(0), "floor signs are up");

            MovePlayer(0f);
            yield return level.Until(2.2f);
            Assert.That(level.World.CameraCenter.x, Is.EqualTo(7f).Within(0.2f), "the camera never goes back");

            yield return level.RewindTo(0.3f);
            Assert.That(level.World.CameraCenter.x, Is.LessThan(1.2f), "the camera goes back with time (to the player + lead = 1)");
            Assert.That(level.World.Stage.Floor, Is.EqualTo(1000), "and so does the floor counter");
            Assert.That(level.World.GetCounter("reached"), Is.EqualTo(0f));
        }

        [UnityTest]
        public IEnumerator ClimbingStageGoesUpWithLedges()
        {
            const string script = @"{
                'floors': { 'start': 100, 'end': 90, 'unitsPerFloor': 1.5, 'circle': 'test' },
                'world': { 'camera': 'Travel', 'travelDirection': 'Up', 'travelLead': 1, 'followSharpness': 30 }
            }";
            level = TestLevel.Create(TestLevel.BuildContent(Content, script));
            yield return level.Until(0.3f);
            level.PlayerObject.GetComponent<Rigidbody2D>().position = new Vector2(0f, 5f);
            level.PlayerObject.transform.position = new Vector3(0f, 5f, 0f);
            yield return level.Until(1.2f);
            Assert.That(level.World.CameraCenter.y, Is.EqualTo(6f).Within(0.2f), "the camera climbs with the player");
            Assert.That(level.World.CameraCenter.x, Is.EqualTo(0f).Within(0.01f), "and stays centered across");
            Assert.That(level.World.Stage.Floor, Is.EqualTo(96));
            Assert.That(level.World.Effects.Track.PlatformCount, Is.GreaterThan(1), "ledges streamed in");
        }

        [UnityTest]
        public IEnumerator FloorActionsChangeTheCounter()
        {
            const string script = @"{
                'floors': { 'start': 500 },
                'rules': [ { 'when': { 'type': 'atTime', 'seconds': 0.3 }, 'do': [ { 'type': 'climbFloors', 'count': 5 } ] },
                           { 'when': { 'type': 'atTime', 'seconds': 0.6 }, 'do': [ { 'type': 'setFloor', 'floor': 13 } ] } ]
            }";
            level = TestLevel.Create(TestLevel.BuildContent(Content, script));
            yield return level.Until(0.45f);
            Assert.That(level.World.Stage.Floor, Is.EqualTo(495));
            yield return level.Until(0.75f);
            Assert.That(level.World.Stage.Floor, Is.EqualTo(13));
        }

        [UnityTest]
        public IEnumerator GeneratorRespectsMaxAliveHoldsTheCameraAndReplaysTheSameFloors()
        {
            const string script = @"{
                'floors': { 'start': 1000, 'end': 990, 'unitsPerFloor': 2, 'circle': 'test' },
                'world': { 'camera': 'Travel', 'travelLead': 1, 'followSharpness': 30 },
                'phases': [ { 'name': 'climb', 'onEnter': [ { 'type': 'generateFloors', 'firstBreather': 0.2 } ] } ]
            }";
            level = TestLevel.Create(TestLevel.BuildContent(Content, script));
            yield return level.Until(1.4f);
            var first = level.Alive("wanderer").ToDictionary(e => e.Serial, e => e.Position);
            Assert.That(first.Count, Is.EqualTo(2), "maxAlive caps the push");
            Assert.That(level.Alive("wanderer").All(e => e.Tags.Contains(LevelDirector.GeneratedTag)), Is.True);

            MovePlayer(6f);
            yield return level.Until(2f);
            Assert.That(level.World.CameraCenter.x, Is.LessThan(1.2f), "the camera waits while the push is alive (it started at the player + lead = 1)");

            yield return level.RewindTo(0.4f);
            MovePlayer(0f);
            yield return level.Until(1.4f);
            var second = level.Alive("wanderer").ToDictionary(e => e.Serial, e => e.Position);
            Assert.That(second.Keys, Is.EquivalentTo(first.Keys), "the same push spawned again");
            foreach (var serial in first.Keys)
                Assert.That(Vector2.Distance(first[serial], second[serial]), Is.LessThan(0.6f), $"entity {serial} came from the same seeded spot");

            // Clear every push as it arrives: the camera only moves on in the breathers between them.
            MovePlayer(6f);
            float until = level.Clock.Time + 1.5f;
            while (level.Clock.Time < until)
            {
                foreach (var e in level.Alive("wanderer"))
                    e.Die(DeathCause.Damage);
                yield return new WaitForFixedUpdate();
            }
            Assert.That(level.World.CameraCenter.x, Is.GreaterThan(2f), "the camera moves on once a push is cleared");
        }
    }
}

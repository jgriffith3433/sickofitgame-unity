using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SickOfIt.Data;
using SickOfIt.Gameplay;

namespace SickOfIt.Tests.PlayMode
{
    /// <summary>
    /// Art made by the art pipeline (Tools/Art/art.mjs, Docs/ArtPipeline.md), with the real content: the imp's
    /// sheets load, its shot waits for the throw in its Attack clip, a hit plays Hurt, and death plays Die.
    /// </summary>
    public class GeneratedArtTests
    {
        TestLevel level;

        [TearDown]
        public void TearDown()
        {
            level?.Destroy();
            level = null;
        }

        [UnityTest]
        public IEnumerator ImpAnimatesAttacksHurtsAndDies()
        {
            level = TestLevel.Create(GameContent.FromDefinition(GameDefinition.Instance), playerAt: new Vector2(-3f, 0f));
            var imp = level.World.Spawn("imp", new Vector2(2f, 0f), new SpawnOptions());
            Assert.That(imp.Animation.HasClips, Is.True, "imp has no animator");
            Assert.That(imp.Animation.TryGetClip(Entity.DieState, out var die, out _), Is.True, "imp has no Die clip");
            Assert.That(die.Length, Is.GreaterThan(1));
            yield return new WaitForFixedUpdate();
            Assert.That(imp.Sprite.sprite, Is.Not.Null, "imp's sheet has no sprites (not sliced or not in the registry)");
            Assert.That(imp.Animation.CurrentState, Is.EqualTo("Idle"));

            // The shot leaves on the Attack clip's event, a few frames after the attack starts.
            yield return level.UntilCondition(() => imp.Animation.CurrentState == "Attack", 10f);
            Assert.That(imp.Animation.CurrentState, Is.EqualTo("Attack"));
            yield return level.UntilCondition(() => level.Alive("projectileEnemy").Count > 0, 5f);
            Assert.That(level.Alive("projectileEnemy").Count, Is.GreaterThan(0), "the Attack clip's event never fired the shot");
            yield return level.UntilCondition(() => imp.Animation.CurrentState == "Idle", 5f);

            imp.Damage(1, null, true);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(imp.Animation.CurrentState, Is.EqualTo("Hurt"));

            imp.Damage(imp.Health, null, true);
            yield return null;
            Assert.That(imp.IsAlive, Is.False);
            var flipbook = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).FirstOrDefault(r => r.name == "Flipbook" && r.gameObject.activeInHierarchy);
            Assert.That(flipbook, Is.Not.Null, "Die didn't play");
            Assert.That(die, Does.Contain(flipbook.sprite));
        }
    }
}

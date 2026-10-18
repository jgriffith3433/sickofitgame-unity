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

        /// <summary>
        /// The Magma King (Docs/Story.md, "The Magma King's fight"): seated he throws a boulder on his Throw clip's event;
        /// at half health he rises (Rise) and stands (IdleStand; Enrage comes at a third); death plays Die.
        /// </summary>
        [UnityTest]
        public IEnumerator MagmaKingThrowsRisesAndDies()
        {
            level = TestLevel.Create(GameContent.FromDefinition(GameDefinition.Instance), playerAt: new Vector2(-3f, -2f));
            var king = level.World.Spawn("magmaKing", new Vector2(3.6f, -1.3f), new SpawnOptions());
            Assert.That(king.Animation.HasClips, Is.True, "the Magma King has no animator");
            Assert.That(king.Animation.TryGetClip(Entity.DieState, out var die, out _), Is.True, "the Magma King has no Die clip");
            yield return new WaitForFixedUpdate();
            Assert.That(king.Sprite.sprite, Is.Not.Null, "his sheet has no sprites (not sliced or not in the registry)");
            Assert.That(king.Animation.CurrentState, Is.EqualTo("Idle"));

            yield return level.UntilCondition(() => king.Animation.CurrentState == "Throw", 10f);
            Assert.That(king.Animation.CurrentState, Is.EqualTo("Throw"));
            yield return level.UntilCondition(() => level.Alive("magmaBoulder").Count > 0, 5f);
            Assert.That(level.Alive("magmaBoulder").Count, Is.GreaterThan(0), "the Throw clip's event never threw the boulder");

            king.Damage(king.Health / 2 + 1, null, true);
            yield return level.UntilCondition(() => king.Animation.CurrentState == "Rise", 10f);
            Assert.That(king.Animation.CurrentState, Is.EqualTo("Rise"), "below half health he must stand up");
            yield return level.UntilCondition(() => king.Animation.CurrentState == "IdleStand", 10f);
            Assert.That(king.Animation.CurrentState, Is.EqualTo("IdleStand"), "after Rise he stays standing");

            king.Damage(king.Health, null, true);
            yield return null;
            Assert.That(king.IsAlive, Is.False);
            var flipbook = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).FirstOrDefault(r => r.name == "Flipbook" && r.gameObject.activeInHierarchy);
            Assert.That(flipbook, Is.Not.Null, "Die didn't play");
            Assert.That(die, Does.Contain(flipbook.sprite));
        }
    }
}

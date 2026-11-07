using NUnit.Framework;
using UnityEngine;
using SickOfIt.Core.Input;

namespace SickOfIt.Tests.Editor
{
    /// <summary>The touch move stick's maths (GameplayHud.TouchStick.cs).</summary>
    public class TouchStickTests
    {
        [Test]
        public void ASmallDragIsNothingAndAFullDragIsFullSpeed()
        {
            Assert.AreEqual(0f, TouchStickZone.Axis(5f, 100f));
            Assert.AreEqual(1f, TouchStickZone.Axis(150f, 100f), 1e-4f);
            Assert.AreEqual(-1f, TouchStickZone.Axis(-100f, 100f), 1e-4f);
            Assert.Greater(TouchStickZone.Axis(60f, 100f), 0.3f);
        }

        [Test]
        public void APushUpPastTheEdgeIsAJump()
        {
            Assert.IsTrue(TouchStickZone.IsJumpFlick(90f, 100f));
            Assert.IsFalse(TouchStickZone.IsJumpFlick(40f, 100f));
            Assert.IsFalse(TouchStickZone.IsJumpFlick(90f, 0f));
        }

        [Test]
        public void TheZoneOnlyTakesFingersInsideIt()
        {
            TouchStickZone.Zone = null;
            Assert.IsFalse(TouchStickZone.Contains(new Vector2(10f, 10f)));
            TouchStickZone.Zone = new Rect(0f, 0f, 100f, 100f);
            Assert.IsTrue(TouchStickZone.Contains(new Vector2(10f, 10f)));
            Assert.IsFalse(TouchStickZone.Contains(new Vector2(500f, 10f)));
            TouchStickZone.Zone = null;
        }
    }
}

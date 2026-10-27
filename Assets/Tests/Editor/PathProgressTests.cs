using NUnit.Framework;
using SickOfIt.Gameplay;

namespace SickOfIt.Tests.Editor
{
    /// <summary>The HUD bar on a one-floor Travel stage: distance along the path over the path's length.</summary>
    public class PathProgressTests
    {
        // Floor 998's sections: river 10, procession 14, hayfield 19, emberfall 21, signpost 19, jam 8 (Up), spire 22.
        const float Floor998 = 113f;

        [Test]
        public void StartsEmptyAndEndsFull()
        {
            Assert.AreEqual(0f, WorldEffects.PathFraction(0f, Floor998), 0.0001f);
            Assert.AreEqual(1f, WorldEffects.PathFraction(Floor998, Floor998), 0.0001f);
        }

        [Test]
        public void HalfwayIsHalf()
        {
            Assert.AreEqual(0.5f, WorldEffects.PathFraction(56.5f, Floor998), 0.0001f);
        }

        [Test]
        public void ClampsPastTheEndAndBeforeTheStart()
        {
            Assert.AreEqual(1f, WorldEffects.PathFraction(200f, Floor998), 0.0001f);
            Assert.AreEqual(0f, WorldEffects.PathFraction(-3f, Floor998), 0.0001f);
        }

        [Test]
        public void NoPathMeansNoMeasure()
        {
            Assert.AreEqual(-1f, WorldEffects.PathFraction(10f, 0f), 0.0001f);
        }
    }
}

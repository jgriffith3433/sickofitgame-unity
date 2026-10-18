using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SickOfIt.Data.Config;

namespace SickOfIt.Tests.Editor
{
    public class TimelineOverlayTests
    {
        static TimelineOverlay Spin()
        {
            return new TimelineOverlay
            {
                name = "spin",
                sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f),
                fps = 10f,
                keyframes = new List<OverlayKeyframe>
                {
                    new OverlayKeyframe { time = 10f, position = new Vector2(1f, 1f), rotation = 0f, scale = 1f, alpha = 1f },
                    new OverlayKeyframe { time = 12f, position = Vector2.zero, rotation = -1080f, scale = 0.1f, alpha = 0f },
                },
            };
        }

        [Test]
        public void HiddenOutsideItsKeyframes()
        {
            var overlay = Spin();
            Assert.IsFalse(overlay.Sample(9.99f, out _));
            Assert.IsFalse(overlay.Sample(12.01f, out _));
            Assert.IsTrue(overlay.Sample(10f, out _));
            Assert.IsTrue(overlay.Sample(12f, out _));
        }

        [Test]
        public void SamplesTheSamePoseAtTheSameTime()
        {
            // Sampled from the clock, not tweened: going back to a time gives the same pose (rewind plays it backward).
            var overlay = Spin();
            overlay.Sample(11.5f, out var first);
            overlay.Sample(10.2f, out _);
            overlay.Sample(11.5f, out var again);
            Assert.AreEqual(first.rotation, again.rotation, 1e-4f);
            Assert.AreEqual(first.scale, again.scale, 1e-4f);
        }

        [Test]
        public void RotationIsNotWrapped()
        {
            var overlay = Spin();
            overlay.Sample(11f, out var middle);
            Assert.AreEqual(-540f, middle.rotation, 1e-3f, "halfway through three turns is one and a half turns");
            Assert.AreEqual(0.55f, middle.scale, 1e-4f);
            Assert.AreEqual(0.5f, middle.position.x, 1e-4f);
        }

        [Test]
        public void EaseShapesTheMoveToTheNextKeyframe()
        {
            var overlay = Spin();
            overlay.keyframes[0].ease = OverlayEase.In;
            overlay.Sample(11f, out var eased);
            Assert.AreEqual(-270f, eased.rotation, 1e-3f);
            overlay.keyframes[0].ease = OverlayEase.Hold;
            overlay.Sample(11.9f, out var held);
            Assert.AreEqual(0f, held.rotation, 1e-3f);
        }

        [Test]
        public void FramesFollowTheClockFromTheFirstKeyframe()
        {
            var overlay = Spin();
            Assert.AreEqual(0, overlay.FrameAt(10f, 8));
            Assert.AreEqual(5, overlay.FrameAt(10.55f, 8));
            Assert.AreEqual(1, overlay.FrameAt(10.95f, 8), "loops");
            overlay.loop = false;
            Assert.AreEqual(7, overlay.FrameAt(11.5f, 8), "holds the last frame");
        }

        [Test]
        public void ProblemsCatchKeyframesOutOfOrderAndMissingArt()
        {
            var overlay = Spin();
            CollectionAssert.IsEmpty(overlay.Problems());
            overlay.sprite = null;
            overlay.keyframes.Reverse();
            var problems = new List<string>(overlay.Problems());
            Assert.That(problems.Exists(p => p.Contains("no spriteSheet or sprite")));
            Assert.That(problems.Exists(p => p.Contains("time order")));
        }
    }
}

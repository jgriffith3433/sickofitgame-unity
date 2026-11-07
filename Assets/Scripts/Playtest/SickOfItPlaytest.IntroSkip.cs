using System.Collections;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core.Flow;
using SickOfIt.Core.Progression;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using Object = UnityEngine.Object;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>The intro-skip scenario: Video1 never sits black for over 2 s, one click anywhere skips it within 1 s, and its buttons are 48+ px tall.</summary>
        public const string IntroSkipId = "intro-skip";
        const float IntroSkipSeconds = 90f;

        sealed partial class Session
        {
            int introFailed;

            void IntroCheck(string beat, bool ok, string detail)
            {
                if (ok) { Run.Event("check", $"intro-skip {beat}: ok ({detail})"); return; }
                introFailed++;
                Run.Event("check", $"intro-skip {beat}: FAILED ({detail})");
                Run.Problem(PlaytestSeverity.Bug, $"intro-skip {beat}: {detail}");
            }

            /// <summary>Video1 loaded directly (setup shortcut), then real input only: a click on empty screen must skip it.</summary>
            public IEnumerator IntroSkipRun()
            {
                yield return ToMainMenu();
                if (Run.Done) yield break;

                SceneTransitionManager.Instance.LoadVideo(1);
                yield return Run.Until(() => Object.FindAnyObjectByType<VideoRuntime>() != null, 30f, "the intro scene");
                if (Run.Done) yield break;
                Run.Scene("Intro video");

                // 1. Playing or gone on within 2 s of the scene being up (no black wait).
                float sceneAt = Time.realtimeSinceStartup;
                yield return Run.Until(() => ClockTime > 0.05f || Object.FindAnyObjectByType<VideoRuntime>() == null, 6f, "the intro to play or move on");
                float black = Time.realtimeSinceStartup - sceneAt;
                bool playing = ClockTime > 0.05f && Object.FindAnyObjectByType<VideoRuntime>() != null;
                IntroCheck("black", black <= 2f, playing ? $"the intro was playing after {black:0.0}s" : $"no picture after {black:0.0}s; the game moved on");
                Run.Metric("introSkip.blackSeconds", black);
                if (Run.Done) yield break;

                // 2. The skip button is at least 48 px tall.
                foreach (var b in Object.FindObjectsByType<MobileControllerFramework.Button>(FindObjectsSortMode.None))
                {
                    var rect = (RectTransform)b.transform;
                    var corners = new Vector3[4];
                    rect.GetWorldCorners(corners);
                    var canvas = rect.GetComponentInParent<Canvas>();
                    var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                    float h = Mathf.Abs(RectTransformUtility.WorldToScreenPoint(cam, corners[1]).y - RectTransformUtility.WorldToScreenPoint(cam, corners[0]).y);
                    Run.Metric("introSkip.button." + b.gameObject.name + "Px", h);
                    IntroCheck("buttonHeight " + b.gameObject.name, h >= 47.5f, $"{h:0} px tall on a {Screen.width}x{Screen.height} screen");
                }

                // 3. One click on empty screen (top-left, off every button) skips it.
                if (Object.FindAnyObjectByType<VideoRuntime>() != null)
                {
                    yield return WaitReal(1f);
                    input.MoveMouse(new Vector2(Screen.width * 0.15f, Screen.height * 0.8f));
                    yield return null;
                    float clickAt = Time.realtimeSinceStartup;
                    input.SetMouseButton(true);
                    yield return null;
                    yield return null;
                    input.SetMouseButton(false);
                    Run.Event("skip", "one click on empty screen");
                    yield return Run.Until(() => Object.FindAnyObjectByType<VideoRuntime>() == null, 5f, "the intro to end after one click");
                    float took = Time.realtimeSinceStartup - clickAt;
                    IntroCheck("skip", Object.FindAnyObjectByType<VideoRuntime>() == null && took <= 1f, $"one click ended the intro in {took:0.00}s");
                    Run.Metric("introSkip.skipSeconds", took);
                }
                Run.Progress(1f);
                End(introFailed == 0 ? PlaytestOutcome.Won : PlaytestOutcome.Done);
            }
        }
    }
}

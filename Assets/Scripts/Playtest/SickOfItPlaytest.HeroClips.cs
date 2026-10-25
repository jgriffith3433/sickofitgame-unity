using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Core.Input;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The hero's own clips (art/hero.json Hurt, DeathBurn/Squash/Spin/Shrug, Victory; player.json states): the bot plays Level 1 to lose four times
        /// (reckless: no dodging, real input) and every first hit must show the Hurt clip and every death its own clip, the four in turn; then it
        /// plays a floor to its clear, which must show Victory. Sheets may be stand-ins until the real clips are generated: the clip on screen is read
        /// from <see cref="PlayerSceneClip"/> (state and frame), not from the sheet's art.
        /// </summary>
        public const string HeroClipsId = "hero-clips";
        const int HeroClipsDeathLevel = 1;
        const int HeroClipsClearLevel = 2;
        const float HeroClipsSeconds = 900f;

        sealed partial class Session
        {
            int clipsPassed, clipsFailed;

            void ClipCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    clipsPassed++;
                    Run.Event("check", $"hero-clips {beat}: ok ({detail})");
                }
                else
                {
                    clipsFailed++;
                    Run.Event("check", $"hero-clips {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"hero-clips {beat}: {detail}");
                }
            }

            /// <summary>The hero clip on screen right now: its state and frame, or null.</summary>
            static string HeroClipOnScreen(out int frame)
            {
                frame = -1;
                var world = GameplayWorld.Instance;
                var pc = world != null ? world.PlayerController : null;
                if (pc == null || !pc.TryGetComponent(out PlayerSceneClip clip) || clip.State == null)
                    return null;
                frame = clip.Frame;
                return clip.State;
            }

            IEnumerator OpenClipsLevel(int n)
            {
                var old = GameplayWorld.Instance;
                var load = LoadLevel(n);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(n), 60f, $"level {n} to start");
            }

            public IEnumerator HeroClipsRun()
            {
                yield return ToMainMenu();
                if (Run.Done) yield break;
                var deaths = new List<string>();
                for (int i = 1; i <= 4 && !Run.Done; i++)
                {
                    Run.Event("shortcut", $"death {i}: load level {HeroClipsDeathLevel} directly");
                    yield return OpenClipsLevel(HeroClipsDeathLevel);
                    if (Run.Done) yield break;
                    bool hurtSeen = false;
                    int hurtFrame = -1;
                    string death = null;
                    int deathFrame = -1, deathTicks = 0;
                    reckless = true;
                    try
                    {
                        yield return PlayLevel(HeroClipsDeathLevel, 0f, null, 0f, 1f, 240f, waitOutDeath: true, until: () =>
                        {
                            string on = HeroClipOnScreen(out int frame);
                            if (on == "Hurt" && !hurtSeen)
                            {
                                hurtSeen = true;
                                hurtFrame = frame;
                                Run.Event("hero-clip", $"Hurt on screen (frame {frame}) at clock {GameClock.Instance.Time:0.00}");
                            }
                            if (on != null && on.StartsWith("Death"))
                            {
                                death = on;
                                deathFrame = frame;
                                deathTicks++;
                            }
                            return deathTicks >= 45;
                        });
                    }
                    finally { reckless = false; }
                    if (Run.Done) yield break;
                    ClipCheck($"hit {i}: the Hurt clip", hurtSeen, hurtSeen ? $"Hurt on screen at frame {hurtFrame}" : "no hurt clip on screen when he took a hit (only the red flash)");
                    ClipCheck($"death {i}: a death clip", death != null, death != null ? $"{death} on screen (frame {deathFrame})" : $"no death clip on screen ({lastOutcome})");
                    if (death != null)
                    {
                        Run.Event("hero-clip", $"death {i}: {death}");
                        deaths.Add(death);
                    }
                }
                if (Run.Done) yield break;
                var distinct = new HashSet<string>(deaths);
                ClipCheck("four different deaths", deaths.Count == 4 && distinct.Count == 4, $"{string.Join(", ", deaths)}");

                // The death scene shows the same death he just died.
                GameObject sceneHero = null;
                yield return Run.Until(() => (sceneHero = GameObject.Find("DeathHero")) != null, 30f, "the death scene's hero");
                if (sceneHero != null)
                {
                    yield return Run.Until(() => sceneHero == null || (sceneHero.TryGetComponent(out PlayerSceneClip c) && c.State != null), 5f, "the death scene's hero clip");
                    string shown = sceneHero != null && sceneHero.TryGetComponent(out PlayerSceneClip sc) ? sc.State : null;
                    ClipCheck("the death scene shows a death clip", shown != null && shown.StartsWith("Death"), $"the death scene plays {shown ?? "nothing"}");
                }
                else
                {
                    ClipCheck("the death scene shows a death clip", false, "no DeathHero in the death scene");
                }
                if (Run.Done) yield break;
                Run.Progress(0.6f);

                // Victory: the bot clears a floor.
                Run.Event("shortcut", $"victory: load level {HeroClipsClearLevel} directly");
                yield return OpenClipsLevel(HeroClipsClearLevel);
                if (Run.Done) yield break;
                bool victory = false;
                int victoryFrame = -1;
                yield return PlayLevel(HeroClipsClearLevel, 0f, null, 0.6f, 0.95f, 420f, until: () =>
                {
                    if (HeroClipOnScreen(out int frame) == "Victory")
                    {
                        if (!victory)
                            Run.Event("hero-clip", $"Victory on screen (frame {frame}) at clock {GameClock.Instance.Time:0.00}");
                        victory = true;
                        victoryFrame = frame;
                    }
                    return victory;
                });
                if (Run.Done) yield break;
                ClipCheck("the floor clear: Victory", victory, victory ? $"Victory on screen at frame {victoryFrame}" : $"no Victory clip when the floor was cleared ({lastOutcome})");
                Run.Metric("heroClips.passed", clipsPassed);
                Run.Metric("heroClips.failed", clipsFailed);
                input.ReleaseAll();
                Run.Progress(1f);
                End(clipsFailed == 0 ? PlaytestOutcome.Won : PlaytestOutcome.Done);
            }
        }
    }
}

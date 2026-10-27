using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.InputSystem;
using SickOfIt.Core;
using SickOfIt.Core.Rewind;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Gameplay;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The incoming-warnings scenario: Floor 1000 (quiet at its start) opened directly (a setup shortcut), known fireballs put in from each edge,
        /// then Floor 998's rain played by the bot. Each marker is compared with the moment its fireball entered the view.
        /// </summary>
        public const string IncomingId = "incoming-warnings";
        const float IncomingSeconds = 300f;

        sealed partial class Session
        {
            int incomingPassed, incomingFailed;
            readonly List<IncomingMarker> incomingMarkers = new List<IncomingMarker>();
            readonly List<float> incomingAlphas = new List<float>();
            readonly List<Rect> incomingRects = new List<Rect>();

            void IncomingCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    incomingPassed++;
                    Run.Event("check", $"incoming {beat}: ok ({detail})");
                }
                else
                {
                    incomingFailed++;
                    Run.Event("check", $"incoming {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"incoming {beat}: {detail}");
                }
            }

            /// <summary>A hostile fireball 'ahead' world units outside the view's edge, flying at 'speed' straight across the view.</summary>
            Entity IncomingProbe(GameplayWorld world, EdgeSide side, float ahead, float speed, Vector2 shift)
            {
                Rect v = world.View;
                Vector2 c = v.center;
                Vector2 at, vel;
                switch (side)
                {
                    case EdgeSide.Top: at = new Vector2(c.x, v.yMax + ahead); vel = new Vector2(0f, -speed); break;
                    case EdgeSide.Bottom: at = new Vector2(c.x, v.yMin - ahead); vel = new Vector2(0f, speed); break;
                    case EdgeSide.Left: at = new Vector2(v.xMin - ahead, c.y); vel = new Vector2(speed, 0f); break;
                    default: at = new Vector2(v.xMax + ahead, c.y); vel = new Vector2(-speed, 0f); break;
                }
                var options = new SpawnOptions { Velocity = vel, FacePlayer = false };
                return world.Spawn("fireball", at + shift, options);
            }

            /// <summary>The probes cross the middle of the view: he stands there unhurt (a setup shortcut), so the run is about the markers, not about surviving.</summary>
            void IncomingGuard()
            {
                var health = GameplayWorld.Instance != null && GameplayWorld.Instance.Player != null ? GameplayWorld.Instance.Player.GetComponentInChildren<SickOfIt.Combat.Health.PlayerHealth>() : null;
                if (health == null || !health.IsAlive)
                    return;
                health.SetHealth(health.MaxHealth);
                health.SetInvincibilityTimer(2f);
            }

            IEnumerator IncomingWait(float seconds)
            {
                float end = Time.realtimeSinceStartup + seconds;
                while (!Run.Done && Time.realtimeSinceStartup < end)
                {
                    yield return null;
                    IncomingGuard();
                }
            }

            static int IncomingOn(GameplayHud hud, EdgeSide side, List<IncomingMarker> scratch, List<float> alphas)
            {
                hud.IncomingState(scratch, alphas);
                int n = 0;
                foreach (var m in scratch)
                    if (m.Side == side)
                        n++;
                return n;
            }

            /// <summary>The widgets a marker must stay clear of: every button on the HUD that is showing (pause, rewind, weapon, dash, the touch pad).</summary>
            static int IncomingHudOverlaps(GameplayHud hud, List<Rect> markerRects, out string worst)
            {
                worst = "";
                hud.IncomingScreenRects(markerRects);
                int overlaps = 0;
                foreach (var button in hud.GetComponentsInChildren<MenuButton>(false))
                {
                    if (button == null || !button.gameObject.activeInHierarchy)
                        continue;
                    var corners = new Vector3[4];
                    ((RectTransform)button.transform).GetWorldCorners(corners);
                    var r = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
                    foreach (var m in markerRects)
                    {
                        if (m.Overlaps(r))
                        {
                            overlaps++;
                            worst = $"marker {m} over {button.name} {r}";
                        }
                    }
                }
                return overlaps;
            }

            sealed class IncomingTrack
            {
                public Entity Entity;
                public float SpawnedAt, FirstMarkerAt = -1f, EnteredAt = -1f, EtaAtFirstSight;
                public EdgeSide Side;
            }

            public IEnumerator IncomingRun()
            {
                var cfg = GameDefinition.Instance.incomingWarnings;
                var problems = cfg.Validate();
                IncomingCheck("config is valid", problems.Count == 0, problems.Count == 0 ? $"lookahead {cfg.lookaheadSeconds} s, cap {cfg.maxMarkers}" : string.Join("; ", problems));
                bool savedSetting = GameSettings.IncomingWarnings;
                GameSettings.IncomingWarnings = true;

                // ── 1. Floor 1000: known fireballs from each edge ───────────────────────────
                int quiet = LevelNumberOf("Floor1000");
                Run.Event("shortcut", $"Floor 1000 (level {quiet}) opened directly");
                var load = LoadLevel(quiet);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => LevelRunning(quiet) && GameplayWorld.Instance != null && GameplayWorld.Instance.Hud != null, 60f, "Floor 1000");
                if (Run.Done)
                    yield break;
                var world = GameplayWorld.Instance;
                var hud = world.Hud;
                Time.timeScale = Speed;
                string shape = world.Camera != null && world.Camera.aspect < 1f ? "portrait" : "landscape";
                Run.Event("incoming", $"{shape}, aspect {(world.Camera != null ? world.Camera.aspect : 0f):0.00}, view {world.View.width:0.0}x{world.View.height:0.0}, lookahead {cfg.lookaheadSeconds} s, cap {cfg.maxMarkers}");
                yield return IncomingWait(1f);

                float leadMin = float.MaxValue;
                foreach (EdgeSide side in new[] { EdgeSide.Top, EdgeSide.Left, EdgeSide.Right, EdgeSide.Bottom })
                {
                    if (Run.Done)
                        yield break;
                    // 7 units out at 4/s: it is in view 1.75 s later; a marker is due 0.25 s after it is spawned.
                    var shot = IncomingProbe(world, side, 7f, 4f, Vector2.zero);
                    float spawnedAt = GameClock.Instance.Time, markerAt = -1f, enteredAt = -1f, vanishedAt = -1f, alphaAtFirst = 0f, alphaMax = 0f;
                    string wrongSide = "";
                    float end = Time.realtimeSinceStartup + 6f;
                    while (!Run.Done && Time.realtimeSinceStartup < end && vanishedAt < 0f)
                    {
                        yield return null;
                        IncomingGuard();
                        float now = GameClock.Instance.Time;
                        bool alive = shot != null && shot.IsAlive;
                        bool inView = alive && world.View.Contains(shot.Position);
                        int count = hud.IncomingState(incomingMarkers, incomingAlphas);
                        for (int i = 0; i < incomingMarkers.Count; i++)
                        {
                            if (incomingMarkers[i].Side != side && wrongSide.Length == 0)
                                wrongSide = $"a {incomingMarkers[i].Side} marker for a {side} fireball";
                            alphaMax = Mathf.Max(alphaMax, incomingAlphas[i]);
                        }
                        if (markerAt < 0f && count > 0)
                        {
                            markerAt = now;
                            alphaAtFirst = incomingAlphas[0];
                        }
                        if (enteredAt < 0f && inView)
                            enteredAt = now;
                        if (!alive && enteredAt < 0f)
                            break;
                        if (enteredAt >= 0f && count == 0)
                            vanishedAt = now;
                    }
                    if (shot != null && shot.IsAlive)
                        shot.Die(DeathCause.Despawn);
                    if (enteredAt < 0f)
                    {
                        Run.Event("incoming", $"{side}: the fireball died before it entered the view (ground or lava in the way): not checked");
                        continue;
                    }
                    float lead = enteredAt - markerAt;
                    leadMin = Mathf.Min(leadMin, markerAt < 0f ? 0f : lead);
                    Run.Metric($"incoming.{shape}.{side}.leadSeconds", markerAt < 0f ? 0f : lead);
                    Run.Event("incoming", $"{side}: marker at {markerAt - spawnedAt:0.00} s, in view at {enteredAt - spawnedAt:0.00} s (lead {lead:0.00} s), first alpha {alphaAtFirst:0.00}, strongest {alphaMax:0.00}, gone {(vanishedAt < 0f ? -1f : vanishedAt - enteredAt):0.00} s after");
                    IncomingCheck($"{shape} {side}: marker 1+ s before it is in view", markerAt >= 0f && lead >= 1f, $"lead {lead:0.00} s");
                    IncomingCheck($"{shape} {side}: on the right edge", wrongSide.Length == 0, wrongSide.Length == 0 ? "same edge" : wrongSide);
                    IncomingCheck($"{shape} {side}: quiet (alpha 0.25 to 0.7, never a flash)", alphaAtFirst <= cfg.farAlpha + 0.1f && alphaMax <= cfg.nearAlpha + 0.01f, $"first {alphaAtFirst:0.00}, strongest {alphaMax:0.00}");
                    IncomingCheck($"{shape} {side}: gone once it is on screen", vanishedAt >= 0f && vanishedAt - enteredAt <= 0.25f, vanishedAt < 0f ? "still shown" : $"{vanishedAt - enteredAt:0.00} s");
                    yield return IncomingWait(0.3f);
                }
                Run.Progress(0.15f);

                // ── 2. Things that move away or never come in get nothing ───────────────────
                var away = new List<Entity>
                {
                    world.Spawn("fireball", new Vector2(world.View.center.x, world.View.yMax + 3f), new SpawnOptions { Velocity = new Vector2(0f, 4f), FacePlayer = false }),
                    world.Spawn("fireball", new Vector2(world.View.xMin - 3f, world.View.yMax + 3f), new SpawnOptions { Velocity = new Vector2(-4f, 0f), FacePlayer = false }),
                    world.Spawn("fireball", new Vector2(world.View.xMin - 3f, world.View.yMax + 4f), new SpawnOptions { Velocity = new Vector2(5f, 0f), FacePlayer = false }),
                };
                int awayMax = 0;
                float awayEnd = Time.realtimeSinceStartup + 1.5f;
                while (!Run.Done && Time.realtimeSinceStartup < awayEnd)
                {
                    yield return null;
                    IncomingGuard();
                    awayMax = Mathf.Max(awayMax, hud.IncomingState(null, null));
                }
                foreach (var e in away)
                    if (e != null && e.IsAlive)
                        e.Die(DeathCause.Despawn);
                IncomingCheck($"{shape} moving away or passing by gets no marker", awayMax == 0, $"{awayMax} marker(s) for 3 fireballs that never come in");
                yield return IncomingWait(0.5f);

                // ── 3. A crowd: ten fireballs along the top become one soft band; never over the cap ──
                var crowd = new List<Entity>();
                for (int i = 0; i < 10; i++)
                {
                    float f = (i + 0.5f) / 10f;
                    var at = new Vector2(Mathf.Lerp(world.View.xMin + 1f, world.View.xMax - 1f, f), world.View.yMax + 7f + (i % 3) * 0.4f);
                    crowd.Add(world.Spawn("fireball", at, new SpawnOptions { Velocity = new Vector2(0f, -4f), FacePlayer = false }));
                }
                int crowdMax = 0, crowdTopMax = 0, crowdDrawnMax = 0;
                float crowdEnd = Time.realtimeSinceStartup + 2.4f;
                while (!Run.Done && Time.realtimeSinceStartup < crowdEnd)
                {
                    yield return null;
                    IncomingGuard();
                    crowdMax = Mathf.Max(crowdMax, hud.IncomingState(incomingMarkers, incomingAlphas));
                    crowdTopMax = Mathf.Max(crowdTopMax, IncomingOn(hud, EdgeSide.Top, incomingMarkers, incomingAlphas));
                    hud.IncomingScreenRects(incomingRects);
                    crowdDrawnMax = Mathf.Max(crowdDrawnMax, incomingRects.Count);
                }
                IncomingCheck($"{shape} ten fireballs: one band on the top edge", crowdTopMax == 1 && crowdMax <= cfg.maxMarkers, $"top edge {crowdTopMax}, all edges {crowdMax}, drawn {crowdDrawnMax}, cap {cfg.maxMarkers}");
                Run.Metric($"incoming.{shape}.crowdMarkers", crowdMax);
                foreach (var e in crowd)
                    if (e != null && e.IsAlive)
                        e.Die(DeathCause.Despawn);
                yield return IncomingWait(0.6f);

                // ── 4. Nothing over the HUD ─────────────────────────────────────────────────
                var rightShot = IncomingProbe(world, EdgeSide.Right, 9f, 4f, new Vector2(0f, world.View.height * 0.4f));
                var topShot = IncomingProbe(world, EdgeSide.Top, 9f, 4f, Vector2.zero);
                var bottomShot = IncomingProbe(world, EdgeSide.Bottom, 9f, 4f, new Vector2(world.View.width * 0.4f, 0f));
                int overlaps = 0;
                string worstOverlap = "";
                float layoutEnd = Time.realtimeSinceStartup + 1.5f;
                while (!Run.Done && Time.realtimeSinceStartup < layoutEnd)
                {
                    yield return null;
                    IncomingGuard();
                    overlaps += IncomingHudOverlaps(hud, incomingRects, out string w);
                    if (w.Length > 0)
                        worstOverlap = w;
                }
                IncomingCheck($"{shape} no overlap with the HUD's buttons", overlaps == 0, overlaps == 0 ? "clear" : $"{overlaps} overlaps, e.g. {worstOverlap}");
                foreach (var e in new[] { rightShot, topShot, bottomShot })
                    if (e != null && e.IsAlive)
                        e.Die(DeathCause.Despawn);
                yield return IncomingWait(0.5f);
                Run.Progress(0.3f);

                // ── 5. No allocation per frame ──────────────────────────────────────────────
                var allocShot = IncomingProbe(world, EdgeSide.Left, 9f, 3f, Vector2.zero);
                yield return IncomingWait(0.8f);
                hud.TickIncomingForTest();
                long before = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
                for (int i = 0; i < 400; i++)
                    hud.TickIncomingForTest();
                long grew = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() - before;
                IncomingCheck("400 updates with a marker showing allocate nothing", grew <= 8192, $"managed heap grew {grew} bytes");
                Run.Metric("incoming.allocBytes400", grew);
                if (allocShot != null && allocShot.IsAlive)
                    allocShot.Die(DeathCause.Despawn);
                yield return IncomingWait(0.5f);

                // ── 6. Pause freezes it ─────────────────────────────────────────────────────
                var pauseShot = IncomingProbe(world, EdgeSide.Top, 8f, 3f, Vector2.zero);
                yield return Run.Until(() => hud.IncomingActive > 0, 6f, "a marker to pause on");
                yield return IncomingWait(0.5f);
                hud.IncomingState(incomingMarkers, incomingAlphas);
                IncomingMarker beforePause = incomingMarkers.Count > 0 ? incomingMarkers[0] : default;
                float alphaBeforePause = incomingAlphas.Count > 0 ? incomingAlphas[0] : 0f;
                yield return Tap(Key.P);
                yield return IncomingWait(0.2f);
                bool paused = hud.Paused;
                hud.IncomingState(incomingMarkers, incomingAlphas);
                float alphaA = incomingAlphas.Count > 0 ? incomingAlphas[0] : -1f;
                IncomingMarker atPause = incomingMarkers.Count > 0 ? incomingMarkers[0] : default;
                yield return IncomingWait(1.2f);
                hud.IncomingState(incomingMarkers, incomingAlphas);
                float alphaB = incomingAlphas.Count > 0 ? incomingAlphas[0] : -1f;
                IncomingMarker later = incomingMarkers.Count > 0 ? incomingMarkers[0] : default;
                IncomingCheck("paused: the marker is frozen", paused && alphaA >= 0f && Mathf.Approximately(alphaA, alphaB) && Mathf.Approximately(atPause.Eta, later.Eta) && Mathf.Approximately(atPause.Along, later.Along),
                    $"paused {paused}, alpha {alphaA:0.000} -> {alphaB:0.000}, eta {atPause.Eta:0.000} -> {later.Eta:0.000} (before the pause eta {beforePause.Eta:0.00}, alpha {alphaBeforePause:0.00})");
                yield return Tap(Key.P);
                yield return IncomingWait(0.3f);
                if (pauseShot != null && pauseShot.IsAlive)
                    pauseShot.Die(DeathCause.Despawn);
                yield return IncomingWait(0.5f);

                // ── 7. Rewind: they go while it runs and come back right, none stuck ─────────
                RewindBudget.Instance?.Add(30f);
                var rewindShot = IncomingProbe(world, EdgeSide.Left, 8f, 3f, Vector2.zero);
                yield return Run.Until(() => hud.IncomingActive > 0, 6f, "a marker to rewind on");
                yield return IncomingWait(0.6f);
                input.SetKey(Key.R, true);
                int shownWhileRewinding = 0;
                bool sawRewind = false;
                float rewindEnd = Time.realtimeSinceStartup + 1.2f;
                while (!Run.Done && Time.realtimeSinceStartup < rewindEnd)
                {
                    yield return null;
                    IncomingGuard();
                    if (GameClock.Instance.IsRewinding)
                    {
                        sawRewind = true;
                        if (Time.realtimeSinceStartup > rewindEnd - 0.7f)
                            shownWhileRewinding = Mathf.Max(shownWhileRewinding, hud.IncomingActive);
                    }
                }
                input.SetKey(Key.R, false);
                yield return IncomingWait(0.6f);
                int expected = IncomingExpected(world, cfg);
                int active = hud.IncomingActive;
                IncomingCheck("rewind: no marker is drawn while rewinding", sawRewind && shownWhileRewinding == 0, sawRewind ? $"{shownWhileRewinding} active during the rewind" : "the rewind never started");
                IncomingCheck("rewind: after it, markers match the live fireballs (none stuck)", active == expected, $"{active} shown, {expected} expected from the live entities");
                if (rewindShot != null && rewindShot.IsAlive)
                    rewindShot.Die(DeathCause.Despawn);
                var leftover = new List<Entity>();
                foreach (var e in world.Live)
                    if (e != null && e.IsAlive && e.Team == Team.Enemy && e.Def != null && e.Def.id == "fireball")
                        leftover.Add(e);
                foreach (var e in leftover)
                    e.Die(DeathCause.Despawn);
                yield return IncomingWait(0.8f);
                IncomingCheck("rewind: markers clear when the fireballs are gone", hud.IncomingActive == 0, $"{hud.IncomingActive} still shown");
                Run.Progress(0.45f);

                // ── 8. The Settings toggle turns them off ───────────────────────────────────
                GameSettings.IncomingWarnings = false;
                var offShot = IncomingProbe(world, EdgeSide.Top, 8f, 3f, Vector2.zero);
                int offMax = 0;
                float offEnd = Time.realtimeSinceStartup + 1.5f;
                while (!Run.Done && Time.realtimeSinceStartup < offEnd)
                {
                    yield return null;
                    IncomingGuard();
                    offMax = Mathf.Max(offMax, hud.IncomingActive);
                }
                IncomingCheck("Settings > Accessibility > Incoming warnings off: no marker", offMax == 0, $"{offMax} marker(s) with the setting off");
                GameSettings.IncomingWarnings = true;
                yield return IncomingWait(0.5f);
                IncomingCheck("... and on again: the marker returns", hud.IncomingActive > 0 || offShot == null || !offShot.IsAlive, $"{hud.IncomingActive} shown");
                if (offShot != null && offShot.IsAlive)
                    offShot.Die(DeathCause.Despawn);
                Run.Progress(0.5f);

                // ── 9. Floor 998's rain: one soft band, never over the cap, a lead on every drop ───
                int rain = LevelNumberOf("Floor998");
                Run.Event("shortcut", $"Floor 998 (level {rain}) opened directly");
                load = LoadLevel(rain);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => LevelRunning(rain) && GameplayWorld.Instance != null && GameplayWorld.Instance.Hud != null, 60f, "Floor 998");
                if (Run.Done)
                    yield break;
                world = GameplayWorld.Instance;
                hud = world.Hud;
                int maxActive = 0, maxTop = 0, frames = 0, framesActive = 0, framesOverCap = 0, spawns = 0, leadsChecked = 0, leadsShort = 0;
                float maxSlew = 0f, shortest = float.MaxValue, lastAlpha = 0f, lastAt = -1f;
                var tracked = new List<IncomingTrack>();
                System.Action<Entity, Vector2, Rect> onSpawn = (entity, at, view) =>
                {
                    var def = entity.Def;
                    if (def == null || def.team != Team.Enemy || def.id != "fireball" || GameClock.Instance.IsRewinding)
                        return;
                    spawns++;
                    tracked.Add(new IncomingTrack { Entity = entity, SpawnedAt = GameClock.Instance.Time });
                };
                GameplayWorld.Spawned += onSpawn;
                bool Poll()
                {
                    var clock = GameClock.Instance;
                    if (clock == null || clock.IsRewinding || hud == null)
                        return false;
                    frames++;
                    int count = hud.IncomingState(incomingMarkers, incomingAlphas);
                    maxActive = Mathf.Max(maxActive, count);
                    if (count > 0)
                        framesActive++;
                    if (count > cfg.maxMarkers)
                        framesOverCap++;
                    int top = 0;
                    float alphaSum = 0f;
                    for (int i = 0; i < incomingMarkers.Count; i++)
                    {
                        if (incomingMarkers[i].Side == EdgeSide.Top)
                            top++;
                        alphaSum += incomingAlphas[i];
                    }
                    maxTop = Mathf.Max(maxTop, top);
                    // A flash is the total opacity jumping: the fade is a slow slope.
                    if (lastAt >= 0f && clock.Time > lastAt)
                        maxSlew = Mathf.Max(maxSlew, Mathf.Abs(alphaSum - lastAlpha) / Mathf.Max(0.001f, Time.unscaledDeltaTime));
                    lastAlpha = alphaSum;
                    lastAt = clock.Time;
                    var v = world.View;
                    foreach (var t in tracked)
                    {
                        if (t.EnteredAt >= 0f || t.Entity == null || !t.Entity.IsAlive)
                            continue;
                        if (v.Contains(t.Entity.Position))
                        {
                            t.EnteredAt = clock.Time;
                            if (t.FirstMarkerAt >= 0f && t.EtaAtFirstSight >= 1.25f)
                            {
                                leadsChecked++;
                                float lead = t.EnteredAt - t.FirstMarkerAt;
                                shortest = Mathf.Min(shortest, lead);
                                if (lead < 1f)
                                    leadsShort++;
                            }
                            continue;
                        }
                        if (t.FirstMarkerAt < 0f && IncomingSet.Crossing(v, t.Entity.Position, t.Entity.Body.linearVelocity, cfg.lookaheadSeconds, out var side, out _, out float eta))
                        {
                            for (int i = 0; i < incomingMarkers.Count; i++)
                            {
                                if (incomingMarkers[i].Side != side)
                                    continue;
                                t.FirstMarkerAt = clock.Time;
                                t.Side = side;
                                t.EtaAtFirstSight = eta;
                                break;
                            }
                        }
                    }
                    tracked.RemoveAll(t => t.EnteredAt >= 0f || t.Entity == null || !t.Entity.IsAlive);
                    return false;
                }
                yield return PlayLevel(rain, 55f, null, 0.5f, 0.95f, 55f * 3f + 60f, until: Poll);
                GameplayWorld.Spawned -= onSpawn;
                if (Run.Done)
                    yield break;
                // A fireball is first seen by the marker only once its crossing is within the lookahead; a drop whose ETA at that moment was
                // under 1.25 s came into being too close to be warned 1 s ahead and is left out (the spawn margin keeps those rare).
                Run.Metric("incoming.rain.maxMarkers", maxActive);
                Run.Metric("incoming.rain.maxTopMarkers", maxTop);
                Run.Metric("incoming.rain.maxAlphaSlewPerSecond", maxSlew);
                Run.Metric("incoming.rain.shortestLead", shortest == float.MaxValue ? -1f : shortest);
                Run.Event("incoming", $"Floor 998 ({shape}): {spawns} fireballs, {frames} frames, markers shown in {framesActive}, at most {maxActive} at once ({maxTop} on the top), leads checked {leadsChecked}, shortest {(shortest == float.MaxValue ? 0f : shortest):0.00} s, steepest fade {maxSlew:0.0}/s");
                IncomingCheck($"{shape} Floor 998: rain was warned about", spawns > 0 && framesActive > 0, $"{spawns} fireballs, markers on {framesActive} of {frames} frames");
                IncomingCheck($"{shape} Floor 998: never more than {cfg.maxMarkers} markers", framesOverCap == 0 && maxActive <= cfg.maxMarkers, $"at most {maxActive}");
                IncomingCheck($"{shape} Floor 998: the rain is one soft band on the top", maxTop <= 1, $"at most {maxTop} markers on the top edge");
                IncomingCheck($"{shape} Floor 998: no flashing (fade only)", maxSlew <= (cfg.nearAlpha / cfg.fadeOutSeconds) * 2.5f, $"steepest change {maxSlew:0.0} of opacity per second");
                IncomingCheck($"{shape} Floor 998: markers 1+ s ahead of each drop", leadsShort == 0, $"{leadsChecked} checked, {leadsShort} under 1 s, shortest {(shortest == float.MaxValue ? 0f : shortest):0.00} s");

                GameSettings.IncomingWarnings = savedSetting;
                Run.Metric("incoming.passed", incomingPassed);
                Run.Metric("incoming.failed", incomingFailed);
                Run.Progress(1f);
                End(incomingFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }

            /// <summary>How many markers the live hostile shots should make (the same maths, read from the world).</summary>
            static int IncomingExpected(GameplayWorld world, SickOfIt.Data.Config.IncomingWarningsConfig cfg)
            {
                var set = new IncomingSet();
                set.Begin(world.View, cfg.lookaheadSeconds, cfg.mergeFraction, cfg.maxMarkers);
                foreach (var e in world.Live)
                {
                    if (e == null || !e.IsAlive || e.Team != Team.Enemy || e.Body == null || e.Def == null || e.Def.id != "fireball")
                        continue;
                    set.Add(e.Position, e.Body.linearVelocity, false);
                }
                set.End();
                return set.Count;
            }
        }
    }
}

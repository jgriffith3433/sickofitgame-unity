using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Core.Audio;
using SickOfIt.Combat.Health;
using SickOfIt.Data;
using SickOfIt.Gameplay;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The story-props scenario (Docs/Story.md "Hell's furniture", Tools/Levels/story_props.py): the Meadow and Floors 1000-997 each opened directly
        /// (setup shortcut) and played by the bot for ~35 s of game time through real input (on a phone-sized screen the touch UI is switched on and the
        /// bot plays through its virtual pad). Per level, from the level's own data: 8+ story props placed, 2+ of them reacting, each standing on its
        /// surface (gap and overlap within 2 px), 3+ drawn tile variants chosen by place. Live, sampled every frame of play: the props that stream in
        /// are drawn behind the hero and every hostile shot (the band and order behind the Player band, so they never cover a bullet at 390x844), rest
        /// on the collider below them, and two of them react with their sound (a hostile shot passing within reach: the bot's own play, then, if
        /// two have not yet, a named setup shortcut spawns an ember beside two reacting props). `check`, `storyprops` events, `storyProps.*` metrics.
        /// </summary>
        public const string StoryPropsId = "story-props";
        const float StoryPropsPlaySeconds = 35f;
        const float StoryPropsSeconds = 420f;
        /// <summary>Two texture pixels of a ledge (202 px per unit), the limit for a gap or an overlap.</summary>
        const float StoryPropsContactLimit = 2f / 202f;
        static readonly string[] StoryPropsLevels = { "Level1", "Floor1000", "Floor999", "Floor998", "Floor997" };
        static readonly string[] StoryPropsEmbers = { "stagedEmber", "demonEmber", "cinderSpark", "cinder" };

        sealed partial class Session
        {
            int propsPassed, propsFailed;
            readonly List<Sfx.PlayedSound> storySounds = new List<Sfx.PlayedSound>();

            sealed class PropWatch
            {
                public Entity Entity;
                public string Id;
                public bool Reacted;
                public float ReactedAt;
                public string Sound;
            }

            void StoryCheck(string beat, bool ok, string detail, bool warnOnly = false)
            {
                if (ok)
                {
                    propsPassed++;
                    Run.Event("check", $"story-props {beat}: ok ({detail})");
                }
                else
                {
                    propsFailed += warnOnly ? 0 : 1;
                    Run.Event("check", $"story-props {beat}: FAILED ({detail})");
                    Run.Problem(warnOnly ? PlaytestSeverity.Warning : PlaytestSeverity.Bug, $"story-props {beat}: {detail}");
                }
            }

            void StoryOnSound(Sfx.PlayedSound p) => storySounds.Add(p);

            static int StoryLayerKey(SpriteRenderer r) => UnityEngine.SortingLayer.GetLayerValueFromID(r.sortingLayerID) * 100000 + r.sortingOrder;

            /// <summary>The sound a reacting prop plays when it enters its react state.</summary>
            static string StoryReactSound(EntityDef def)
            {
                foreach (var state in def.states)
                {
                    if (state.name != "react")
                        continue;
                    foreach (var action in state.onEnter)
                        if (action is PlaySoundAction play)
                            return play.sound;
                }
                return "";
            }

            /// <summary>The props a level places, read from its data: (entity id, world-ish place, the surface it stands on or null).</summary>
            sealed class PlacedProp
            {
                public string Entity;
                public Vector2 At;
                public float SurfaceTop;
                public float SurfaceHalf;
                public Vector2 SurfaceAt;
                public bool HasSurface;
            }

            static bool StoryIsProp(GameContent content, string entity) =>
                entity != null && content.Entities.TryGetValue(entity, out var def) && def.tags.Contains("storyProp");

            static bool StoryIsSurface(GameContent content, string entity) =>
                entity != null && content.Entities.TryGetValue(entity, out var def) && def.tags.Contains("foothold") && def.body.solid;

            static void StoryPlaced(GameContent content, string levelName, List<PlacedProp> props, List<(string entity, Vector2 at)> tiles)
            {
                if (!content.LevelScripts.TryGetValue(levelName, out var script) || script == null)
                    return;
                float ground = GameDefinition.Instance.groundY + GameDefinition.Instance.groundColliderSize.y * 0.5f;
                if (script.world != null && script.world.path != null && script.world.path.Count > 0)
                {
                    foreach (var section in script.world.path)
                    {
                        foreach (var spawn in section.spawns)
                        {
                            if (StoryIsSurface(content, spawn.entity) && content.Entities[spawn.entity].initialStateByPlace)
                                tiles.Add((spawn.entity, spawn.at));
                            if (!StoryIsProp(content, spawn.entity))
                                continue;
                            var placed = new PlacedProp { Entity = spawn.entity, At = spawn.at };
                            foreach (var host in section.spawns)
                            {
                                if (!StoryIsSurface(content, host.entity) || host.anchor != spawn.anchor)
                                    continue;
                                var hostDef = content.Entities[host.entity];
                                float scaleY = host.scale.y * hostDef.visual.scale;
                                float scaleX = host.scale.x * hostDef.visual.scale;
                                float top = host.at.y + hostDef.body.offset.y * scaleY + hostDef.body.size.y * 0.5f * scaleY;
                                float half = hostDef.body.size.x * 0.5f * scaleX;
                                if (Mathf.Abs(spawn.at.x - host.at.x) <= half + 0.05f && Mathf.Abs(spawn.at.y - top) < 0.2f)
                                {
                                    placed.HasSurface = true;
                                    placed.SurfaceTop = top;
                                    placed.SurfaceHalf = half;
                                    placed.SurfaceAt = host.at;
                                    break;
                                }
                            }
                            props.Add(placed);
                        }
                    }
                    return;
                }
                // The Meadow: spawn actions at the start of the first phase, standing on the ground (or its sod).
                foreach (var phase in script.phases)
                {
                    foreach (var action in phase.onEnter)
                    {
                        if (!(action is SpawnAction spawn))
                            continue;
                        if (content.Entities.TryGetValue(spawn.entity, out var def) && def.initialStateByPlace)
                            tiles.Add((spawn.entity, spawn.at));
                        if (StoryIsProp(content, spawn.entity))
                            props.Add(new PlacedProp { Entity = spawn.entity, At = spawn.at, HasSurface = true, SurfaceTop = ground, SurfaceHalf = 7f, SurfaceAt = new Vector2(0f, ground) });
                    }
                    break;
                }
            }

            public IEnumerator StoryPropsRun()
            {
                bool portrait = Screen.height > Screen.width;
                if (portrait)
                {
                    GameplayHud.TouchUiForced = true;
                    Run.Event("shortcut", "portrait screen: the touch UI on so the stick zone is the real one (the bot plays through its virtual pad)");
                }
                var content = GameContent.Current;
                if (content == null)
                {
                    Run.Problem(PlaytestSeverity.Bug, "story-props: no game content loaded");
                    End(PlaytestOutcome.Error);
                    yield break;
                }
                Sfx.Played += StoryOnSound;
                try
                {
                    for (int i = 0; i < StoryPropsLevels.Length && !Run.Done; i++)
                    {
                        yield return StoryPropsLevel(content, StoryPropsLevels[i], i);
                        Run.Progress((i + 1f) / StoryPropsLevels.Length);
                    }
                }
                finally
                {
                    Sfx.Played -= StoryOnSound;
                    GameplayHud.TouchUiForced = false;
                    StopBot();
                }
                if (Run.Done)
                    yield break;
                Run.Metric("storyProps.passed", propsPassed);
                Run.Metric("storyProps.failed", propsFailed);
                End(propsFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }

            IEnumerator StoryPropsLevel(GameContent content, string levelName, int index)
            {
                int n = LevelNumberOf(levelName);
                string L = levelName;
                if (n == 0)
                {
                    StoryCheck($"{L} exists", false, "no such level in game.json");
                    yield break;
                }

                // 1. The level's data: how many props, how many react, each one's contact with its surface, how many tile variants.
                var props = new List<PlacedProp>();
                var tiles = new List<(string entity, Vector2 at)>();
                StoryPlaced(content, levelName, props, tiles);
                int reacting = 0;
                foreach (var p in props)
                    if (content.Entities[p.Entity].tags.Contains("reacts"))
                        reacting++;
                StoryCheck($"{L}: story props placed", props.Count >= 8, $"{props.Count} props (8 wanted)");
                StoryCheck($"{L}: props that react", reacting >= 2, $"{reacting} of them answer a shot or the hero (2 wanted)");
                float worst = 0f;
                string worstProp = "";
                int floating = 0;
                foreach (var p in props)
                {
                    float gap = p.HasSurface ? Mathf.Abs(p.At.y - p.SurfaceTop) : 99f;
                    if (!p.HasSurface || Mathf.Abs(p.At.x - p.SurfaceAt.x) > p.SurfaceHalf + 0.05f || gap > StoryPropsContactLimit)
                        floating++;
                    if (gap > worst)
                    {
                        worst = gap;
                        worstProp = p.Entity;
                    }
                }
                StoryCheck($"{L}: every prop rests on its surface", floating == 0, $"{floating} of {props.Count} off its surface; worst gap {worst * 202f:0.0} px ({worstProp}, limit 2 px)");
                Run.Metric($"storyProps.{L}.props", props.Count);
                Run.Metric($"storyProps.{L}.reacting", reacting);
                var variants = new HashSet<string>();
                foreach (var tile in tiles)
                {
                    var def = content.Entities[tile.entity];
                    if (def.initialStates.Count > 0)
                        variants.Add(def.initialStates[Entity.PlaceHash(tile.at, def.initialStates.Count)]);
                }
                StoryCheck($"{L}: tile variants by place", variants.Count >= 3, $"{variants.Count} of {tiles.Count} placed tiles' variants ({string.Join("", variants)}) by Entity.PlaceHash");
                Run.Metric($"storyProps.{L}.tileVariants", variants.Count);

                // 2. Live: open the level and play it.
                Run.Event("shortcut", $"level {n} ({levelName}) loaded directly");
                StopBot();
                var old = GameplayWorld.Instance;
                var load = LoadLevel(n);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(n), 60f, $"level {n} to start");
                if (Run.Done)
                    yield break;
                var world = GameplayWorld.Instance;
                var health = world.Player != null ? world.Player.GetComponentInChildren<PlayerHealth>() : null;
                if (health != null)
                    health.SetInvincibilityTimer(120f); // setup shortcut: the bot keeps playing so the props stream in; damage is not what this checks

                var watch = new Dictionary<int, PropWatch>();
                var liveTiles = new HashSet<string>();
                int liveTileCount = 0, behindFails = 0, contactFails = 0, contactChecked = 0, behindChecked = 0;
                float worstLive = 0f;
                string behindDetail = "", contactDetail = "";
                bool forced = false;
                string emberId = null;
                foreach (string id in StoryPropsEmbers)
                {
                    if (content.Entities.ContainsKey(id))
                    {
                        emberId = id;
                        break;
                    }
                }
                int soundsBefore = storySounds.Count;
                float startedAt = GameClock.Instance != null ? GameClock.Instance.Time : 0f;

                yield return PlayLevel(n, StoryPropsPlaySeconds, now =>
                {
                    var w = GameplayWorld.Instance;
                    if (w == null || w.Player == null)
                        return null;
                    var heroRenderer = w.PlayerController != null ? w.PlayerController.HeroRenderer : null;
                    int heroKey = heroRenderer != null ? StoryLayerKey(heroRenderer) : int.MinValue;
                    int lowestShot = int.MaxValue;
                    var live = w.Live;
                    for (int i = 0; i < live.Count; i++)
                    {
                        var e = live[i];
                        if (e == null || !e.IsAlive || e.IsPlayer)
                            continue;
                        if (e.Team == Team.Enemy && e.Sprite != null && e.FindAbility<ProjectileAbility>() != null)
                            lowestShot = Mathf.Min(lowestShot, StoryLayerKey(e.Sprite));
                    }
                    for (int i = 0; i < live.Count; i++)
                    {
                        var e = live[i];
                        if (e == null || !e.IsAlive || e.IsPlayer)
                            continue;
                        if (e.Def != null && e.Def.initialStateByPlace && (e.Def.tags.Contains("foothold") || e.Def.tags.Contains("sod")))
                        {
                            if (liveTiles.Add(e.DefId + ":" + e.StateName))
                                Run.Event("storyprops", $"{levelName}: a {e.DefId} draws variant {e.StateName} at {e.Position.x:0.0}, {e.Position.y:0.0}");
                            liveTileCount++;
                            continue;
                        }
                        if (e.Def == null || !e.Def.tags.Contains("storyProp") || e.Sprite == null)
                            continue;
                        if (!watch.TryGetValue(e.Serial, out var pw))
                        {
                            pw = new PropWatch { Entity = e, Id = e.DefId, Sound = StoryReactSound(e.Def) };
                            watch[e.Serial] = pw;
                            // Behind the hero and every hostile shot (draw order), checked once when it appears.
                            int key = StoryLayerKey(e.Sprite);
                            behindChecked++;
                            if (key >= heroKey || key >= lowestShot)
                            {
                                behindFails++;
                                behindDetail = $"{e.DefId} order {key} vs hero {heroKey}, lowest hostile shot {(lowestShot == int.MaxValue ? "none" : lowestShot.ToString())}";
                            }
                            // Rests on the collider below it.
                            var hits = Physics2D.RaycastAll(e.Position + Vector2.up * 0.2f, Vector2.down, 0.7f);
                            foreach (var hit in hits)
                            {
                                if (hit.collider == null || hit.collider.isTrigger || (w.Player.Body != null && hit.collider.attachedRigidbody == w.Player.Body))
                                    continue;
                                contactChecked++;
                                float gap = Mathf.Abs(e.Position.y - hit.point.y);
                                worstLive = Mathf.Max(worstLive, gap);
                                if (gap > StoryPropsContactLimit)
                                {
                                    contactFails++;
                                    contactDetail = $"{e.DefId} at {e.Position.x:0.00}, {e.Position.y:0.00} is {gap * 202f:0.0} px from the {hit.collider.name} below";
                                }
                                break;
                            }
                        }
                        if (!pw.Reacted && e.StateName.Length > 0 && e.StateName != "idle")
                        {
                            pw.Reacted = true;
                            pw.ReactedAt = Time.unscaledTime;
                            Run.Event("storyprops", $"{levelName}: {e.DefId} reacts (state {e.StateName}, animation {e.Animation.CurrentState}), sound {pw.Sound}");
                        }
                    }
                    // A named setup shortcut once the bot has had 18 s: an ember beside two reacting props that have not answered yet.
                    if (!forced && now - startedAt > 18f)
                    {
                        forced = true;
                        int already = 0;
                        foreach (var pair in watch)
                            if (pair.Value.Reacted)
                                already++;
                        if (already < 2 && emberId != null)
                        {
                            int sent = 0;
                            foreach (var pair in watch)
                            {
                                var p = pair.Value;
                                if (p.Reacted || p.Entity == null || !p.Entity.IsAlive || !p.Entity.Def.tags.Contains("reacts"))
                                    continue;
                                if ((p.Entity.Position - (Vector2)w.Player.Position).sqrMagnitude > 100f)
                                    continue; // only the props near him: those are on screen
                                var options = new SpawnOptions { Team = Team.Enemy, Velocity = Vector2.zero, FacePlayer = false, InstanceId = "storyEmber" };
                                w.Spawn(emberId, p.Entity.Position + new Vector2(0.4f, 0.5f), options);
                                Run.Event("shortcut", $"an ember ({emberId}) beside {p.Id}: a shot passing it");
                                if (++sent >= 3)
                                    break;
                            }
                        }
                    }
                    return null;
                });
                if (Run.Done)
                    yield break;
                // The embers need a moment to be noticed.
                yield return WaitReal(1.2f);

                int reacted = 0, withSound = 0;
                foreach (var pair in watch)
                {
                    var p = pair.Value;
                    if (!p.Reacted)
                        continue;
                    reacted++;
                    if (storySounds.Exists(s => s.id == p.Sound))
                        withSound++;
                }
                Run.Event("storyprops", $"{levelName}: {watch.Count} props seen live, {reacted} reacted, {withSound} with their sound, {liveTiles.Count} live tile variants");
                StoryCheck($"{L}: props are drawn behind the hero and every hostile shot", behindChecked > 0 && behindFails == 0,
                    behindFails == 0 ? $"{behindChecked} props seen, all behind (band Background2, under the Player band)" : $"{behindFails} of {behindChecked}: {behindDetail}");
                StoryCheck($"{L}: live props rest on the collider below", contactChecked == 0 || contactFails == 0,
                    contactFails == 0 ? $"{contactChecked} props measured, worst gap {worstLive * 202f:0.0} px" : $"{contactFails} of {contactChecked}: {contactDetail}");
                StoryCheck($"{L}: two props reacted, each with its sound", reacted >= 2 && withSound >= 2,
                    $"{reacted} reacted, {withSound} with their audio.json sound heard ({storySounds.Count - soundsBefore} sounds in all)");
                StoryCheck($"{L}: tile variants on screen", liveTiles.Count >= 3, $"{liveTiles.Count} variants drawn while it played ({string.Join(", ", liveTiles)})", warnOnly: liveTiles.Count >= 2);
                Run.Metric($"storyProps.{L}.seenLive", watch.Count);
                Run.Metric($"storyProps.{L}.reacted", reacted);
                Run.Metric($"storyProps.{L}.liveTileVariants", liveTiles.Count);
                Run.Metric($"storyProps.{L}.worstLiveGapPx", worstLive * 202f);
                Run.Scene($"story props {L}");
            }
        }
    }
}

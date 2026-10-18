using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// The floor generator (generateFloors): fills a stretch of a stage from a Circle's tables. It alternates
    /// breathers and pushes (a few enemies, or one telegraphed hazard), reading the difficulty for the current
    /// floor. Every roll comes from its own seeded stream, saved with the director, so a rewind replays the same
    /// floors.
    /// </summary>
    public sealed partial class LevelDirector
    {
        /// <summary>The tag every generated enemy carries (alongside the action's own tags).</summary>
        public const string GeneratedTag = "generated";

        enum GenMode { Off, Breather, Push, Hazard, Finishing }

        static readonly TargetSelector GeneratedSelector = new TargetSelector { tag = GeneratedTag };

        // ─── Saved state ──────────────────────────────────────────────
        GenMode genMode;
        int genAction = -1;
        string genScope;
        DeterministicRandom genRandom;
        float genNextAt;
        float genPushEndsAt;
        int genToSpawn;
        int genHazard = -1;
        bool genHazardFired;
        int genPushes;

        /// <summary>A generated push is being fought: travel stages hold the camera until it is over.</summary>
        public bool GeneratorHoldsCamera =>
            (genMode == GenMode.Push && (genToSpawn > 0 || world.AnyAlive(GeneratedSelector, null)))
            || (genMode == GenMode.Finishing && world.AnyAlive(GeneratedSelector, null));

        public bool GeneratorRunning => genMode != GenMode.Off;

        /// <summary>A generated push (enemies or a hazard) is on: the Circle's push music plays (StageMusic).</summary>
        public bool GeneratorPushing => genMode == GenMode.Hazard || GeneratorHoldsCamera;

        public void StartGenerator(GenerateFloorsAction action, ActionContext context)
        {
            var circle = GeneratorCircle(action);
            if (circle == null)
            {
                Debug.LogError($"[LevelDirector] generateFloors: no circle \"{(string.IsNullOrEmpty(action.circle) ? script.floors.circle : action.circle)}\".");
                return;
            }
            genAction = IndexOf(action);
            genScope = action.persist ? null : context.Scope;
            genRandom = DeterministicRandom.Derive(world.Seed, 0x6E4F1u + (uint)action.seed * 7919u);
            genMode = GenMode.Breather;
            genNextAt = context.Now + Mathf.Max(0f, action.firstBreather);
            genToSpawn = 0;
            genHazard = -1;
            genHazardFired = false;
            genPushes = 0;
            world.Telemetry.Record("generator", circle.id, world.Stage.Floor);
        }

        public void StopGenerator()
        {
            genMode = GenMode.Off;
        }

        CircleDef GeneratorCircle(GenerateFloorsAction action)
        {
            string id = string.IsNullOrEmpty(action.circle) ? script.floors.circle : action.circle;
            return !string.IsNullOrEmpty(id) && world.Content.Circles.TryGetValue(id, out var circle) ? circle : null;
        }

        void StopGeneratorInScope(string scope)
        {
            if (genMode != GenMode.Off && genScope == scope)
                genMode = GenMode.Off;
        }

        void TickGenerator(float now)
        {
            if (genMode == GenMode.Off)
                return;
            var action = (GenerateFloorsAction)actions[genAction];
            var circle = GeneratorCircle(action);
            if (circle == null)
            {
                genMode = GenMode.Off;
                return;
            }

            int floor = world.Stage.Floor;
            var difficulty = circle.DifficultyAt(floor);
            bool reached = action.untilFloor > 0 && world.Stage.HasFloors && floor <= action.untilFloor;
            int alive = world.CountAlive(GeneratedSelector, null);

            switch (genMode)
            {
                case GenMode.Breather:
                    if (reached)
                        genMode = GenMode.Finishing;
                    else if (now >= genNextAt)
                        StartPush(now, action, circle, floor, difficulty);
                    break;

                case GenMode.Push:
                    if (reached)
                        genToSpawn = 0;
                    if (genToSpawn > 0 && now >= genNextAt && alive < Mathf.Max(1, difficulty.maxAlive))
                    {
                        SpawnGenerated(now, action, circle, floor, difficulty);
                        genToSpawn--;
                        genNextAt = now + Mathf.Max(0.1f, difficulty.spawnInterval);
                        alive++;
                    }
                    if (genToSpawn == 0 && (alive == 0 || now >= genPushEndsAt))
                        EndPush(now, reached, difficulty);
                    break;

                case GenMode.Hazard:
                    if (!genHazardFired && now >= genNextAt)
                    {
                        genHazardFired = true;
                        var hazard = circle.hazards[genHazard];
                        genPushEndsAt = now + Mathf.Max(0.5f, hazard.duration);
                        world.RunActions(hazard.actions, Context(now, null, genPushes, genScope));
                    }
                    if (genHazardFired && now >= genPushEndsAt)
                        EndPush(now, reached, difficulty);
                    break;

                case GenMode.Finishing:
                    if (alive == 0)
                    {
                        genMode = GenMode.Off;
                        world.Telemetry.Record("generatorDone", circle.id, floor);
                        if (!string.IsNullOrEmpty(action.doneSignal))
                            world.Emit(action.doneSignal);
                    }
                    break;
            }
        }

        void EndPush(float now, bool reached, DifficultyDef difficulty)
        {
            genMode = reached ? GenMode.Finishing : GenMode.Breather;
            genNextAt = now + Mathf.Max(0f, difficulty.breather);
        }

        void StartPush(float now, GenerateFloorsAction action, CircleDef circle, int floor, DifficultyDef difficulty)
        {
            genPushes++;
            bool anyEnemy = PickEnemy(circle, floor, roll: false) >= 0;
            int hazard = -1;
            if (!anyEnemy || genRandom.Value() < difficulty.hazardChance)
                hazard = PickHazard(circle, floor);

            if (hazard >= 0)
            {
                var def = circle.hazards[hazard];
                genMode = GenMode.Hazard;
                genHazard = hazard;
                genHazardFired = false;
                genNextAt = now + Mathf.Max(1f, def.telegraph);
                if (!string.IsNullOrEmpty(def.warning))
                    world.Hud.ShowText(def.warning, TextStyle.Subtitle, Color.white, now + Mathf.Max(1f, def.telegraph) + 1f);
                SickOfIt.Core.Audio.Sfx.Play(def.warningSound);
                world.Telemetry.Record("push", string.IsNullOrEmpty(def.name) ? "hazard " + hazard : def.name, floor);
                return;
            }
            if (!anyEnemy)
            {
                // Nothing to send here yet: take another breather.
                genNextAt = now + Mathf.Max(1f, difficulty.breather);
                return;
            }
            genMode = GenMode.Push;
            genToSpawn = Mathf.Max(1, difficulty.pushSize);
            genNextAt = now;
            genPushEndsAt = now + Mathf.Max(1f, difficulty.pushTimeout);
            world.Telemetry.Record("push", "enemies", floor);
        }

        void SpawnGenerated(float now, GenerateFloorsAction action, CircleDef circle, int floor, DifficultyDef difficulty)
        {
            int index = PickEnemy(circle, floor, roll: true);
            if (index < 0)
                return;
            var def = circle.enemies[index];
            Vector2 local = def.areaCenter + new Vector2(
                genRandom.Range(-0.5f, 0.5f) * def.areaSize.x,
                genRandom.Range(-0.5f, 0.5f) * def.areaSize.y);
            var context = Context(now, null, genPushes, genScope);
            var options = SpawnOptions.From(context, "", action.tags, 0, null);
            if (!options.Tags.Contains(GeneratedTag))
                options.Tags.Add(GeneratedTag);
            if (!Mathf.Approximately(difficulty.healthMultiplier, 1f))
                options.Modifiers.Add((Stat.MaxHealth, difficulty.healthMultiplier, 0f));
            // Generated pushes come from ahead; on a climb "ahead" is up, so the area turns with the stage.
            if (script.world.camera == CameraMode.Travel && script.world.travelDirection == TravelDirection.Up)
                local = new Vector2(local.y, local.x);
            world.Spawn(def.entity, world.CameraCenter + local, options);
        }

        static bool Eligible(int fromFloor, float weight, int floor) => weight > 0f && (fromFloor <= 0 || floor <= fromFloor);

        /// <summary>An enemy picked by weight among those allowed on this floor (-1 = none). Without 'roll' it only checks.</summary>
        int PickEnemy(CircleDef circle, int floor, bool roll)
        {
            float total = 0f;
            for (int i = 0; i < circle.enemies.Count; i++)
            {
                var e = circle.enemies[i];
                if (Eligible(e.fromFloor, e.weight, floor))
                    total += e.weight;
            }
            if (total <= 0f)
                return -1;
            if (!roll)
                return 0;
            float pick = genRandom.Range(0f, total);
            int last = -1;
            for (int i = 0; i < circle.enemies.Count; i++)
            {
                var e = circle.enemies[i];
                if (!Eligible(e.fromFloor, e.weight, floor))
                    continue;
                last = i;
                pick -= e.weight;
                if (pick < 0f)
                    return i;
            }
            return last;
        }

        int PickHazard(CircleDef circle, int floor)
        {
            float total = 0f;
            foreach (var h in circle.hazards)
            {
                if (Eligible(h.fromFloor, h.weight, floor))
                    total += h.weight;
            }
            if (total <= 0f)
                return -1;
            float pick = genRandom.Range(0f, total);
            int last = -1;
            for (int i = 0; i < circle.hazards.Count; i++)
            {
                var h = circle.hazards[i];
                if (!Eligible(h.fromFloor, h.weight, floor))
                    continue;
                last = i;
                pick -= h.weight;
                if (pick < 0f)
                    return i;
            }
            return last;
        }

        void SaveGenerator(StateWriter w)
        {
            w.Int((int)genMode);
            w.Int(genAction);
            w.String(genScope);
            w.UInt(genRandom.State);
            w.Float(genNextAt);
            w.Float(genPushEndsAt);
            w.Int(genToSpawn);
            w.Int(genHazard);
            w.Bool(genHazardFired);
            w.Int(genPushes);
        }

        void LoadGenerator(StateReader r)
        {
            genMode = (GenMode)r.Int();
            genAction = r.Int();
            genScope = r.String();
            genRandom.State = r.UInt();
            genNextAt = r.Float();
            genPushEndsAt = r.Float();
            genToSpawn = r.Int();
            genHazard = r.Int();
            genHazardFired = r.Bool();
            genPushes = r.Int();
        }
    }
}

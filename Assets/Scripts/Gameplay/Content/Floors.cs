using System;
using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Json;

namespace SickOfIt.Gameplay.Content
{
    // =====================================================================
    // FLOOR COUNTER
    // =====================================================================

    [Serializable, JsonType("setFloor")]
    [Doc("Sets the floor counter (the HUD's FLOOR readout), e.g. after a shortcut or a cutscene that skips floors.")]
    public sealed class SetFloorAction : GameAction
    {
        public int floor = 1000;
        [Doc("Jump straight there without the milestone lines of the floors in between (a shortcut skips them too).")] public bool skipMilestones;
        public override void Run(ActionContext context) => context.World.Stage.SetFloor(floor, skipMilestones);
    }

    [Serializable, JsonType("climbFloors")]
    [Doc("The hero goes up this many floors (the counter goes down; negative goes back down).")]
    public sealed class ClimbFloorsAction : GameAction
    {
        public int count = 1;
        public override void Run(ActionContext context) => context.World.Stage.SetFloor(context.World.Stage.Floor - count);
    }

    [Serializable, JsonType("floorReached")]
    [Doc("The floor counter is at 'floor' or higher up (a lower number). Use it for milestones and to end a generated stretch.")]
    public sealed class FloorReachedTrigger : Trigger
    {
        public int floor = 1;

        public override bool Check(TriggerContext context)
        {
            var stage = context.World.Stage;
            return stage.HasFloors && stage.Floor <= floor;
        }
    }

    [Serializable, JsonType("distanceReached")]
    [Doc("Travel stages: the camera has travelled at least 'units' world units from the start (the counter \"distance\").")]
    public sealed class DistanceReachedTrigger : Trigger
    {
        public float units = 10f;
        public override bool Check(TriggerContext context) => context.World.Stage.Distance >= units;
    }

    // =====================================================================
    // TRAVEL
    // =====================================================================

    [Serializable, JsonType("lockTravel")]
    [Doc("Travel stages: the camera stops moving forward (an arena moment, a boss, a cutscene) until unlockTravel.")]
    public sealed class LockTravelAction : GameAction
    {
        public override void Run(ActionContext context) => context.World.Effects.TravelLocked = true;
    }

    [Serializable, JsonType("unlockTravel")]
    [Doc("Travel stages: the camera moves forward again after lockTravel.")]
    public sealed class UnlockTravelAction : GameAction
    {
        public override void Run(ActionContext context) => context.World.Effects.TravelLocked = false;
    }

    // =====================================================================
    // FLOOR GENERATOR
    // =====================================================================

    [Serializable, JsonType("generateFloors")]
    [Doc("Fills the stage with floors built from a Circle (content/circles): pushes of enemies from its table and telegraphed hazards, " +
         "with breathers between them, harder the higher the floor. Runs until the floor counter reaches 'untilFloor', its phase ends, or " +
         "stopGenerator; hand-written story beats go in the phases around it. Seeded: same seed, same floors, so rewinds and playtests repeat. " +
         "Generated enemies get the tags in 'tags'.")]
    public sealed class GenerateFloorsAction : GameAction
    {
        [CircleRef, Doc("Circle to build from (empty = the script's floors.circle).")] public string circle = "";
        [Doc("Stop starting pushes once the floor counter reaches this floor (0 = run until the phase ends).")] public int untilFloor;
        [Doc("Quiet seconds before the first push.")] public float firstBreather = 2f;
        [Doc("Tags for every generated enemy (target them with allDead etc.).")] public List<string> tags = new List<string> { "generated" };
        [Doc("Added to the level seed, so two generated stretches in one stage differ.")] public int seed;
        [Doc("Keep running after the phase that started it ends.")] public bool persist;
        [Doc("Signal emitted when it stops at untilFloor and its last push is over.")] public string doneSignal = "floorsDone";
        [Doc("Replay modes: every push fought off takes the hero up one floor (the counter goes down by one), on stages that stand still. " +
             "With untilFloor it ends the stretch there, without it the floors never end (Endless Floor).")] public bool climbs;
        [Doc("Replay modes: the Circle's easiest-to-hardest ramp is walked over this many pushes instead of over its floors (0 = by floor). " +
             "The enemies' fromFloor follow the ramp, as if the hero were that far up the Circle.")] public int rampPushes;
        [Doc("Replay modes: the floor the ramp's enemy tables start at (0 = the Circle's first floor). Enemies come in at their fromFloor, so a ramp that " +
             "starts on 990 has the imps from its first push.")] public int rampStartFloor;
        [Doc("Replay modes: past the ramp, every push is this much 'heat' harder (0 = no more): +1 enemy per push per 1.0, +0.5 alive at once, " +
             "+8% health... (see LevelDirector.Generator). Endless Floor never stops getting worse.")] public float escalation;
        [Doc("Replay modes: each generated enemy comes from a random side of the camera instead of always from ahead.")] public bool bothSides;
        [Doc("Each generated enemy appears this far outside the view edge it comes from, so it is never visible when it spawns (fliers from the side, walkers dropping in from above, always inside the Travel barriers). 0 = inside the Circle's spawn area.")]
        public float edgeMargin = ViewEdge.DefaultMargin;

        public override void Run(ActionContext context) => context.Director.StartGenerator(this, context);
    }

    [Serializable, JsonType("stopGenerator")]
    [Doc("Stops the floor generator (enemies already spawned stay).")]
    public sealed class StopGeneratorAction : GameAction
    {
        public override void Run(ActionContext context) => context.Director.StopGenerator();
    }
}

using System;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Json;

namespace SickOfIt.Gameplay.Content
{
    // =====================================================================
    // Two small pieces for the Overlord's glimpses on the floors (Docs/Story.md "The Overlord on the far ledge"): an entity that is held at a place
    // on the screen (so a far ledge doesn't scroll away on a travelling floor), and a trigger for the hero's rewind.
    // =====================================================================

    [Serializable, JsonType("screenAnchor")]
    [Doc("Holds the entity at a place on the screen: 'offset' from the camera's centre, kept as the camera travels, so far scenery (the Overlord on his ledge) stays where it is " +
         "on screen. After 'driftAfter' seconds it drifts at 'drift' units per second (a walk along the ledge, a ride up on a lift). The place is saved, so a rewind puts it back.")]
    public sealed class ScreenAnchorAbility : Ability
    {
        [Doc("Offset from the camera's centre, in world units.")] public Vector2 offset = new Vector2(4f, 1.8f);
        [Doc("Units per second the place moves by once the drift starts.")] public Vector2 drift;
        [Doc("Seconds after the spawn before it starts to drift.")] public float driftAfter = 1f;

        Vector2 local;

        public override void OnSpawn() => local = offset;

        public override void Tick(float now, float dt)
        {
            if (now - Owner.SpawnTime >= driftAfter)
                local += drift * dt;
            FloorKit.Moved(Owner, Owner.World.CameraCenter + local, dt);
        }

        public override void SaveState(StateWriter writer)
        {
            writer.Float(local.x);
            writer.Float(local.y);
        }

        public override void LoadState(StateReader reader) => local = new Vector2(reader.Float(), reader.Float());
    }

    [Serializable, JsonType("heroRewound")]
    [Doc("A rewind of the hero's just ended: true for 'within' real seconds after it ends (the rewind itself is not rewound, so this reads the clock's own count). " +
         "Combine it with a counter that says a scene is on (allOf) to answer the hero's rewind in the middle of it.")]
    public sealed class HeroRewoundTrigger : Trigger
    {
        [Doc("Real seconds after a rewind ends that it stays true.")] public float within = 2f;

        public override bool Check(TriggerContext context) =>
            GameClock.RewindsEnded > 0 && UnityEngine.Time.unscaledTime - GameClock.LastRewindEndedAt <= within;
    }
}

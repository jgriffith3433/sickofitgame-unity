using System;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Json;

namespace SickOfIt.Gameplay.Content
{
    // =====================================================================
    // RUNE SPANS (Docs/Story.md, "Circle VIII: the Rune Bridges")
    //
    // A bridge piece that is solid only while its rune is lit. Every stage is a function of the game clock (or of where
    // a Watcher is, or whether the hero stands on it), so a rewind brings a vanished span back by itself; the only saved
    // field is the last stage reported, so the telemetry does not repeat itself.
    // =====================================================================

    [Serializable, JsonType("runeSpan")]
    [Doc("A rune span (Circle VIII): a bridge piece that is solid only while its rune is lit. On a fixed beat of 'period' seconds it hums for " +
         "'hum' seconds (the rune brightens, not yet solid), is solid for 'lit' seconds (its last 'dim' seconds blink: it is about to go), then is " +
         "dark and gone until the next beat. 'phase' and 'phasePerUnit' shift the beat so a row of spans is a rhythm (a wave that walks along the " +
         "bridge). 'holdWhileStoodOn' keeps it lit while the hero stands on it (the safe intro floor). 'gazeTag' makes it follow a Watcher instead " +
         "of the clock: solid while a live entity with that tag is within 'gazeRadius' sideways, humming within 'humRadius'. The dark span is " +
         "drawn as a faint outline so the route can be read. Everything comes from the game clock, so a rewind brings a vanished span back.")]
    public sealed class RuneSpanAbility : Ability
    {
        [Doc("Seconds per beat (at least hum + lit + 0.5).")] public float period = 9f;
        [Doc("Seconds the rune hums before the span appears (at least 1: the telegraph).")] public float hum = 1f;
        [Doc("Seconds the span is solid (its last 'dim' seconds blink).")] public float lit = 5.5f;
        [Doc("Seconds of blinking before it goes out (at least 1: the warning).")] public float dim = 1f;
        [Doc("Where in the beat it starts, 0-1 of the period.")] public float phase;
        [Doc("Added to the phase for every world unit of x it stands at: spans in a row light one after another (a wave along the bridge).")]
        public float phasePerUnit;
        [Doc("Stays lit while the hero stands on it (a span that waits for him: the first floor of the Circle).")] public bool holdWhileStoodOn;
        [Doc("Follow a Watcher: lit while a live entity with this tag is within 'gazeRadius' of it sideways (empty = follow the beat).")] public string gazeTag = "";
        [Doc("With 'gazeTag': solid while the nearest gazer is this close sideways.")] public float gazeRadius = 2.4f;
        [Doc("With 'gazeTag': the rune hums (not solid yet) while the gazer is this close sideways.")] public float humRadius = 5f;
        [Doc("Colour of the lit rune and span.")] public Color litColor = new Color(0.55f, 0.95f, 1f, 1f);
        [Doc("Colour of the humming rune.")] public Color humColor = new Color(0.4f, 0.7f, 1f, 1f);
        [SoundRef, Doc("Played when the rune begins to hum (sound id in audio.json; empty = none). Only the span nearest the hero plays it.")] public string humSound = "";

        public const int Dark = 0, Hum = 1, Lit = 2, Dimming = 3;

        Vector2 home;
        Vector2 extent;
        int lastStage = -1;
        bool measured;

        /// <summary>The stage now (Dark, Hum, Lit, Dimming): the playtest and the bot read it.</summary>
        public int Stage { get; private set; } = Dark;

        public bool IsSolid => Stage == Lit || Stage == Dimming;

        public override void OnSpawn()
        {
            home = Owner.Position;
            lastStage = -1;
            measured = false;
            Stage = Dark;
        }

        float Phase() => Mathf.Repeat(phase + home.x * phasePerUnit, 1f);

        float Period() => Mathf.Max(period, Mathf.Max(1f, hum) + lit + 0.5f);

        /// <summary>The beat's stage at a game time (no gaze, no hold).</summary>
        int BeatStage(float now)
        {
            float p = Period();
            float h = Mathf.Max(1f, hum);
            float t = Mathf.Repeat(now - Phase() * p, p);
            if (t < h) return Hum;
            if (t < h + lit - Mathf.Max(1f, dim)) return Lit;
            if (t < h + lit) return Dimming;
            return Dark;
        }

        int GazeStage()
        {
            float best = float.PositiveInfinity;
            foreach (var e in Owner.World.Live)
            {
                if (e == null || !e.IsAlive || e == Owner || !e.Tags.Contains(gazeTag))
                    continue;
                if (Mathf.Abs(e.Position.y - home.y) > 7f)
                    continue;
                best = Mathf.Min(best, Mathf.Abs(e.Position.x - home.x));
            }
            if (best <= gazeRadius * 0.8f) return Lit;
            if (best <= gazeRadius) return Dimming;
            return best <= humRadius ? Hum : Dark;
        }

        /// <summary>
        /// Will it stay solid for at least 'seconds' more? (The bot only jumps for a span that will.) A gazing span or one that holds
        /// while stood on is always answered yes while it is solid.
        /// </summary>
        public bool SolidFor(float now, float seconds)
        {
            if (!IsSolid)
                return false;
            if (!string.IsNullOrEmpty(gazeTag) || holdWhileStoodOn)
                return Stage == Lit || holdWhileStoodOn;
            float p = Period();
            float t = Mathf.Repeat(now - Phase() * p, p);
            return Mathf.Max(1f, hum) + lit - t >= seconds;
        }

        public override void Tick(float now, float dt)
        {
            var col = Owner.MainCollider;
            if (col == null)
                return;
            if (!measured)
            {
                var size = Owner.Def.body.size;
                var scale = Owner.transform.lossyScale;
                extent = new Vector2(Mathf.Abs(scale.x) * size.x * 0.5f, Mathf.Abs(scale.y) * size.y * 0.5f);
                measured = true;
            }

            int stage = string.IsNullOrEmpty(gazeTag) ? BeatStage(now) : GazeStage();
            bool stood = col.enabled && Owner.World.PlayerStandsOn(Owner);
            if (holdWhileStoodOn && stood && stage != Lit)
                stage = Lit;

            if (stage != lastStage)
            {
                if (stage == Hum)
                {
                    FloorKit.Record(Owner, "runeHum");
                    var player = Player;
                    if (!string.IsNullOrEmpty(humSound) && player != null && Mathf.Abs(player.Position.x - home.x) < 3.5f)
                        SickOfIt.Core.Audio.Sfx.Play(humSound);
                }
                else if (stage == Lit)
                    FloorKit.Record(Owner, "runeLit");
                else if (stage == Dark && lastStage >= 0)
                    FloorKit.Record(Owner, "runeOut");
                lastStage = stage;
                Owner.RecordNow();
            }
            Stage = stage;
            col.enabled = IsSolid;

            // Presentation (from the clock, so a rewind plays it backward): the span itself and a rune along its top.
            Color c;
            switch (stage)
            {
                case Hum:
                    c = Color.Lerp(humColor, litColor, 0.5f);
                    c.a = 0.2f + 0.35f * Mathf.Abs(Mathf.Sin(now * 12f));
                    break;
                case Lit:
                    c = litColor;
                    break;
                case Dimming:
                    c = litColor;
                    c.a = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(now * 9f));
                    break;
                default:
                    c = humColor;
                    c.a = 0.12f;
                    break;
            }
            FloorKit.Tint(Owner, c);
            var rune = FloorKit.Child(Owner, "Rune", Owner.Def.visual.layer, SpriteLayers.Platform + 1);
            Color glow = stage == Dark ? new Color(humColor.r, humColor.g, humColor.b, 0.3f) : litColor;
            glow.a *= stage == Hum ? 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(now * 12f)) : 1f;
            rune.color = glow;
            FloorKit.PlaceWorld(Owner, rune, new Vector2(home.x, home.y + extent.y - 0.04f), new Vector2(Mathf.Max(0.2f, extent.x * 1.7f), 0.07f));
        }

        public override void SaveState(StateWriter writer)
        {
            writer.Float(home.x);
            writer.Float(home.y);
            writer.Float(extent.x);
            writer.Float(extent.y);
            writer.Int(lastStage);
            writer.Bool(measured);
        }

        public override void LoadState(StateReader reader)
        {
            home = new Vector2(reader.Float(), reader.Float());
            extent = new Vector2(reader.Float(), reader.Float());
            lastStage = reader.Int();
            measured = reader.Bool();
        }
    }
}

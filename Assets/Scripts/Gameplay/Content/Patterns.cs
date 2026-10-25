using System;
using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Json;

namespace SickOfIt.Gameplay.Content
{
    [Doc("Which way shots go before the pattern spreads them.")]
    public enum Aim
    {
        [Doc("At the player's body.")] Player,
        [Doc("The way the shooter faces (left or right).")] Facing,
        [Doc("The 'direction' field.")] Direction,
        Down,
        Up,
        Left,
        Right,
        [Doc("A random direction each volley (deterministic).")] Random,
        [Doc("Where the player will be when the shot gets there: leads his current velocity (needs the shot's speed; falls back to Player when no lead hits).")] Predictive,
    }

    /// <summary>One projectile to launch.</summary>
    public struct Shot
    {
        public Vector2 Position;
        public Vector2 Direction;
    }

    /// <summary>What a pattern knows when it emits a volley.</summary>
    public struct EmitInput
    {
        public Vector2 Origin;
        /// <summary>Unit aim direction.</summary>
        public Vector2 Aim;
        /// <summary>How many volleys this emitter has fired before (for sweeps and rotation).</summary>
        public int VolleyIndex;
        /// <summary>The visible world area.</summary>
        public Rect View;
        public IRandomSource Random;
    }

    /// <summary>The shape of one volley.</summary>
    [Serializable, JsonPolymorphic]
    [Doc("The shape of one volley of shots.")]
    public abstract class Pattern
    {
        public abstract void Emit(EmitInput input, List<Shot> shots);

        protected static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r);
            float s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }

    [Serializable, JsonType("single")]
    [Doc("One shot along the aim.")]
    public sealed class SinglePattern : Pattern
    {
        public override void Emit(EmitInput input, List<Shot> shots)
        {
            shots.Add(new Shot { Position = input.Origin, Direction = input.Aim });
        }
    }

    [Serializable, JsonType("spread")]
    [Doc("A fan of 'count' shots spanning 'angle' degrees, centered on the aim.")]
    public sealed class SpreadPattern : Pattern
    {
        public int count = 3;
        public float angle = 30f;

        public override void Emit(EmitInput input, List<Shot> shots)
        {
            if (count <= 1)
            {
                shots.Add(new Shot { Position = input.Origin, Direction = input.Aim });
                return;
            }
            for (int i = 0; i < count; i++)
            {
                float offset = -angle * 0.5f + angle * i / (count - 1);
                shots.Add(new Shot { Position = input.Origin, Direction = Rotate(input.Aim, offset) });
            }
        }
    }

    [Serializable, JsonType("radial")]
    [Doc("'count' shots evenly around a full circle, turned by 'rotationPerVolley' degrees each volley (spirals).")]
    public sealed class RadialPattern : Pattern
    {
        public int count = 8;
        public float rotationPerVolley;

        public override void Emit(EmitInput input, List<Shot> shots)
        {
            int n = Mathf.Max(1, count);
            float start = rotationPerVolley * input.VolleyIndex;
            for (int i = 0; i < n; i++)
                shots.Add(new Shot { Position = input.Origin, Direction = Rotate(input.Aim, start + 360f * i / n) });
        }
    }

    [Serializable, JsonType("line")]
    [Doc("'count' parallel shots side by side, 'spacing' apart.")]
    public sealed class LinePattern : Pattern
    {
        public int count = 3;
        public float spacing = 0.3f;

        public override void Emit(EmitInput input, List<Shot> shots)
        {
            Vector2 side = new Vector2(-input.Aim.y, input.Aim.x);
            for (int i = 0; i < count; i++)
            {
                float offset = (i - (count - 1) * 0.5f) * spacing;
                shots.Add(new Shot { Position = input.Origin + side * offset, Direction = input.Aim });
            }
        }
    }

    [Serializable, JsonType("sweep")]
    [Doc("One shot per volley, turning from 'fromAngle' to 'toAngle' (degrees from the aim) over 'steps' volleys, then repeating.")]
    public sealed class SweepPattern : Pattern
    {
        public float fromAngle = -45f;
        public float toAngle = 45f;
        public int steps = 8;
        [Doc("Go back and forth instead of restarting.")] public bool pingPong = true;

        public override void Emit(EmitInput input, List<Shot> shots)
        {
            int n = Mathf.Max(2, steps);
            int i = input.VolleyIndex;
            float t;
            if (pingPong)
            {
                int period = (n - 1) * 2;
                int k = i % period;
                t = (k < n ? k : period - k) / (float)(n - 1);
            }
            else
            {
                t = (i % n) / (float)(n - 1);
            }
            shots.Add(new Shot { Position = input.Origin, Direction = Rotate(input.Aim, Mathf.Lerp(fromAngle, toAngle, t)) });
        }
    }

    [Serializable, JsonType("rain")]
    [Doc("'count' shots from random points along the top of the screen (or across 'width' around the origin if width > 0), falling down. Ignores the aim.")]
    public sealed class RainPattern : Pattern
    {
        public int count = 1;
        [Doc("0 = the whole screen width.")] public float width;
        [Doc("Random tilt, degrees either side of straight down.")] public float angleJitter = 5f;
        [Doc("Start this far below the top edge of the screen.")] public float topInset = 0.2f;

        public override void Emit(EmitInput input, List<Shot> shots)
        {
            float left = width > 0f ? input.Origin.x - width * 0.5f : input.View.xMin;
            float right = width > 0f ? input.Origin.x + width * 0.5f : input.View.xMax;
            float y = input.View.yMax - topInset;
            for (int i = 0; i < count; i++)
            {
                float x = input.Random.Range(left, right);
                float tilt = input.Random.Range(-angleJitter, angleJitter);
                shots.Add(new Shot { Position = new Vector2(x, y), Direction = Rotate(Vector2.down, tilt) });
            }
        }
    }

    [Serializable, JsonType("scatter")]
    [Doc("'count' shots in random directions within 'angle' degrees of the aim.")]
    public sealed class ScatterPattern : Pattern
    {
        public int count = 5;
        public float angle = 60f;

        public override void Emit(EmitInput input, List<Shot> shots)
        {
            for (int i = 0; i < count; i++)
                shots.Add(new Shot { Position = input.Origin, Direction = Rotate(input.Aim, input.Random.Range(-angle * 0.5f, angle * 0.5f)) });
        }
    }
}

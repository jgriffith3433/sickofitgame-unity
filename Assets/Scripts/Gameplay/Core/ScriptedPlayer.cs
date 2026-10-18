using UnityEngine;
using SickOfIt.Core.Input;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// What a level script does with the hero in a scene: walks him to a point through his own movement
    /// (walkPlayerTo), and pulls him into a point, spinning and shrinking, until he is gone (pullPlayerInto).
    /// Its state is part of the world's saved state, so a rewind replays the scene; the player's transform itself
    /// rewinds through its RewindableTransform.
    /// </summary>
    public sealed class ScriptedPlayer
    {
        /// <summary>A walk is over this close to its point (world units), or after this long without getting closer.</summary>
        const float ArriveDistance = 0.05f;
        const float StuckSeconds = 1.5f;

        readonly GameplayWorld world;
        readonly PlayerController controller;
        readonly Transform root;
        readonly Vector3 baseScale = Vector3.one;

        // ─── Saved state ──────────────────────────────────────────────
        bool walking;
        float walkX;
        float walkSpeed;
        string walkSignal = "";
        float walkClosest;
        float walkCloserAt;

        bool pulling;
        bool hidden;
        string pullTarget = "";
        Vector2 pullFrom;
        Vector2 pullTo;
        float pullStart;
        float pullDuration;
        float pullSpin;
        float pullEndScale;
        int pullEase;
        string pullSignal = "";

        public ScriptedPlayer(GameplayWorld world, PlayerController controller)
        {
            this.world = world;
            this.controller = controller;
            root = controller != null ? controller.transform : null;
            if (root != null)
                baseScale = root.localScale;
        }

        public bool Walking => walking;
        public float WalkTargetX => walkX;
        public bool Pulling => pulling;
        /// <summary>Pulled all the way in: hidden, physics off, until the level ends.</summary>
        public bool Hidden => hidden;
        public Vector2 PullPoint => pullTo;

        // ─── Starting ─────────────────────────────────────────────────

        public void WalkTo(float x, float speed, string signal, float now)
        {
            if (controller == null || pulling || hidden)
                return;
            walking = true;
            walkX = x;
            walkSpeed = Mathf.Clamp(speed, 0.05f, 2f);
            walkSignal = signal ?? "";
            walkClosest = float.MaxValue;
            walkCloserAt = now;
            world.Telemetry.Record("cutscene", "walkPlayerTo", Mathf.RoundToInt(x * 100f), signal ?? "");
        }

        public void StopWalking()
        {
            walking = false;
            if (controller != null)
                controller.ScriptedMove = null;
        }

        public void PullInto(string targetId, Vector2 point, float duration, float spin, float endScale, Ease ease, string signal, float now)
        {
            if (controller == null || hidden)
                return;
            StopWalking();
            pulling = true;
            pullTarget = targetId ?? "";
            pullFrom = root.position;
            pullTo = point;
            pullStart = now;
            pullDuration = Mathf.Max(0f, duration);
            pullSpin = spin;
            pullEndScale = Mathf.Max(0f, endScale);
            pullEase = (int)ease;
            pullSignal = signal ?? "";
            controller.SetSuspended(true);
            world.Telemetry.Record("cutscene", "pullPlayerInto", Mathf.RoundToInt(spin), $"{point.x:0.##},{point.y:0.##}");
        }

        // ─── Tick ─────────────────────────────────────────────────────

        public void Tick(float now, float dt)
        {
            if (controller == null)
                return;
            if (walking)
                TickWalk(now, dt);
            if (pulling)
                TickPull(now);
            else if (hidden && root.localScale != Vector3.zero)
                root.localScale = Vector3.zero;
        }

        void TickWalk(float now, float dt)
        {
            float dx = walkX - root.position.x;
            float distance = Mathf.Abs(dx);
            float step = walkSpeed * controller.CurrentMoveSpeed * dt;
            if (distance < walkClosest - 0.01f)
            {
                walkClosest = distance;
                walkCloserAt = now;
            }
            bool arrived = distance <= Mathf.Max(ArriveDistance, step);
            bool stuck = now - walkCloserAt > StuckSeconds;
            if (!arrived && !stuck)
            {
                controller.ScriptedMove = Mathf.Sign(dx) * walkSpeed;
                return;
            }

            StopWalking();
            controller.StopHorizontal();
            world.Telemetry.Record("cutscene", stuck && !arrived ? "walkBlocked" : "walkArrived", Mathf.RoundToInt(root.position.x * 100f), walkSignal);
            if (!string.IsNullOrEmpty(walkSignal))
                world.Emit(walkSignal);
        }

        void TickPull(float now)
        {
            if (!string.IsNullOrEmpty(pullTarget))
            {
                foreach (var e in world.Live)
                {
                    if (e.IsAlive && e.InstanceId == pullTarget)
                    {
                        pullTo = e.Position;
                        break;
                    }
                }
            }
            float t = pullDuration > 0f ? Mathf.Clamp01((now - pullStart) / pullDuration) : 1f;
            float k = Easing.Apply((Ease)pullEase, t);
            Vector2 p = Vector2.Lerp(pullFrom, pullTo, k);
            root.position = new Vector3(p.x, p.y, root.position.z);
            // Positive spin turns clockwise, like water down a drain.
            root.localRotation = Quaternion.Euler(0f, 0f, -pullSpin * k);
            root.localScale = baseScale * Mathf.Lerp(1f, pullEndScale, k);
            if (t < 1f)
                return;

            pulling = false;
            hidden = true;
            root.localScale = Vector3.zero;
            world.Telemetry.Record("cutscene", "playerGone", 0, pullSignal);
            if (!string.IsNullOrEmpty(pullSignal))
                world.Emit(pullSignal);
        }

        // ─── Rewind ───────────────────────────────────────────────────

        public void SaveState(StateWriter w)
        {
            w.Bool(walking);
            w.Float(walkX);
            w.Float(walkSpeed);
            w.String(walkSignal);
            w.Float(walkClosest);
            w.Float(walkCloserAt);

            w.Bool(pulling);
            w.Bool(hidden);
            w.String(pullTarget);
            w.Float(pullFrom.x);
            w.Float(pullFrom.y);
            w.Float(pullTo.x);
            w.Float(pullTo.y);
            w.Float(pullStart);
            w.Float(pullDuration);
            w.Float(pullSpin);
            w.Float(pullEndScale);
            w.Int(pullEase);
            w.String(pullSignal);
        }

        public void LoadState(StateReader r)
        {
            walking = r.Bool();
            walkX = r.Float();
            walkSpeed = r.Float();
            walkSignal = r.String();
            walkClosest = r.Float();
            walkCloserAt = r.Float();

            pulling = r.Bool();
            hidden = r.Bool();
            pullTarget = r.String();
            pullFrom = new Vector2(r.Float(), r.Float());
            pullTo = new Vector2(r.Float(), r.Float());
            pullStart = r.Float();
            pullDuration = r.Float();
            pullSpin = r.Float();
            pullEndScale = r.Float();
            pullEase = r.Int();
            pullSignal = r.String();

            if (controller == null)
                return;
            if (!walking)
                controller.ScriptedMove = null;
            // Back before the pull: physics on again (the transform comes back through its RewindableTransform).
            controller.SetSuspended(pulling || hidden);
        }
    }
}

using UnityEngine;
using SickOfIt.Core.Input;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// The hero's grapple hook (Docs/Story.md "The Hook"; player.json <c>moves.grapple</c>; anchors are entities with the
    /// <c>grappleAnchor</c> ability). A press of the hook key throws it along his aim: the anchor nearest the aim inside the
    /// cone and the range is hooked. A swing anchor holds a rope of the length he hooked it at: he swings on it (A / D pump,
    /// up reels in, down lets out), Jump lets go with the swing's speed, the key again lets go without a jump. A pull anchor
    /// reels him straight in and hops him onto the ledge it sits on. The world's side of it: every number he swings with is
    /// <see cref="SaveState"/>d, so a rewind puts back the rope, the swing and the anchor; the controller's hold is re-made
    /// on the next tick (or let go of by the world when the saved state says he wasn't hooked). Motion is a function of the
    /// game clock's step and saved state only, no random numbers.
    /// </summary>
    public sealed class HeroGrapple
    {
        readonly GameplayWorld world;
        readonly PlayerController controller;

        bool attached;
        bool pulling;
        int anchorSerial;
        float rope;
        Vector2 vel;
        float readyAt = -100f;
        float attachedAt = -100f;

        public HeroGrapple(GameplayWorld world, PlayerController controller)
        {
            this.world = world;
            this.controller = controller;
        }

        GrappleConfig Config
        {
            get
            {
                var def = GameDefinition.Instance;
                return def != null && def.player != null && def.player.moves != null ? def.player.moves.grapple : null;
            }
        }

        public bool Attached => attached;
        public bool Pulling => attached && pulling;
        public int AnchorSerial => attached ? anchorSerial : -1;
        public float RopeLength => rope;
        public Vector2 Velocity => vel;
        public float AttachedAt => attachedAt;

        /// <summary>The anchor the hook would take from here along 'aim' (null when none is in range and cone). Read by the playtest and the anchors' glow.</summary>
        public Entity FindAnchor(Vector2 aim)
        {
            var cfg = Config;
            if (cfg == null || world.Player == null)
                return null;
            Vector2 from = world.PlayerAimPoint;
            Entity best = null;
            float bestScore = float.MaxValue;
            foreach (var e in world.Live)
            {
                if (e == null || !e.IsAlive || !e.Tags.Contains("anchor"))
                    continue;
                var a = e.FindAbility<GrappleAnchorAbility>();
                if (a == null)
                    continue;
                Vector2 to = e.Position - from;
                float distance = to.magnitude;
                float reach = a.reach > 0f ? Mathf.Min(a.reach, cfg.range) : cfg.range;
                if (distance > reach || distance < 0.5f)
                    continue;
                float angle = Vector2.Angle(aim, to);
                if (angle > cfg.aimCone)
                    continue;
                float score = angle + distance * 0.8f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = e;
                }
            }
            return best;
        }

        public void Tick(float now, float dt)
        {
            var cfg = Config;
            var player = world.Player;
            if (controller == null || player == null || cfg == null || dt <= 0f)
                return;
            bool press = controller.ConsumeGrappleRequest();
            controller.GrappleAttached = attached; // his GrappleThrow/GrappleSwing clips (presentation)

            if (!player.IsAlive || controller.Suspended || controller.InputLocked)
            {
                if (attached)
                    Let(now, cfg, "locked", false);
                return;
            }

            if (!attached)
            {
                if (!press || now < readyAt || controller.IsHeld || !controller.GrappleUnlocked)
                    return;
                var target = FindAnchor(controller.GrappleAim());
                if (target == null)
                {
                    world.Telemetry.Record("grapple", "miss", 0);
                    return;
                }
                Attach(now, cfg, target, player);
                // fall through: the first step of the rope is this tick
            }

            var anchor = world.FindBySerial(anchorSerial);
            var anchorAbility = anchor != null && anchor.IsAlive ? anchor.FindAbility<GrappleAnchorAbility>() : null;
            if (anchorAbility == null)
            {
                Let(now, cfg, "anchorGone", false);
                return;
            }

            if (controller.ConsumeJumpOff())
            {
                Let(now, cfg, "jump", true);
                return;
            }
            if (press && attachedAt < now)
            {
                Let(now, cfg, "release", false);
                return;
            }

            Vector2 anchorAt = anchor.Position;
            Vector2 pos = player.Position;
            Vector2 next;
            if (pulling)
            {
                Vector2 to = anchorAt - pos;
                float distance = to.magnitude;
                if (distance <= cfg.pullStop)
                {
                    Vector2 dir = distance > 0.001f ? to / distance : Vector2.up;
                    vel = new Vector2(dir.x * cfg.pullSpeed * 0.3f, cfg.pullHop * controller.GravitySign);
                    Let(now, cfg, "arrived", false);
                    return;
                }
                Vector2 heading = to / distance;
                vel = heading * cfg.pullSpeed;
                next = pos + heading * Mathf.Min(distance, cfg.pullSpeed * dt);
            }
            else
            {
                float ropeMax = anchorAbility.ropeMax > 0f ? Mathf.Min(anchorAbility.ropeMax, cfg.maxRope) : cfg.maxRope;
                Vector2 v = vel;
                v.y -= cfg.gravity * controller.GravitySign * dt;
                v.x += controller.MoveX * cfg.swingPump * dt;
                v *= Mathf.Max(0f, 1f - cfg.damping * dt);
                rope = Mathf.Clamp(rope - controller.MoveY * cfg.reelSpeed * dt, cfg.minRope, ropeMax);
                next = pos + v * dt;
                Vector2 off = next - anchorAt;
                float d = off.magnitude;
                if (d > rope && d > 0.0001f)
                {
                    next = anchorAt + off / d * rope;
                    v = (next - pos) / dt;
                }
                if (v.magnitude > cfg.maxSpeed)
                    v = v.normalized * cfg.maxSpeed;
                vel = v;
                // Feet on the ground again, moving down: he lands rather than being dragged through it.
                if (now - attachedAt > 0.3f && controller.IsGrounded && vel.y * controller.GravitySign <= 0.1f)
                {
                    Let(now, cfg, "land", false);
                    return;
                }
            }
            world.HoldPlayer(next, vel);
        }

        void Attach(float now, GrappleConfig cfg, Entity target, Entity player)
        {
            var a = target.FindAbility<GrappleAnchorAbility>();
            attached = true;
            anchorSerial = target.Serial;
            pulling = a.mode == GrappleMode.Pull;
            attachedAt = now;
            float ropeMax = a.ropeMax > 0f ? Mathf.Min(a.ropeMax, cfg.maxRope) : cfg.maxRope;
            rope = Mathf.Clamp(Vector2.Distance(player.Position, target.Position), cfg.minRope, ropeMax);
            Vector2 v = player.Body != null ? player.Body.linearVelocity : Vector2.zero;
            vel = v.magnitude > cfg.maxSpeed ? v.normalized * cfg.maxSpeed : v;
            controller.ConsumeJumpOff();
            SickOfIt.Core.Audio.Sfx.Play(a.hookSound);
            world.Telemetry.Record("grapple", pulling ? "attachPull" : "attach", Mathf.RoundToInt(rope * 100f), target.InstanceId);
            world.RecordNow();
        }

        // Lets go of the rope: the world's hold ends this tick; the swing's speed goes on (a jump adds a share of its force).
        void Let(float now, GrappleConfig cfg, string why, bool jump)
        {
            if (!attached)
                return;
            attached = false;
            readyAt = now + cfg.cooldown;
            var player = world.Player;
            Vector2 v = vel;
            if (jump)
                v.y = Mathf.Max(v.y * controller.GravitySign, 0f) * controller.GravitySign + controller.JumpSpeed * cfg.releaseJump * controller.GravitySign;
            if (player != null && player.Body != null)
                player.Body.linearVelocity = v;
            controller.KeepMomentum(v.x);
            world.Telemetry.Record("grapple", why, Mathf.RoundToInt(v.magnitude * 100f));
            world.RecordNow();
        }

        // ─── Rewind ──────────────────────────────────────────────────

        public void SaveState(StateWriter w)
        {
            w.Bool(attached);
            w.Bool(pulling);
            w.Int(anchorSerial);
            w.Float(rope);
            w.Float(vel.x);
            w.Float(vel.y);
            w.Float(readyAt);
            w.Float(attachedAt);
        }

        public void LoadState(StateReader r)
        {
            attached = r.Bool();
            pulling = r.Bool();
            anchorSerial = r.Int();
            rope = r.Float();
            vel = new Vector2(r.Float(), r.Float());
            readyAt = r.Float();
            attachedAt = r.Float();
        }
    }
}

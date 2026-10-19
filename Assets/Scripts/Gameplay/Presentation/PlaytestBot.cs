using UnityEngine;
using UnityEngine.InputSystem;
using SickOfIt.Core;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// A simple automatic player for playtests: shoots the nearest enemy, keeps a comfortable distance from
    /// threats, jumps over incoming shots, rewinds when hit and takes the first upgrade offered. It plays through
    /// the real input path, pressing keys and sticks on <see cref="PlaytestInput"/>'s virtual devices, so it hits
    /// what a player would. Its choices come from a seeded random stream, so runs are repeatable.
    /// Used by the Hawkes playtest scenarios (SickOfIt.Playtest) in builds and by the PlayMode playtests.
    /// </summary>
    public sealed class PlaytestBot : MonoBehaviour
    {
        public float preferredDistanceMin = 2.5f;
        public float preferredDistanceMax = 5f;
        public float dodgeRadius = 1.6f;
        /// <summary>
        /// Plays to lose (the waystone scenario, to see a death through real input): walks into the nearest shot or
        /// enemy, never dodges, jumps, fires or rewinds. Still takes the upgrade picker and stops on pause and results.
        /// </summary>
        public bool reckless;
        /// <summary>
        /// Takes Hell's shortcuts when one is offered (the shortcut scenario): presses 2, the shortcut's card. Otherwise
        /// it presses 1 like on every picker, and keeps climbing the long way.
        /// </summary>
        public bool takeShortcuts;
        /// <summary>Holds down wherever it stands (the one-way scenario, on solid ground: it must not drop through).</summary>
        public bool holdDown;
        /// <summary>
        /// The one-way scenario's dead-zone probes. brushDown &gt; 0: the next time he stands on a one-way platform it stops and
        /// taps S for that many seconds (shorter than player.json dropThroughHoldSeconds: he must stay on top). lowRun: while
        /// it runs across a one-way platform it steers with the gamepad's left stick a little low instead of A/D.
        /// </summary>
        public float brushDown;
        public bool lowRun;
        /// <summary>The last brush of down: when it started and ended (Time.time) and what it stood on; -1 = none yet.</summary>
        public float BrushStartedAt { get; private set; } = -1f;
        public float BrushEndedAt { get; private set; } = -1f;
        public Collider2D BrushedOn { get; private set; }
        /// <summary>Seconds it has run across one-way platforms with the stick low (lowRun).</summary>
        public float LowRunSeconds { get; private set; }
        /// <summary>The stick it runs with while lowRun: mostly sideways, a little low (deeper than the old -0.5 drop line).</summary>
        public static readonly Vector2 LowRunStick = new Vector2(0.8f, -0.6f);
        public bool LowRunning { get; private set; }
        float brushUntil = -1f;

        /// <summary>Drops through a one-way platform it started (holding S, or the gamepad's left stick down, every other one).</summary>
        public int DropsStarted { get; private set; }
        /// <summary>What the last drop was held with: "keyboard" (S) or "gamepad" (left stick down).</summary>
        public string LastDropDevice { get; private set; } = "";
        /// <summary>The foothold under him it dropped through a platform for (its climb's next target), or null.</summary>
        public Entity LastDropTarget { get; private set; }
        bool dropping;

        GameplayWorld world;
        PlaytestInput input;
        bool ownsInput;
        DeterministicRandom random;
        float nextJumpCheck;
        float strafeUntil;
        float strafeDirection = 1f;
        int releaseJumpFrame = -1;
        int releasePickFrame = -1;
        Key pickKey = Key.Digit1;

        public Vector2 Move { get; private set; }
        public Vector2 FireDirection { get; private set; }
        /// <summary>Rewinds the bot started (each one a hold of R).</summary>
        public int RewindsStarted { get; private set; }

        /// <summary>Starts playing the level. Uses the given input devices, or makes its own (removed with the bot).</summary>
        public static PlaytestBot Attach(GameplayWorld world, uint seed = 12345, PlaytestInput input = null)
        {
            if (world == null || world.Player == null)
                return null;
            var bot = world.gameObject.AddComponent<PlaytestBot>();
            bot.world = world;
            bot.ownsInput = input == null;
            bot.input = input ?? PlaytestInput.Create();
            bot.random = new DeterministicRandom(seed);
            return bot;
        }

        void OnDestroy()
        {
            if (input == null)
                return;
            if (ownsInput)
                input.Dispose();
            else
                input.ReleaseAll();
        }

        void Press(Key key, ref int releaseFrame)
        {
            input.SetKey(key, true);
            releaseFrame = Time.frameCount + 2;
        }

        void ReleaseTaps()
        {
            if (releaseJumpFrame >= 0 && Time.frameCount >= releaseJumpFrame)
            {
                input.SetKey(Key.Space, false);
                releaseJumpFrame = -1;
            }
            if (releasePickFrame >= 0 && Time.frameCount >= releasePickFrame)
            {
                input.SetKey(pickKey, false);
                releasePickFrame = -1;
            }
        }

        void Idle()
        {
            Move = Vector2.zero;
            FireDirection = Vector2.zero;
            input.SetMoveX(0f);
            input.SetAim(Vector2.zero);
            HoldDown(false);
        }

        /// <summary>Holds down (to drop through what it stands on): S for one drop, the gamepad's left stick for the next.</summary>
        void HoldDown(bool down)
        {
            if (down && !dropping)
            {
                LastDropDevice = DropsStarted % 2 == 0 ? "keyboard" : "gamepad";
                DropsStarted++;
            }
            dropping = down;
            bool stick = down && LastDropDevice == "gamepad";
            input.SetDown(down && !stick);
            input.SetMoveStick(stick ? Vector2.down : Vector2.zero);
        }

        void Update()
        {
            if (world == null || world.Player == null || input == null || !input.Alive)
                return;
            ReleaseTaps();
            if (world.Hud != null && world.Hud.PickerOpen)
            {
                Idle();
                var key = takeShortcuts && world.Hud.ShortcutOpen ? Key.Digit2 : Key.Digit1;
                if (releasePickFrame < 0 && !input.IsHeld(key))
                {
                    pickKey = key;
                    Press(key, ref releasePickFrame);
                }
                return;
            }
            if (world.Hud != null && (world.Hud.Paused || world.Hud.ResultsOpen))
            {
                Idle();
                return;
            }
            var clock = GameClock.Instance;
            float now = clock != null ? clock.Time : Time.time;
            Vector2 me = world.Player.Position;

            Entity target = null;
            Entity threat = null;
            float targetDistance = float.MaxValue;
            float threatDistance = float.MaxValue;
            foreach (var e in world.Live)
            {
                if (!e.IsAlive || !world.Player.IsHostileTo(e))
                    continue;
                float d = Vector2.Distance(me, e.Position);
                bool shootable = e.MaxHealth > 0;
                if (shootable && d < targetDistance)
                {
                    target = e;
                    targetDistance = d;
                }
                if (!shootable && d < threatDistance)
                {
                    threat = e;
                    threatDistance = d;
                }
            }

            if (reckless)
            {
                // Toward the closest thing that hurts: a shot first, else an enemy; with nothing near, on along the stage.
                var into = threat != null ? threat : target;
                float towards = into != null ? Mathf.Sign(into.Position.x - me.x) : 1f;
                if (into != null && Mathf.Abs(into.Position.x - me.x) < 0.15f)
                    towards = 0f;
                FireDirection = Vector2.zero;
                Move = new Vector2(towards, 0f);
                input.SetMoveX(towards);
                input.SetAim(Vector2.zero);
                return;
            }

            FireDirection = target != null ? (target.Position + Vector2.up * 0.1f - me).normalized : Vector2.zero;

            HandleRewind(now);

            float moveX = 0f;
            if (target != null)
            {
                float dx = target.Position.x - me.x;
                // After a boss's first flood he stays back, near the strip it doesn't reach, like a player who has seen it.
                bool floodKnown = target.Serial == floodBoss && world == floodWorld;
                float near = floodKnown ? 6f : preferredDistanceMin, far = floodKnown ? 7.5f : preferredDistanceMax;
                if (targetDistance < near)
                    moveX = -Mathf.Sign(dx);
                else if (targetDistance > far)
                    moveX = Mathf.Sign(dx);
            }
            // A travel stage only ends by going somewhere: with nothing to shoot, keep walking.
            bool travelling = world.Effects.Mode == SickOfIt.Gameplay.Content.CameraMode.Travel
                && world.Effects.Track != null && world.Director.Script.world.travelDirection == SickOfIt.Gameplay.Content.TravelDirection.Right;
            if (travelling && target == null)
                moveX = 1f;
            // A floor with a path: climb it foothold by foothold (no random strafes or hops, they'd drop him in the lava).
            bool climbing = Climb(me, ref moveX, out bool climbJump, out bool climbDrop);
            if (!climbing)
            {
                if (now < strafeUntil)
                    moveX = strafeDirection;
                else if (random.Value() < 0.004f)
                {
                    strafeDirection = random.Value() < 0.5f ? -1f : 1f;
                    strafeUntil = now + random.Range(0.3f, 0.8f);
                }
            }

            // Dodge: find the shot that will pass closest within the next second and step out of its way.
            bool jumpToDodge = false, waveComing = false, waveJump = false;
            float closestMiss = float.MaxValue;
            foreach (var e in world.Live)
            {
                if (!e.IsAlive || e.MaxHealth > 0 || !world.Player.IsHostileTo(e) || e.Body == null)
                    continue;
                Vector2 v = e.Body.linearVelocity;
                Vector2 rel = e.Position - me;
                float speed2 = v.sqrMagnitude;
                if (speed2 < 0.01f)
                    continue;
                // A low wave along the floor (the Magma King's slam): a jump clears it only when timed, so stand still
                // while it comes and jump about 0.2 s before it reaches him, like a player reading it.
                if (Mathf.Abs(v.y) < Mathf.Abs(v.x) * 0.3f && e.MainCollider != null)
                {
                    var wave = e.MainCollider.bounds;
                    if (wave.size.y < 1.2f && wave.size.x > wave.size.y * 1.3f && wave.max.y < me.y + 0.8f && wave.max.y > me.y - 0.5f)
                    {
                        float edgeGap = Mathf.Abs(rel.x) - wave.extents.x - 0.3f;
                        if (Mathf.Sign(v.x) != Mathf.Sign(rel.x) && edgeGap > -0.2f && edgeGap < Mathf.Abs(v.x) * 1.2f)
                        {
                            waveComing = true;
                            if (edgeGap < Mathf.Abs(v.x) * 0.22f)
                                waveJump = true;
                        }
                        continue;
                    }
                }
                float t = Mathf.Clamp(-Vector2.Dot(rel, v) / speed2, 0f, 1f);
                float miss = (rel + v * t).magnitude;
                if (miss > dodgeRadius * 0.45f || miss >= closestMiss)
                    continue;
                closestMiss = miss;
                bool fromAbove = v.y < -Mathf.Abs(v.x);
                if (fromAbove)
                    moveX = (rel.x + v.x * t) > 0f ? -1f : 1f;
                else
                    jumpToDodge = true;
            }
            if (waveComing && !climbing)
                moveX = 0f;

            var view = world.View;
            if (me.x < view.xMin + 0.8f) moveX = 1f;
            if (me.x > view.xMax - 0.8f) moveX = -1f;
            // A wall of shots too tall to jump (the Magma King's lava flood): run away from it instead, and hold at the
            // screen's edge rather than turning back into it. Caught anyway, the rewind above has another go.
            float wall = WallComing(me);
            if (wall != 0f)
            {
                jumpToDodge = false;
                bool atEdge = wall < 0f ? me.x < view.xMin + 0.5f : me.x > view.xMax - 0.5f;
                moveX = atEdge ? 0f : wall;
            }
            bool down = (climbing && climbDrop) || holdDown;
            var controller = world.PlayerController;
            bool onOneWay = controller != null && controller.OnOneWayPlatform;
            // A brush of down on a one-way platform (a short tap of S): it must not drop him.
            if (brushDown > 0f && brushUntil < 0f && onOneWay && !down)
            {
                brushUntil = Time.time + brushDown;
                BrushStartedAt = Time.time;
                BrushedOn = controller.GroundCollider;
            }
            bool brushing = brushUntil >= 0f;
            if (brushing)
            {
                // No hold of its own until the tap is over and S has been let go for a frame.
                down = false;
                if (Time.time >= brushUntil)
                {
                    BrushEndedAt = Time.time;
                    brushDown = 0f;
                    brushUntil = -1f;
                    brushing = false;
                }
            }
            if (down || brushing)
                moveX = 0f;
            Move = new Vector2(moveX, down || brushing ? -1f : 0f);
            input.SetAim(FireDirection);
            if (brushing)
                input.SetDown(true);
            else
                HoldDown(down);
            LowRunning = lowRun && !down && !brushing && moveX != 0f && onOneWay;
            if (LowRunning)
            {
                input.SetMoveX(0f);
                input.SetMoveStick(new Vector2(LowRunStick.x * Mathf.Sign(moveX), LowRunStick.y));
                LowRunSeconds += Time.deltaTime;
            }
            else
                input.SetMoveX(moveX);

            if (climbing)
            {
                if (climbJump && releaseJumpFrame < 0 && !input.IsHeld(Key.Space))
                    Press(Key.Space, ref releaseJumpFrame);
            }
            else if (waveComing)
            {
                // Only the timed jump: an early hop would land him in it.
                if (waveJump && wall == 0f && releaseJumpFrame < 0 && !input.IsHeld(Key.Space))
                    Press(Key.Space, ref releaseJumpFrame);
            }
            else if (now >= nextJumpCheck)
            {
                nextJumpCheck = now + 0.2f;
                if ((jumpToDodge || (threat != null && threatDistance < dodgeRadius * 0.5f) || random.Value() < 0.04f)
                    && releaseJumpFrame < 0 && !input.IsHeld(Key.Space))
                    Press(Key.Space, ref releaseJumpFrame);
            }
        }

        // ─── Floors with a path: climb like a player would ───────────

        /// <summary>The foothold the bot is heading for on a floor with a path (null = none in reach).</summary>
        public Entity ClimbTarget { get; private set; }
        float lastClimbJump = -99f;
        // The swing's speed toward the way on last tick, while he hangs on a chain (it peaks at the bottom of the arc).
        float swingSpeedBefore;

        /// <summary>Footholds it gave up on: it got no closer to one for GiveUpSeconds (behind the screen's edge, under a lip), so it tried another.</summary>
        public int GaveUpOn { get; private set; }
        const float GiveUpSeconds = 5f, SkipSeconds = 5f;
        Entity chasing, skipped;
        float chaseSince, chaseClosest, skipUntil;
        // Where his feet were when the bot last jumped for a foothold: in the air it keeps judging footholds from there, like a
        // player who picked the stone before jumping. Judged from his rising feet, the stone he jumped for dropped out of reach
        // half way up and one 2.6 over the take-off "came into reach" (Floor 1000: stone2 to stone3 turned into a jump at the ledge).
        // The stone he jumped off is left out meanwhile: rising back as it stops sinking, it would read "higher" from there.
        float takeoffFeet;
        bool fromTakeoff;
        Entity takeoffFrom;
        // Hung in the air on a foothold's corner: his middle beside its edge, pushed into it, held there by the contact's
        // friction, not grounded (no jump) and standing on nothing (Floor 907: thrown up by gapVent0 under the crumbling stone's
        // corner at (13.7, 4.4), 160 s). Let go the other way for a moment so he drops off it, like a player would, then climb again.
        float wedgedSince = -1f, wedgeCheckedAt, backOffUntil;
        float backOffWay;

        /// <summary>Times it found him hung on a foothold's corner in the air and let go to drop off it.</summary>
        public int Unwedged { get; private set; }

        /// <summary>
        /// On a floor with a path (world.path), picks the next foothold ahead along the current section (a stone, a raft, a ledge,
        /// a chain's handle) within a jump, walks to it and jumps at the right moment (up through a one-way piece above him, too);
        /// with the next one right under the one-way piece he stands on, holds down to drop through; on a chain, lets go when
        /// the swing carries him forward. Only real input: it moves, presses Space and holds down. False when the level has no
        /// path or the section is an arena.
        /// </summary>
        bool Climb(Vector2 me, ref float moveX, out bool jump, out bool drop)
        {
            jump = false;
            drop = false;
            ClimbTarget = null;
            var effects = world.Effects;
            var controller = world.PlayerController;
            var section = effects != null && effects.HasPath ? effects.CurrentSection : null;
            if (section == null || controller == null || controller.BodyCollider == null)
                return false;
            Vector2 forward = section.Axis;
            if (forward == Vector2.zero)
                return false;
            var feetBounds = controller.BodyCollider.bounds;
            float feet = feetBounds.min.y;
            float now = Time.time;

            if (controller.IsHeld)
            {
                // Hanging on a chain: let go just past the bottom of a swing toward the way on, at its fastest (the swing
                // slows from there), the way a player times it. Letting go as soon as it moves forward threw him short.
                moveX = 0f;
                fromTakeoff = false;
                Vector2 v = world.Player.Body != null ? world.Player.Body.linearVelocity : Vector2.zero;
                float way = Mathf.Abs(forward.x) > 0.1f ? Mathf.Sign(forward.x) : NextFootholdSide(me);
                float speed = v.x * way;
                if (speed > 1.2f && speed < swingSpeedBefore - 0.01f && now - lastClimbJump > 0.3f)
                {
                    jump = true;
                    lastClimbJump = now;
                }
                swingSpeedBefore = speed;
                return true;
            }
            swingSpeedBefore = 0f;

            if (now < backOffUntil)
            {
                moveX = backOffWay;
                fromTakeoff = false;
                return true;
            }

            // In an updraft's column (957's chimney, 952's pile): ride it up and step out over the foothold it throws him to.
            if (!controller.IsGrounded && RideUpdraft(me, feetBounds, forward, ref moveX))
            {
                fromTakeoff = false;
                return true;
            }

            // In the air from its own jump (not thrown by a burn, not off a chain or a column): footholds are judged from the
            // take-off, until he lands, falls under it (a miss) or rises past a jump's reach (thrown). The press takes a tick or
            // two to lift him, so he still reads grounded just after it.
            float fromY = feet;
            if (now - lastClimbJump > 1.2f || (controller.IsGrounded && now - lastClimbJump > 0.25f)
                || feet < takeoffFeet - 0.5f || feet > takeoffFeet + 2.4f)
                fromTakeoff = false;
            bool airFromTakeoff = fromTakeoff && !controller.IsGrounded;
            if (airFromTakeoff)
                fromY = takeoffFeet;

            // In the air, the top of this jump: a new foothold above it is out of reach until he lands (the one he jumped for stays
            // the target). Without this, a one-way ledge hanging right over his stone (Floor 1000's, over stone2) came "in reach"
            // on the way up, the bot steered under it instead of on to the stone it jumped for, fell back short of its top, and
            // did it again for the whole level (beta run r_de7787efe892; the give-up timer never ran: it restarts while airborne).
            bool airborne = !controller.IsGrounded;
            float jumpTop = feet;
            if (airborne && world.Player.Body != null)
            {
                float g = Mathf.Max(0.1f, Mathf.Abs(Physics2D.gravity.y) * world.Player.Body.gravityScale);
                float vy = Mathf.Max(0f, world.Player.Body.linearVelocity.y);
                jumpTop = feet + vy * vy / (2f * g);
            }

            Entity standingOn = null;
            Entity best = null;
            float bestScore = float.MaxValue;
            Bounds bestBounds = default;
            bool bestIsChain = false;
            foreach (var e in world.Live)
            {
                if (e == null || !e.IsAlive || e.IsPlayer)
                    continue;
                bool chain = e.FindAbility<SickOfIt.Gameplay.Content.SwingChainAbility>() != null;
                // An imp riding a hauling chain (impSwing) swings the same way, but it burns: never jump into it.
                if (chain && world.Player.IsHostileTo(e))
                    continue;
                if (!chain && !e.Tags.Contains("foothold"))
                    continue;
                if (e == skipped && now < skipUntil)
                    continue;
                if (e == takeoffFrom && airFromTakeoff)
                    continue;
                var col = e.MainCollider;
                if (col == null || !col.enabled)
                    continue;
                if (!chain && world.PlayerStandsOn(e))
                {
                    standingOn = e;
                    continue;
                }
                var crumble = e.FindAbility<SickOfIt.Gameplay.Content.CrumbleAbility>();
                if (crumble != null && crumble.Falling)
                    continue;
                var sink = e.FindAbility<SickOfIt.Gameplay.Content.SinkAbility>();
                if (sink != null && sink.Sunk > sink.depth * 0.6f)
                    continue;
                var b = col.bounds;
                float topY = chain ? e.Position.y - 0.5f : b.max.y;
                float rise = topY - fromY;
                if (rise > (chain ? 2.6f : 1.9f) || rise < -3.5f)
                    continue;
                if (airborne && !chain && e != chasing && topY > jumpTop + 0.05f)
                    continue;
                float edge = Mathf.Max(0f, Mathf.Max(b.min.x - me.x, me.x - b.max.x));
                if (edge > 3.2f)
                    continue;
                // Ahead along the path: higher on a climb, further on a run.
                Vector2 point = new Vector2(Mathf.Clamp(me.x, b.min.x, b.max.x), topY);
                float gain = Vector2.Dot(point - new Vector2(me.x, fromY), forward);
                if (Mathf.Abs(forward.x) > 0.1f)
                    gain = Mathf.Max(gain, (b.center.x - me.x) * Mathf.Sign(forward.x) - 0.3f);
                if (gain < 0.35f)
                    continue;
                float score = gain + edge * 0.8f - (chain ? 0.6f : 0f);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = e;
                    bestBounds = b;
                    bestIsChain = chain;
                }
            }

            bool grounded = controller.IsGrounded;
            // Resting on what he stands on but not grounded: his middle is past its edge (on a corner, or across the gap between
            // two stones), where the feet's ground check finds nothing and a jump does nothing. Step on to it, like a player.
            // Floor 1000 on beta: wedged between stone2 and a sunk stone3 for 190 s, the bot waiting to land.
            if (!grounded && standingOn != null && standingOn.MainCollider != null && world.Player.Body != null
                && Mathf.Abs(world.Player.Body.linearVelocity.y) < 0.5f && now - lastClimbJump > 0.35f)
            {
                var own = standingOn.MainCollider.bounds;
                float middle = feetBounds.center.x;
                if (middle < own.min.x + 0.3f || middle > own.max.x - 0.3f)
                {
                    moveX = Mathf.Sign(own.center.x - middle);
                    Wedged(grounded, now, ref moveX);
                    return true;
                }
            }
            if (best == null)
            {
                // Nothing in reach: ride what carries him (a raft), else an updraft ahead, else keep going the path's way.
                bool riding = standingOn != null && standingOn.FindAbility<SickOfIt.Gameplay.Content.RaftAbility>() != null;
                if (!riding && grounded && ApproachUpdraft(me, feet, forward, ref moveX, out bool hop))
                {
                    if (hop && now - lastClimbJump > 0.35f)
                    {
                        jump = true;
                        lastClimbJump = now;
                    }
                    return true;
                }
                // Climbing, on a sinking stone gone too deep for the next one: hop on the spot, like a player. It rises while he's in
                // the air (riseSpeed) more than it sinks while he stands, until the next stone is in reach again.
                var sunkStone = standingOn != null ? standingOn.FindAbility<SickOfIt.Gameplay.Content.SinkAbility>() : null;
                if (grounded && Mathf.Abs(forward.x) <= 0.1f && sunkStone != null && sunkStone.riseSpeed > 0f && sunkStone.Sunk > sunkStone.dip + 0.1f)
                {
                    moveX = 0f;
                    if (now - lastClimbJump > 0.35f)
                    {
                        // On the spot: judged from the take-off, a foothold that only comes in reach from his rising feet isn't
                        // steered for (on Floor 1000 a late steer for the ledge off a sunk stone3 drops him back to stone2).
                        jump = true;
                        lastClimbJump = now;
                        takeoffFeet = feet;
                        takeoffFrom = standingOn;
                        fromTakeoff = true;
                    }
                    return true;
                }
                moveX = riding ? 0f : Mathf.Abs(forward.x) > 0.1f ? Mathf.Sign(forward.x) : 0f;
                return true;
            }
            ClimbTarget = best;

            float targetX = bestIsChain ? best.Position.x : Mathf.Clamp(me.x, bestBounds.min.x + 0.35f, bestBounds.max.x - 0.35f);
            float dx = targetX - me.x;
            float edgeGap = bestIsChain ? Mathf.Abs(dx) : Mathf.Max(0f, Mathf.Max(bestBounds.min.x - me.x, me.x - bestBounds.max.x));
            float up = (bestIsChain ? best.Position.y - 0.5f : bestBounds.max.y) - feet;
            moveX = Mathf.Abs(dx) > 0.12f ? Mathf.Sign(dx) : 0f;

            if (!bestIsChain && Wedged(grounded, now, ref moveX))
                return true;

            // A chain swinging out over the gap: wait at the edge for its handle to come back in reach, don't walk off after it.
            if (bestIsChain && grounded && standingOn != null && standingOn.MainCollider != null)
            {
                var own = standingOn.MainCollider.bounds;
                if (dx > 0f ? own.max.x - me.x < 0.3f : me.x - own.min.x < 0.3f)
                    moveX = 0f;
            }

            // A geyser telling or blowing, or a lava fall pouring or about to, across the way to it: wait for the beat on solid
            // footing, like a player counting it (never on a stone that sinks or crumbles, and never standing in it).
            if (grounded && !bestIsChain && standingOn != null && SafeToWait(standingOn)
                && HazardAcross(me.x, targetX, feet, Mathf.Max(feet, bestBounds.max.y) + (up > 0.3f || edgeGap > 0.05f ? 2.3f : 1f)))
            {
                moveX = 0f;
                return true;
            }

            // Getting no closer to it for a while, standing on solid footing (not riding a raft that brings it near):
            // it's out of reach (behind the screen's edge, under a lip). Leave it a few seconds, hop, and try the next one.
            float distance = new Vector2(dx, up).magnitude;
            bool carried = standingOn != null && standingOn.FindAbility<SickOfIt.Gameplay.Content.RaftAbility>() != null;
            if (best != chasing)
            {
                chasing = best;
                chaseSince = now;
                chaseClosest = distance;
            }
            else if (distance < chaseClosest - 0.25f || !grounded || carried)
            {
                chaseClosest = Mathf.Min(chaseClosest, distance);
                chaseSince = now;
            }
            else if (now - chaseSince > GiveUpSeconds)
            {
                skipped = best;
                skipUntil = now + SkipSeconds;
                chasing = null;
                GaveUpOn++;
                if (now - lastClimbJump > 0.35f)
                {
                    jump = true;
                    lastClimbJump = now;
                    takeoffFeet = feet;
                    takeoffFrom = standingOn;
                    fromTakeoff = true;
                }
                return true;
            }

            // The next foothold is right under the one-way piece he stands on: drop through it.
            if (!bestIsChain && grounded && up < -0.4f && controller.OnOneWayPlatform
                && me.x > bestBounds.min.x + 0.2f && me.x < bestBounds.max.x - 0.2f)
            {
                moveX = 0f;
                drop = true;
                LastDropTarget = best;
                return true;
            }

            if (grounded && now - lastClimbJump > 0.35f)
            {
                bool higher = up > 0.3f && edgeGap < (bestIsChain ? 1.0f : 1.3f);
                // Same height or lower across a gap: jump from the edge of what he stands on.
                bool atEdge = false;
                if (!higher && !bestIsChain && standingOn != null && standingOn.MainCollider != null && edgeGap > 0.05f)
                {
                    var own = standingOn.MainCollider.bounds;
                    atEdge = dx > 0f ? own.max.x - me.x < 0.4f : me.x - own.min.x < 0.4f;
                }
                if (higher || atEdge)
                {
                    jump = true;
                    lastClimbJump = now;
                    takeoffFeet = feet;
                    takeoffFrom = standingOn;
                    fromTakeoff = true;
                }
            }
            return true;
        }

        /// <summary>
        /// Pushing at a foothold in the air ('moveX') but not moving for 0.3 s: hung on its corner (see wedgedSince). Starts
        /// letting go the other way (moveX turned round, kept for 0.3 s) so he drops off it, and forgets the chase.
        /// </summary>
        bool Wedged(bool grounded, float now, ref float moveX)
        {
            Vector2 velocity = world.Player.Body != null ? world.Player.Body.linearVelocity : Vector2.one;
            // Only a stall seen on every tick counts (not one left over from a climb that took another branch meanwhile).
            bool sinceLastTick = now - wedgeCheckedAt < 0.25f;
            wedgeCheckedAt = now;
            if (grounded || moveX == 0f || velocity.sqrMagnitude >= 0.3f * 0.3f || !sinceLastTick)
            {
                wedgedSince = -1f;
                return false;
            }
            if (wedgedSince < 0f)
                wedgedSince = now;
            if (now - wedgedSince <= 0.3f)
                return false;
            wedgedSince = -1f;
            backOffWay = -moveX;
            backOffUntil = now + 0.3f;
            chasing = null;
            fromTakeoff = false;
            Unwedged++;
            moveX = backOffWay;
            return true;
        }

        static bool SafeToWait(Entity standingOn) =>
            standingOn.FindAbility<SickOfIt.Gameplay.Content.SinkAbility>() == null
            && standingOn.FindAbility<SickOfIt.Gameplay.Content.CrumbleAbility>() == null
            && standingOn.FindAbility<SickOfIt.Gameplay.Content.CounterweightAbility>() == null;

        /// <summary>
        /// A timed hazard that is live or about to be (a geyser telling or blowing, a lava fall pouring or due within a second)
        /// across the corridor from his x to 'toX' between 'low' and 'high', but not where he stands now (standing in it, go).
        /// </summary>
        bool HazardAcross(float fromX, float toX, float low, float high)
        {
            float x0 = Mathf.Min(fromX, toX) - 0.35f, x1 = Mathf.Max(fromX, toX) + 0.35f;
            float now = world.Now;
            foreach (var e in world.Live)
            {
                if (e == null || !e.IsAlive)
                    continue;
                float h0, h1, hy0, hy1;
                var geyser = e.FindAbility<SickOfIt.Gameplay.Content.GeyserAbility>();
                var fall = geyser == null ? e.FindAbility<SickOfIt.Gameplay.Content.LavaFallAbility>() : null;
                if (geyser != null)
                {
                    if (geyser.Phase < 1)
                        continue;
                    h0 = e.Position.x - geyser.width * 0.5f;
                    h1 = e.Position.x + geyser.width * 0.5f;
                    hy0 = e.Position.y;
                    hy1 = e.Position.y + geyser.height;
                }
                else if (fall != null)
                {
                    if (fall.UntilPour(now) > 1.1f)
                        continue;
                    // Its collider is off between pours: measure the body from the definition.
                    Vector2 scale = new Vector2(Mathf.Abs(e.transform.lossyScale.x), Mathf.Abs(e.transform.lossyScale.y));
                    Vector2 size = Vector2.Scale(e.Def.body.size, scale);
                    Vector2 c = e.Position + Vector2.Scale(e.Def.body.offset, scale);
                    h0 = c.x - size.x * 0.5f;
                    h1 = c.x + size.x * 0.5f;
                    hy0 = c.y - size.y * 0.5f;
                    hy1 = c.y + size.y * 0.5f;
                }
                else
                    continue;
                if (h1 < x0 || h0 > x1 || hy1 < low || hy0 > high)
                    continue;
                if (fromX + 0.3f > h0 && fromX - 0.3f < h1)
                    continue;
                return true;
            }
            return false;
        }

        /// <summary>How far along the path's way a foothold or column top is from a point (higher on a climb, further on a run).</summary>
        static float Gain(Bounds b, float top, Vector2 me, float fromY, Vector2 forward)
        {
            Vector2 point = new Vector2(Mathf.Clamp(me.x, b.min.x, b.max.x), top);
            float gain = Vector2.Dot(point - new Vector2(me.x, fromY), forward);
            if (Mathf.Abs(forward.x) > 0.1f)
                gain = Mathf.Max(gain, (b.center.x - me.x) * Mathf.Sign(forward.x) - 0.3f);
            return gain;
        }

        static float GapX(Bounds a, Bounds b) => Mathf.Max(0f, Mathf.Max(b.min.x - a.max.x, a.min.x - b.max.x));

        /// <summary>
        /// Carried up an updraft's column: picks the foothold furthest along the way that the column's throw (its lift out of the
        /// top) carries his feet 0.25 over, holds in the column on that side until his rise clears it, then steps out onto it.
        /// Like a player: stepping out early drops him back into the lava. False when he isn't in a column or nothing is ahead.
        /// </summary>
        bool RideUpdraft(Vector2 me, Bounds body, Vector2 forward, ref float moveX)
        {
            Bounds column = default;
            float lift = 0f;
            bool inside = false;
            foreach (var e in world.Live)
            {
                if (e == null || !e.IsAlive)
                    continue;
                var draft = e.FindAbility<SickOfIt.Gameplay.Content.UpdraftAbility>();
                var col = draft != null ? e.MainCollider : null;
                if (col == null || !col.bounds.Intersects(body))
                    continue;
                column = col.bounds;
                lift = draft.lift;
                inside = true;
                break;
            }
            if (!inside || world.Player.Body == null)
                return false;
            float g = Mathf.Max(0.1f, Mathf.Abs(Physics2D.gravity.y) * world.Player.Body.gravityScale);
            float vy = Mathf.Max(0f, world.Player.Body.linearVelocity.y);
            float feet = body.min.y;
            float throwTop = column.max.y + lift * lift / (2f * g);

            Entity target = null;
            Bounds tb = default;
            float bestGain = 0.35f;
            foreach (var e in world.Live)
            {
                if (e == null || !e.IsAlive || e.IsPlayer || !e.Tags.Contains("foothold"))
                    continue;
                var col = e.MainCollider;
                if (col == null || !col.enabled)
                    continue;
                var crumble = e.FindAbility<SickOfIt.Gameplay.Content.CrumbleAbility>();
                if (crumble != null && crumble.Falling)
                    continue;
                var b = col.bounds;
                if (b.max.y > throwTop - 0.25f || b.max.y < column.min.y - 3f || GapX(column, b) > 1.5f)
                    continue;
                // Measured from the column's foot, so a foothold he has already risen past still counts.
                float gain = Gain(b, b.max.y, me, column.min.y, forward);
                if (gain > bestGain)
                {
                    bestGain = gain;
                    target = e;
                    tb = b;
                }
            }
            if (target == null)
                return false;
            ClimbTarget = target;
            float targetX = Mathf.Clamp(me.x, tb.min.x + 0.35f, tb.max.x - 0.35f);
            // Where his rise carries his feet from here, the column left behind (it only adds to that).
            float reach = feet + vy * vy / (2f * g);
            float goX = reach >= tb.max.y + 0.25f ? targetX : Mathf.Clamp(targetX, column.min.x + 0.1f, column.max.x - 0.1f);
            float dx = goX - me.x;
            moveX = Mathf.Abs(dx) > 0.12f ? Mathf.Sign(dx) : 0f;
            return true;
        }

        /// <summary>
        /// No foothold within a jump: an updraft column ahead whose top is above him and whose foot he can get into (walking off
        /// his foothold into it, or a hop when its foot is above his). Heads into it; RideUpdraft takes over inside.
        /// </summary>
        bool ApproachUpdraft(Vector2 me, float feet, Vector2 forward, ref float moveX, out bool hop)
        {
            hop = false;
            Bounds best = default;
            float bestEdge = float.MaxValue;
            foreach (var e in world.Live)
            {
                if (e == null || !e.IsAlive || e.FindAbility<SickOfIt.Gameplay.Content.UpdraftAbility>() == null)
                    continue;
                var col = e.MainCollider;
                if (col == null)
                    continue;
                var b = col.bounds;
                if (b.max.y < feet + 0.6f || b.min.y > feet + 1.2f)
                    continue;
                float edge = Mathf.Max(0f, Mathf.Max(b.min.x - me.x, me.x - b.max.x));
                if (edge > 3.2f || edge >= bestEdge || Gain(b, b.max.y, me, feet, forward) < 0.35f)
                    continue;
                bestEdge = edge;
                best = b;
            }
            if (bestEdge == float.MaxValue)
                return false;
            float dx = best.center.x - me.x;
            moveX = Mathf.Abs(dx) > 0.12f ? Mathf.Sign(dx) : 0f;
            hop = best.min.y > feet + 0.1f && bestEdge < 0.8f;
            return true;
        }

        /// <summary>Which side the nearest foothold above him is on (-1 left, 1 right).</summary>
        float NextFootholdSide(Vector2 me)
        {
            float best = float.MaxValue, side = 1f;
            foreach (var e in world.Live)
            {
                if (e == null || !e.IsAlive || !e.Tags.Contains("foothold") || e.MainCollider == null)
                    continue;
                var b = e.MainCollider.bounds;
                if (b.max.y < me.y - 1f)
                    continue;
                float d = Mathf.Abs(b.center.x - me.x) + Mathf.Abs(b.max.y - me.y);
                if (d < best)
                {
                    best = d;
                    side = Mathf.Sign(b.center.x - me.x);
                }
            }
            return side;
        }

        // The boss whose flood he has seen (its Serial; -1 = none): he keeps his distance from it from then on.
        int floodBoss = -1;
        GameplayWorld floodWorld;

        /// <summary>
        /// A wall rolling at the bot: hostile shots moving sideways toward it, stacked over a column taller than a jump
        /// clears, within a few units. Returns the way to run (-1 left, 1 right), or 0 when there is none.
        /// </summary>
        float WallComing(Vector2 me)
        {
            float bestDistance = 6f, flee = 0f;
            // Its warning glow on the floor (tag floodWarning): a player runs as it lights, away from the boss sending it.
            foreach (var w in world.Live)
            {
                if (w == null || !w.IsAlive || !w.Tags.Contains("floodWarning"))
                    continue;
                Entity boss = null;
                foreach (var b in world.Live)
                    if (b != null && b.IsAlive && b.Tags.Contains("boss") && (boss == null || Mathf.Abs(b.Position.x - me.x) < Mathf.Abs(boss.Position.x - me.x)))
                        boss = b;
                if (boss != null)
                {
                    floodBoss = boss.Serial;
                    floodWorld = world;
                    return boss.Position.x > me.x ? -1f : 1f;
                }
            }
            foreach (var e in world.Live)
            {
                if (!e.IsAlive || e.MaxHealth > 0 || !world.Player.IsHostileTo(e) || e.Body == null)
                    continue;
                Vector2 v = e.Body.linearVelocity;
                float dx = e.Position.x - me.x;
                if (Mathf.Abs(v.x) < 0.5f || Mathf.Abs(v.y) > Mathf.Abs(v.x) * 0.3f || Mathf.Sign(v.x) == Mathf.Sign(dx) || Mathf.Abs(dx) >= bestDistance)
                    continue;
                float low = e.Position.y, high = e.Position.y;
                foreach (var o in world.Live)
                {
                    if (o == e || !o.IsAlive || o.MaxHealth > 0 || !world.Player.IsHostileTo(o) || Mathf.Abs(o.Position.x - e.Position.x) > 0.5f)
                        continue;
                    low = Mathf.Min(low, o.Position.y);
                    high = Mathf.Max(high, o.Position.y);
                }
                if (high - low < 1.6f)
                    continue;
                bestDistance = Mathf.Abs(dx);
                flee = -Mathf.Sign(dx);
            }
            return flee;
        }

        // ─── Rewind like a player would: when hit, hold R for about a second if there's juice ───

        public bool useRewind = true;
        int lastHealth = -1;
        float rewindTarget = -1f;
        float holdStartedAt = -1f;

        /// <summary>Holds R to rewind about 'seconds' of game time (if the game allows it now). Returns true if it pressed.</summary>
        public bool RewindNow(float seconds = 1.2f)
        {
            var clock = GameClock.Instance;
            if (clock == null || clock.IsRewinding || input == null || input.IsHeld(Key.R))
                return false;
            var budget = SickOfIt.Core.Rewind.RewindBudget.Instance;
            if (budget != null && !budget.CanRewind)
                return false;
            // A scripted scene locked rewind (lockRewind): a player would see the button say so and not try.
            if (SickOfIt.Core.Rewind.RewindInput.Locked)
                return false;
            rewindTarget = clock.Time - seconds;
            holdStartedAt = Time.unscaledTime;
            input.SetKey(Key.R, true);
            RewindsStarted++;
            lastHealth = -1;
            return true;
        }

        void HandleRewind(float now)
        {
            var clock = GameClock.Instance;
            if (clock == null)
                return;
            if (input.IsHeld(Key.R))
            {
                // Let go once far enough back, or if the game didn't start a rewind (too early, no juice).
                bool reached = clock.IsRewinding && now <= rewindTarget;
                bool refused = !clock.IsRewinding && Time.unscaledTime - holdStartedAt > 0.4f;
                if (reached || refused || Time.unscaledTime - holdStartedAt > 5f)
                {
                    input.SetKey(Key.R, false);
                    rewindTarget = -1f;
                }
                return;
            }
            if (clock.IsRewinding)
                return;

            int health = world.Player.Health;
            bool hit = lastHealth >= 0 && health < lastHealth;
            lastHealth = health;
            // A fatal hit is undone with whatever juice is left (a player would hold R on "undo your death"), any other with a full second.
            bool fatal = health <= 0;
            if (!hit || !useRewind || (now < 2f && !fatal))
                return;
            var budget = SickOfIt.Core.Rewind.RewindBudget.Instance;
            if (budget == null || budget.Juice < (fatal ? 0.4f : 1.2f))
                return;
            RewindNow(fatal ? Mathf.Clamp(budget.Juice - 0.1f, 0.3f, 2f) : 1.2f);
        }
    }
}

using System;
using UnityEngine;
using SickOfIt.Core.Input;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// The hero's two moves, the dash and the wall-jump (Docs/Story.md, "The hero's moves"; player.json <c>moves</c>). The controller
    /// does the moving; this is the world's side: which moves are his (Yooba's good, or the story moment that gives the wall-jump),
    /// the hero's lines, telemetry for the playtests, and the saved state a rewind puts back (the controller's own flags are not
    /// recorded, so the world keeps them with <see cref="SaveState"/>). An unlock is for good (<see cref="UnlockFlags"/>); a rewind
    /// that goes back before it never takes it away, like a found weapon.
    /// </summary>
    public sealed class HeroMoves : IDisposable
    {
        public const string DashFlag = "move:dash";
        public const string WallJumpFlag = "move:wallJump";

        readonly GameplayWorld world;
        readonly PlayerController controller;

        public HeroMoves(GameplayWorld world, PlayerController controller)
        {
            this.world = world;
            this.controller = controller;
            if (controller != null)
            {
                PlayerController.Dashed += OnDashed;
                PlayerController.WallClung += OnClung;
                PlayerController.WallJumped += OnKicked;
                PlayerController.MoveFirstUsed += OnFirstUse;
            }
        }

        public void Dispose()
        {
            PlayerController.Dashed -= OnDashed;
            PlayerController.WallClung -= OnClung;
            PlayerController.WallJumped -= OnKicked;
            PlayerController.MoveFirstUsed -= OnFirstUse;
        }

        public bool DashUnlocked => controller != null && controller.DashUnlocked;
        public bool WallJumpUnlocked => controller != null && controller.WallJumpUnlocked;

        /// <summary>
        /// The floor starts: works out which moves are his. The dash is Yooba's good (or a story condition, if player.json gives one);
        /// the wall-jump comes from the story (Floor 996 cleared). A move that just became his writes its flag and, if it has one,
        /// the hero says his line (not in a scene, not while input is locked).
        /// </summary>
        public void Arm(RogueliteManager roguelite)
        {
            if (controller == null)
                return;
            var moves = GameDefinition.Instance != null && GameDefinition.Instance.player != null ? GameDefinition.Instance.player.moves : null;
            if (moves == null)
                return;
            // The dash is his from the first second; Yooba's Slimed Lining only upgrades it (a second air dash, a shorter cooldown).
            bool upgraded = roguelite != null && roguelite.GetShopTotal(ShopEffect.Dash) > 0f;
            bool dash = Unlock(DashFlag, moves.dash.unlockFlag, moves.dash.unlockWhen, upgraded || moves.dash.startUnlocked, null);
            controller.DashUpgraded = upgraded;
            bool wall = Unlock(WallJumpFlag, moves.wallJump.unlockFlag, moves.wallJump.unlockWhen, false, moves.wallJump.unlockLine);
            controller.SetMovesUnlocked(dash, wall);
        }

        bool Unlock(string defaultFlag, string flag, System.Collections.Generic.List<string> when, bool bought, string line)
        {
            if (string.IsNullOrEmpty(flag))
                flag = defaultFlag;
            if (UnlockFlags.Has(flag))
                return true;
            bool story = when != null && when.Count > 0 && Requirements.Met(when);
            if (!bought && !story)
                return false;
            UnlockFlags.Set(flag);
            world.Telemetry.Record("move", "unlocked", 0, flag);
            if (story && !string.IsNullOrEmpty(line) && string.IsNullOrEmpty(world.SceneId))
                Say(line, flag + ":unlock");
            return true;
        }

        void OnDashed(PlayerController who)
        {
            if (who == controller)
                world.Telemetry.Record("move", "dash", Mathf.RoundToInt(who.DashDistance * 100f));
        }

        void OnClung(PlayerController who, int side)
        {
            if (who == controller)
                world.Telemetry.Record("move", "wallCling", side);
        }

        void OnKicked(PlayerController who, int side)
        {
            if (who == controller)
                world.Telemetry.Record("move", "wallJump", side);
        }

        void OnFirstUse(PlayerController who, string move)
        {
            if (who != controller || !string.IsNullOrEmpty(world.SceneId))
                return;
            var moves = GameDefinition.Instance.player.moves;
            string line = move == "dash" ? moves.dash.firstLine : moves.wallJump.firstLine;
            string key = "moveSeen:" + move;
            if (string.IsNullOrEmpty(line) || UnlockFlags.Has(key))
                return;
            UnlockFlags.Set(key);
            Say(line, key);
        }

        // A hero's line: its prompt is the subtitle (Sfx.SpokenText), its clip the voice, shown once per save by the caller.
        void Say(string soundId, string why)
        {
            if (world.RewindLocked || world.Now < world.InputLockedUntil)
                return;
            string text = SickOfIt.Core.Audio.Sfx.SpokenText(soundId);
            if (string.IsNullOrEmpty(text))
                return;
            bool voiced = SickOfIt.Core.Audio.Sfx.Has(soundId);
            float shown = voiced ? Mathf.Max(3f, SickOfIt.Core.Audio.Sfx.LengthOf(soundId) + 0.4f) : 3f;
            world.Hud.ShowText(text, SickOfIt.Gameplay.Content.TextStyle.Subtitle, Color.white, world.Now + shown);
            if (voiced)
                SickOfIt.Core.Audio.Sfx.Play(soundId);
            world.Telemetry.Record("move", "line", 0, why);
        }

        // ─── Rewind ──────────────────────────────────────────────────

        public void SaveState(StateWriter w)
        {
            if (controller == null)
                return;
            var s = controller.GetMoveState();
            w.Float(s.dashEnd);
            w.Float(s.dashInvulnUntil);
            w.Float(s.dashReadyAt);
            w.Float(s.dashDir);
            w.Float(s.wallCoyoteUntil);
            w.Float(s.kickUntil);
            w.Float(s.kickDir);
            w.Float(s.hitPushUntil);
            w.Float(s.hitPushX);
            w.Float(s.hitPushY);
            w.Int(s.airDashesLeft);
            w.Int(s.wallSide);
            w.Int(s.lastWallSide);
        }

        public void LoadState(StateReader r)
        {
            if (controller == null)
                return;
            controller.SetMoveState(new PlayerController.MoveState
            {
                dashEnd = r.Float(),
                dashInvulnUntil = r.Float(),
                dashReadyAt = r.Float(),
                dashDir = r.Float(),
                wallCoyoteUntil = r.Float(),
                kickUntil = r.Float(),
                kickDir = r.Float(),
                hitPushUntil = r.Float(),
                hitPushX = r.Float(),
                hitPushY = r.Float(),
                airDashesLeft = r.Int(),
                wallSide = r.Int(),
                lastWallSide = r.Int(),
            });
        }
    }
}

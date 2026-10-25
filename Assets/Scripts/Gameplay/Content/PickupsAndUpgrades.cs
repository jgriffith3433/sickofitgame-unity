using System;
using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Json;

namespace SickOfIt.Gameplay.Content
{
    [Serializable, JsonType("collect")]
    [Doc("Makes it a pickup: drifts to the player when close, and when touched runs its actions (self = the pickup) and disappears. " +
         "Each pickup pays out once, even if a rewind brings it back.")]
    public sealed class CollectAbility : Ability
    {
        [Doc("Starts drifting toward the player inside this distance (0 = never).")] public float magnetRadius = 1.6f;
        public float magnetSpeed = 6f;
        [Doc("Collected inside this distance (added to the player's size).")] public float pickupRadius = 0.25f;
        [Doc("Bob up and down while waiting.")] public float bobHeight = 0.06f;
        public List<GameAction> @do = new List<GameAction>();
        public AudioClip sound;
        [SoundRef, Doc("audio.json sound to play when collected (used when 'sound' isn't set).")] public string soundId = "";

        public override void Tick(float now, float dt)
        {
            var player = Player;
            if (player == null || !player.IsAlive)
                return;
            Vector2 to = player.Position - Owner.Position;
            float distance = to.magnitude;
            if (distance <= pickupRadius + player.Radius)
            {
                Collect();
                return;
            }
            if (magnetRadius > 0f && distance <= magnetRadius + Owner.World.PickupMagnetBonus)
                Owner.Move(to / distance * magnetSpeed);
            else if (bobHeight > 0f)
                Owner.Move(Vector2.up * Mathf.Cos((now - Owner.SpawnTime) * 4f) * bobHeight * 4f);
        }

        public override void OnContact(Entity other, bool entering)
        {
            if (other.IsPlayer)
                Collect();
        }

        void Collect()
        {
            if (!Owner.IsAlive)
                return;
            // Paid out once per pickup: a rewind can bring the pickup back, but not the reward.
            if (Owner.World.TryGrantOnce("collect:" + Owner.Serial))
            {
                Owner.RunActions(@do, 0);
                if (sound != null)
                    AudioSource.PlayClipAtPoint(sound, Owner.Position);
                else
                    SickOfIt.Core.Audio.Sfx.Play(soundId);
                SickOfIt.Core.Fx.Fx.Flash(Owner.Position, new Color(1f, 1f, 1f, 0.8f), 0.7f, 0.15f);
                Owner.World.Telemetry.Record("pickup", Owner.DefId, Owner.Serial);
            }
            Owner.Die(DeathCause.Despawn);
        }
    }

    [Serializable, JsonType("addDiamonds")]
    [Doc("Gives the player diamonds (the shop currency). Use it in a pickup's collect actions, which pay out once even if rewound.")]
    public sealed class AddDiamondsAction : GameAction
    {
        public int amount = 1;

        public override void Run(ActionContext context)
        {
            // Fat Diamonds: a pickup can be worth more (keyed on the pickup, so a replay agrees).
            int extra = context.Self != null && context.World != null && context.World.Perks != null ? context.World.Perks.DiamondExtra(context.Self.Serial) : 0;
            SickOfIt.Data.RogueliteManager.Instance?.AddPoints(amount + extra);
        }
    }

    [Serializable, JsonType("addRewind")]
    [Doc("Adds seconds of rewind juice (up to the maximum) and shows it.")]
    public sealed class AddRewindAction : GameAction
    {
        public float seconds = 1f;
        [Doc("Text floated at the pickup ('' = none). {0} = the seconds.")] public string text = "+{0}s REWIND";

        public override void Run(ActionContext context)
        {
            var budget = SickOfIt.Core.Rewind.RewindBudget.Instance;
            budget?.Add(seconds);
            if (!string.IsNullOrEmpty(text))
                context.World.Effects.PopText(context.SelfPosition, string.Format(text, seconds.ToString("0.#")), new Color(0.4f, 0.95f, 1f));
        }
    }

    [Serializable, JsonType("setFlag")]
    [Doc("Writes an unlock flag for good (saved; kept through runs, cleared by New Game). Yooba's shop and Grudges read them with 'flag:<id>' in 'requires'. " +
         "Use it in a pickup's collect actions (the lost things of Yooba's quest), which pay out once even if rewound.")]
    public sealed class SetFlagAction : GameAction
    {
        [Required, Doc("Letters, digits, dots and dashes (no ';').")] public string flag = "";

        public override void Run(ActionContext context) => SickOfIt.Data.UnlockFlags.Set(flag);
    }

    [Serializable, JsonType("popText")]
    [Doc("Floats a short text up from a point (damage numbers, jokes, \"NICE\").")]
    public sealed class PopTextAction : GameAction
    {
        public string text = "";
        public Color color = Color.white;
        public Vector2 at;
        public Anchor anchor = Anchor.Self;
        public float size = 1f;

        public override void Run(ActionContext context) => context.World.Effects.PopText(context.Resolve(at, anchor), text, color, size);
    }

    [Serializable, JsonType("offerUpgrades")]
    [Doc("Pauses the game and lets the player pick one of 'count' upgrades. Offered once per level, even across rewinds. " +
         "Picks from 'choices' if set, otherwise from every upgrade not yet maxed (weighted).")]
    public sealed class OfferUpgradesAction : GameAction
    {
        public int count = 3;
        [UpgradeRef] public List<string> choices = new List<string>();
        public string title = "Pick an upgrade";

        public override void Run(ActionContext context) => context.World.Upgrades.Offer(this, context);
    }

    [Serializable, JsonType("grantUpgrade")]
    [Doc("Gives the player one level of an upgrade (once per level, even across rewinds).")]
    public sealed class GrantUpgradeAction : GameAction
    {
        [Required, UpgradeRef] public string upgrade = "";

        public override void Run(ActionContext context) => context.World.Upgrades.Grant(upgrade, "grant:" + context.Director?.IndexOfAction(this));
    }
}

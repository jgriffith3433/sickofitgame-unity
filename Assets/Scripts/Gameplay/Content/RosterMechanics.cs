using System;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Json;

namespace SickOfIt.Gameplay.Content
{
    // =====================================================================
    // Three abilities for the Cracked Road's bestiary (Docs/Story.md "The Cracked Road's bestiary"): an enemy that gets up
    // once, two enemies that share one life, and one that only takes damage right after the hero has rewound.
    // Entity.Damage asks each of them; all of their state is saved for rewind.
    // =====================================================================

    [Serializable, JsonType("revive")]
    [Doc("Gets up again after a killing hit: it goes down for 'downSeconds' (stunned and untouchable, tinted by the 'effect'), then rises with 'healthPercent' of its health back. 'times' says how often; a death by 'kill' or a despawn is final.")]
    public sealed class ReviveAbility : Ability
    {
        [Doc("How many times it can get up.")] public int times = 1;
        [Doc("Share of its full health it rises with, in percent.")] public int healthPercent = 60;
        [Doc("Seconds it lies down.")] public float downSeconds = 2.2f;
        [EffectRef, Doc("The status effect that holds it down (stun + invulnerable + a tint): content/effects/downed.json.")] public string effect = "downed";
        [SoundRef, Doc("Sound id (audio.json) played when it rises; empty = none.")] public string riseSound = "";
        public Color riseColor = new Color(1f, 0.7f, 0.3f, 1f);

        int used;
        float riseAt;

        public int Used => used;
        public bool Down => riseAt > 0f;

        public override void OnSpawn()
        {
            used = 0;
            riseAt = 0f;
        }

        /// <summary>Called by Entity.Damage on a killing hit. True if it will get up (the entity is kept alive).</summary>
        public bool TryRevive()
        {
            if (used >= times)
                return false;
            used++;
            int full = Owner.MaxHealth;
            Owner.SetHealth(Mathf.Max(1, Mathf.RoundToInt(full * Mathf.Clamp(healthPercent, 1, 100) / 100f)));
            riseAt = Owner.Now + downSeconds;
            if (!string.IsNullOrEmpty(effect))
                Owner.ApplyStatus(effect, null);
            SickOfIt.Core.Fx.Fx.Burst(Owner.Position, Owner.FxColor, 10, 2.5f, 0.12f);
            Owner.World.Telemetry?.Record("revive", Owner.DefId, used, $"down {downSeconds:0.0} s");
            Owner.RecordNow();
            return true;
        }

        public override void Tick(float now, float dt)
        {
            if (riseAt <= 0f || now < riseAt)
                return;
            riseAt = 0f;
            Owner.FlashFor(0.3f, riseColor);
            SickOfIt.Core.Fx.Fx.Flash(Owner.Position, riseColor, Mathf.Clamp(Owner.Radius * 3f, 0.5f, 2f), 0.2f);
            if (!string.IsNullOrEmpty(riseSound))
                SickOfIt.Core.Audio.Sfx.Play(riseSound);
            Owner.World.Telemetry?.Record("rise", Owner.DefId, used, $"health {Owner.Health}");
            Owner.RecordNow();
        }

        public override void SaveState(StateWriter writer)
        {
            writer.Int(used);
            writer.Float(riseAt);
        }

        public override void LoadState(StateReader reader)
        {
            used = reader.Int();
            riseAt = reader.Float();
        }
    }

    [Serializable, JsonType("chainLink")]
    [Doc("Chained to another entity (the 'partner' type): damage to either is dealt to both, and when one dies the other dies with it. They find each other by themselves (the nearest free one of that type) and a bar is drawn along the chain.")]
    public sealed class ChainLinkAbility : Ability
    {
        [Required, EntityRef, Doc("Entity id of the other end of the chain.")] public string partner = "";
        [Doc("Share of each hit the other end takes, 0 to 1.")] public float share = 1f;
        public Color color = new Color(0.75f, 0.7f, 0.65f, 0.9f);
        public float thickness = 0.07f;
        [SoundRef, Doc("Sound id (audio.json) played when a hit travels down the chain; empty = none.")] public string linkSound = "";

        static bool sharing;

        int partnerSerial;
        float sharedAt = -10f;
        MechanicView view;

        public int PartnerSerial => partnerSerial;

        public override void OnSpawn()
        {
            partnerSerial = 0;
            sharedAt = -10f;
        }

        /// <summary>The other end, or null if it is not bound, gone or dead.</summary>
        public Entity Partner()
        {
            if (partnerSerial == 0)
                return null;
            var other = Owner.World.FindBySerial(partnerSerial);
            return other != null && other.IsAlive && other.DefId == partner ? other : null;
        }

        void TryBind()
        {
            foreach (var e in Owner.World.Live)
            {
                if (e == null || e == Owner || !e.IsAlive || e.DefId != partner)
                    continue;
                var link = e.FindAbility<ChainLinkAbility>();
                if (link == null || link.partnerSerial != 0 || link.partner != Owner.DefId)
                    continue;
                partnerSerial = e.Serial;
                link.partnerSerial = Owner.Serial;
                Owner.RecordNow();
                e.RecordNow();
                Owner.World.Telemetry?.Record("chainBound", Owner.DefId, e.Serial, partner);
                return;
            }
        }

        /// <summary>Called by Entity.Damage after this end took 'amount'; 'died' is true if this end is dead for good.</summary>
        public void Share(int amount, bool died)
        {
            if (sharing)
                return;
            var other = Partner();
            if (other == null)
                return;
            sharing = true;
            try
            {
                int mirrored = Mathf.RoundToInt(amount * Mathf.Clamp01(share));
                if (mirrored > 0)
                    other.Damage(mirrored, null, true);
                if (died && other.IsAlive)
                    other.Die(DeathCause.Damage);
            }
            finally
            {
                sharing = false;
            }
            sharedAt = Owner.Now;
            if (!string.IsNullOrEmpty(linkSound))
                SickOfIt.Core.Audio.Sfx.Play(linkSound);
            Owner.World.Telemetry?.Record("chainShare", Owner.DefId, amount, died ? "both die" : "both hurt");
            Owner.RecordNow();
        }

        public override void Tick(float now, float dt)
        {
            if (view == null)
            {
                view = MechanicView.Create(Owner, "Chain", -1);
                view.Draw = Draw;
            }
            if (partnerSerial == 0)
                TryBind();
        }

        void Draw()
        {
            if (view == null)
                return;
            if (Owner == null || !Owner.IsAlive)
            {
                view.Hide();
                return;
            }
            var other = Partner();
            // One end draws the chain: the one spawned first.
            if (other == null || other.Serial < Owner.Serial)
            {
                view.Hide();
                return;
            }
            Vector2 a = Owner.Position, b = other.Position;
            Vector2 d = b - a;
            float flash = Mathf.Clamp01(1f - (Owner.Now - sharedAt) / 0.25f);
            view.PlaceBar(a, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, d.magnitude, thickness + 0.05f * flash, Color.Lerp(color, Color.white, flash));
        }

        public override void SaveState(StateWriter writer)
        {
            writer.Int(partnerSerial);
            writer.Float(sharedAt);
        }

        public override void LoadState(StateReader reader)
        {
            partnerSerial = reader.Int();
            sharedAt = reader.Float();
        }
    }

    [Serializable, JsonType("rewindSnag")]
    [Doc("Slips out of the way of every shot, until the hero rewinds: when a rewind ends it is snagged in the loop and takes damage for 'window' seconds (a bar above it shows when). Its answer is a short hold of R, then shooting.")]
    public sealed class RewindSnagAbility : Ability
    {
        [Doc("Seconds it can be hurt after a rewind ends.")] public float window = 4f;
        public Color openColor = new Color(0.5f, 1f, 1f, 1f);
        public Color shutColor = new Color(0.55f, 0.5f, 0.7f, 0.7f);
        [SoundRef, Doc("Sound id (audio.json) played when a shot passes through it; empty = none.")] public string deflectSound = "";
        [Doc("Draw the bar above it.")] public bool show = true;

        int seenRewinds;
        float openUntil;
        float deflectedAt = -10f;
        int deflects;
        MechanicView view;

        public int Deflects => deflects;

        public override void OnSpawn()
        {
            seenRewinds = GameClock.RewindsEnded;
            openUntil = 0f;
            deflectedAt = -10f;
            deflects = 0;
        }

        /// <summary>Is it snagged right now? A rewind that ended since the state was saved opens the window.</summary>
        public bool IsOpen
        {
            get
            {
                int ended = GameClock.RewindsEnded;
                if (ended != seenRewinds)
                {
                    seenRewinds = ended;
                    openUntil = Owner.Now + window;
                    Owner.World.Telemetry?.Record("snag", Owner.DefId, ended, $"open {window:0.0} s");
                    Owner.RecordNow();
                }
                return Owner.Now < openUntil;
            }
        }

        /// <summary>Called by Entity.Damage. True if the hit passes through it (the shot is spent).</summary>
        public bool TryDeflect()
        {
            if (IsOpen)
                return false;
            deflectedAt = Owner.Now;
            deflects++;
            SickOfIt.Core.Fx.Fx.Spark(Owner.Position, shutColor, 0.3f);
            if (!string.IsNullOrEmpty(deflectSound))
                SickOfIt.Core.Audio.Sfx.Play(deflectSound);
            Owner.World.Telemetry?.Record("snagDeflect", Owner.DefId, deflects, "shot passed through");
            Owner.RecordNow();
            return true;
        }

        public override void Tick(float now, float dt)
        {
            bool open = IsOpen;
            if (open)
                Owner.FlashFor(0.1f, openColor);
            if (show && view == null)
            {
                view = MechanicView.Create(Owner, "SnagBar", 2);
                view.Draw = Draw;
            }
        }

        void Draw()
        {
            if (view == null)
                return;
            if (Owner == null || !Owner.IsAlive || !show)
            {
                view.Hide();
                return;
            }
            bool open = Owner.Now < openUntil;
            float left = open ? Mathf.Clamp01((openUntil - Owner.Now) / Mathf.Max(0.01f, window)) : 1f;
            float flash = Mathf.Clamp01(1f - (Owner.Now - deflectedAt) / 0.2f);
            var c = open ? openColor : Color.Lerp(shutColor, Color.white, flash);
            float width = Owner.Radius * 2f * left;
            Vector2 start = Owner.Position + new Vector2(-Owner.Radius * left, Owner.Radius + 0.18f);
            view.PlaceBar(start, 0f, width, 0.07f, c);
        }

        public override void SaveState(StateWriter writer)
        {
            writer.Int(seenRewinds);
            writer.Float(openUntil);
            writer.Float(deflectedAt);
            writer.Int(deflects);
        }

        public override void LoadState(StateReader reader)
        {
            seenRewinds = reader.Int();
            openUntil = reader.Float();
            deflectedAt = reader.Float();
            deflects = reader.Int();
        }
    }
}

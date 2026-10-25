using System;
using UnityEngine;
using SickOfIt.Combat.Health;
using SickOfIt.Core.Input;
using SickOfIt.Core.Rewind;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// The hero's small perks from Yooba's goods and the Grudges (Docs/Story.md, "Yooba's new goods"): armour plates, bubbles,
    /// slips, crits, heal on kill, bonus diamonds, slower enemy shots, clean-kill bonus, cheaper rewinds, bombs, afterglow,
    /// quicker shots, a floor-start heal and a last gasp. Totals are constant for a floor (shop goods and held Grudges are added
    /// when it starts, a Grudge picked mid-floor adds itself), so a rewind agrees with them. What is spent (bubbles, bombs, gasps,
    /// plates and their regrowth) is saved state. Every chance is a hash of the seed, the moment and a salt, never a random
    /// stream, so a replay after a rewind rolls the same; payouts are keyed through <see cref="GameplayWorld.TryGrantOnce"/>.
    /// </summary>
    public sealed class HeroPerks
    {
        public const float PlateRegrowSeconds = 20f;
        public const float BlockedSeconds = 0.5f;
        public const float BombBaseRadius = 2.6f;
        public const int BombDamage = 3;

        readonly GameplayWorld world;
        readonly PlayerController controller;
        readonly PlayerHealth health;
        readonly float[] totals = new float[Enum.GetValues(typeof(ShopEffect)).Length];
        bool armed;

        // Saved state.
        int shield, bombs, gasp, platesMissing;
        float plateRegrowAt;

        public HeroPerks(GameplayWorld world, PlayerController controller, PlayerHealth health)
        {
            this.world = world;
            this.controller = controller;
            this.health = health;
        }

        public static bool IsPerk(ShopEffect effect) => effect >= ShopEffect.Armor;

        public float Total(ShopEffect effect) => totals[(int)effect];
        public int Shield => shield;
        public int Bombs => bombs;
        public int Gasp => gasp;
        public int Plates => Mathf.Max(0, Mathf.RoundToInt(totals[(int)ShopEffect.Armor]) - platesMissing);
        /// <summary>Enemy shots fly at this fraction of their speed (Sticky Air).</summary>
        public float EnemyShotSpeed => Mathf.Clamp(1f - totals[(int)ShopEffect.SlowShots], 0.5f, 1f);

        /// <summary>Adds one effect's worth. Before <see cref="Arm"/> it only counts; after, a charge-type effect also fills its charges.</summary>
        public void Add(ShopEffect effect, float amount)
        {
            if (!IsPerk(effect) || amount == 0f)
                return;
            totals[(int)effect] += amount;
            int whole = Mathf.RoundToInt(amount);
            switch (effect)
            {
                case ShopEffect.ShieldCharges: if (armed) shield += whole; break;
                case ShopEffect.BombCharges: if (armed) bombs += whole; break;
                case ShopEffect.LastGasp: if (armed) gasp += whole; break;
                case ShopEffect.Afterglow: health?.AddInvincibilityBonus(amount); break;
                case ShopEffect.RewindDiscount:
                    if (RewindBudget.Instance != null)
                        RewindBudget.Instance.CostScale = Mathf.Clamp(1f - totals[(int)effect], 0.4f, 1f);
                    break;
                case ShopEffect.ShotSpeed:
                    if (controller != null)
                        controller.ShotSpeedScale = 1f + totals[(int)effect];
                    break;
            }
        }

        /// <summary>The shop goods he owns, added once when the floor starts.</summary>
        public void AddShop(RogueliteManager roguelite)
        {
            foreach (var good in RogueliteManager.ShopCatalog)
            {
                if (good == null || !IsPerk(good.Effect))
                    continue;
                int level = roguelite.GetUpgradeLevel(good.id);
                if (level > 0)
                    Add(good.Effect, level * good.amountPerLevel);
            }
        }

        /// <summary>The floor starts: every charge is full. Called after the goods and the held Grudges are added.</summary>
        public void Arm()
        {
            shield = Mathf.RoundToInt(totals[(int)ShopEffect.ShieldCharges]);
            bombs = Mathf.RoundToInt(totals[(int)ShopEffect.BombCharges]);
            gasp = Mathf.RoundToInt(totals[(int)ShopEffect.LastGasp]);
            platesMissing = 0;
            armed = true;
            int heal = Mathf.RoundToInt(totals[(int)ShopEffect.FloorHeal]);
            if (heal > 0 && health != null && health.IsAlive && health.CurrentHealth < health.MaxHealth)
            {
                health.SetHealth(Mathf.Min(health.MaxHealth, health.CurrentHealth + heal));
                world.Telemetry.Record("perk", "floorHeal", health.CurrentHealth);
            }
        }

        public void Tick(float now, float dt)
        {
            if (platesMissing > 0 && now >= plateRegrowAt)
            {
                platesMissing--;
                plateRegrowAt = now + PlateRegrowSeconds;
                world.Telemetry.Record("perk", "plateRegrown", Plates);
            }
        }

        // ─── Rolls ─────────────────────────────────────────────────

        float Roll(int moment, int salt)
        {
            unchecked
            {
                uint h = world.Seed ^ (uint)moment * 2654435761u ^ (uint)salt * 40503u;
                h ^= h >> 16;
                h *= 0x7feb352du;
                h ^= h >> 15;
                h *= 0x846ca68bu;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        int Moment => Mathf.RoundToInt(world.Now * 100f);

        // ─── The hero is hit ──────────────────────────────────────

        /// <summary>
        /// A hit is about to land for <paramref name="amount"/>. Returns false when a bubble, a plate or a slip took it whole; a last gasp
        /// can lower the amount; a bomb goes off for a hit that lands.
        /// </summary>
        public bool FilterHit(ref int amount)
        {
            if (!armed || world.Player == null || health == null)
                return true;
            if (shield > 0)
            {
                shield--;
                Blocked("bubble", shield);
                return false;
            }
            if (Plates > 0)
            {
                if (platesMissing == 0)
                    plateRegrowAt = world.Now + PlateRegrowSeconds;
                platesMissing++;
                Blocked("plate", Plates);
                return false;
            }
            float dodge = Mathf.Clamp(totals[(int)ShopEffect.Dodge], 0f, 0.6f);
            if (dodge > 0f && Roll(Moment, 3) < dodge)
            {
                Blocked("slip", 0);
                return false;
            }
            if (gasp > 0 && amount >= health.CurrentHealth)
            {
                gasp--;
                if (health.CurrentHealth <= 1)
                {
                    Blocked("gasp", gasp);
                    return false;
                }
                amount = health.CurrentHealth - 1;
                world.Telemetry.Record("perk", "gasp", gasp);
                world.Effects.PopText(world.Player.Position + Vector2.up * 0.7f, "LAST GASP!", new Color(1f, 0.55f, 0.55f), 1.1f);
            }
            if (bombs > 0)
            {
                bombs--;
                Bomb();
            }
            return true;
        }

        void Blocked(string kind, int left)
        {
            health.SetInvincibilityTimer(Mathf.Max(health.InvincibilityTimer, BlockedSeconds));
            Vector2 at = world.Player.Position;
            SickOfIt.Core.Fx.Fx.Flash(at, new Color(0.55f, 1f, 0.7f, 0.8f), 0.9f, 0.18f);
            world.Effects.PopText(at + Vector2.up * 0.7f, kind == "bubble" ? "POP!" : kind == "plate" ? "CLANK!" : kind == "slip" ? "SLIPPED!" : "NOT YET!", new Color(0.55f, 1f, 0.7f), 1.1f);
            world.Telemetry.Record("perk", kind, left);
        }

        void Bomb()
        {
            Vector2 at = world.Player.Position;
            float radius = BombBaseRadius + totals[(int)ShopEffect.BombRadius];
            int shots = 0, hurt = 0;
            world.ForEachHostileWithin(world.Player, at, radius, e =>
            {
                if (e.FindAbility<ProjectileAbility>() != null)
                {
                    e.Die(DeathCause.Despawn);
                    shots++;
                }
                else if (e.Damage(BombDamage, world.Player))
                    hurt++;
            });
            SickOfIt.Core.Fx.Fx.Flash(at, new Color(0.7f, 1f, 0.35f, 0.9f), radius * 1.4f, 0.3f);
            world.Effects.PopText(at + Vector2.up * 0.9f, "BELCH!", new Color(0.7f, 1f, 0.35f), 1.3f);
            world.Telemetry.Record("perk", "bomb", shots, hurt.ToString());
        }

        // ─── The hero shoots, kills, collects ──────────────────────

        /// <summary>The damage scale of the next volley: 1, or a crit.</summary>
        public float RollVolleyScale()
        {
            float chance = totals[(int)ShopEffect.CritChance];
            if (chance <= 0f || world.Player == null || Roll(Moment, 5) >= chance)
                return 1f;
            float scale = 2f + totals[(int)ShopEffect.CritDamage];
            world.Effects.PopText(world.Player.Position + Vector2.up * 0.9f, "CRIT!", new Color(1f, 0.85f, 0.3f), 1f);
            world.Telemetry.Record("perk", "crit", Mathf.RoundToInt(scale * 100f));
            return scale;
        }

        /// <summary>An enemy died to the hero's damage (not a shot): the burp may heal a heart.</summary>
        public void OnEnemyKilled(Entity enemy)
        {
            float chance = totals[(int)ShopEffect.HealOnKill];
            if (!armed || chance <= 0f || health == null || !health.IsAlive || health.CurrentHealth >= health.MaxHealth)
                return;
            if (!world.TryGrantOnce("perkHeal:" + enemy.Serial) || Roll(enemy.Serial, 11) >= chance)
                return;
            health.SetHealth(health.CurrentHealth + 1);
            world.Effects.PopText(world.Player.Position + Vector2.up * 0.8f, "+1 BURP", new Color(0.45f, 1f, 0.5f), 1f);
            world.Telemetry.Record("perk", "burpHeal", health.CurrentHealth);
        }

        /// <summary>Extra diamonds a pickup is worth (Fat Diamonds): the whole part sure, the rest a chance. Keyed on the pickup, so a replay agrees.</summary>
        public int DiamondExtra(int pickupSerial)
        {
            float value = totals[(int)ShopEffect.DiamondValue];
            if (value <= 0f)
                return 0;
            int extra = Mathf.FloorToInt(value);
            if (Roll(pickupSerial, 13) < value - extra)
                extra++;
            if (extra > 0)
                world.Telemetry.Record("perk", "fatDiamond", extra);
            return extra;
        }

        /// <summary>Seconds of Deja Vu added to a clean kill's refund.</summary>
        public float CleanKillBonus => totals[(int)ShopEffect.CleanKillBonus];

        // ─── Rewind ─────────────────────────────────────────────

        public void SaveState(StateWriter w)
        {
            w.Int(shield);
            w.Int(bombs);
            w.Int(gasp);
            w.Int(platesMissing);
            w.Float(plateRegrowAt);
        }

        public void LoadState(StateReader r)
        {
            shield = r.Int();
            bombs = r.Int();
            gasp = r.Int();
            platesMissing = r.Int();
            plateRegrowAt = r.Float();
        }
    }
}

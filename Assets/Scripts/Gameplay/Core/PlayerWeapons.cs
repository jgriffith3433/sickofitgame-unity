using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Core.Audio;
using SickOfIt.Core.Input;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// The hero's weapons (content/weapons): which he holds, which is equipped, each one's ammo, and the melee swing in
    /// progress. PlayerController asks it what the equipped weapon is and whether it can be used; the level's
    /// giveWeapon / takeWeapon actions and the weapon button change what he holds.
    ///
    /// Everything that decides what happens is saved with the world's state (SaveState / LoadState), so a rewind
    /// brings back the equipped weapon, the ammo and a swing's timers exactly. The rocket launcher is always in hand
    /// and fires the way player.json says; it is the first weapon and cannot be taken. What he holds past the launcher
    /// is kept between floors in <see cref="WeaponProgress"/> (cleared when a run ends).
    /// </summary>
    public sealed class PlayerWeapons : IPlayerArmory
    {
        public const string Launcher = "rocketLauncher";
        const float DryClickSeconds = 0.45f;

        readonly GameplayWorld world;
        readonly PlayerController controller;

        // ─── Saved state ──────────────────────────────────────────────
        readonly List<string> owned = new List<string>();
        readonly List<float> ammo = new List<float>();
        string current = Launcher;
        float usableAt;
        bool hitPending;
        float swingStart;
        float hitAt;
        float swingEnd;
        float swingDirX = 1f;
        float swingDirY;
        int swingStep;
        int comboNext;
        float comboUntil;
        float swingDamage = 1f;

        // ─── Not simulation (sound pacing, the saved-prefs mirror) ─────
        float dryAt;
        bool storeDirty;
        readonly List<string> storedIds = new List<string>();
        string storedCurrent = Launcher;
        static readonly Collider2D[] hitBuffer = new Collider2D[48];
        readonly List<Entity> struck = new List<Entity>();
        readonly List<KeyValuePair<string, int>> storeScratch = new List<KeyValuePair<string, int>>();

        public PlayerWeapons(GameplayWorld world, PlayerController controller)
        {
            this.world = world;
            this.controller = controller;
            Restore();
        }

        // ─── Reading ─────────────────────────────────────────────────

        public string CurrentId => current;
        public IReadOnlyList<string> Held => owned;
        public int HeldCount => owned.Count;
        public bool Has(string id) => owned.Contains(id);

        /// <summary>The equipped weapon's definition (null if the content has none).</summary>
        public WeaponDef CurrentDef => world.Content.Weapons.TryGetValue(current, out var def) ? def : null;

        /// <summary>Rounds left in the equipped weapon (0 for a weapon without ammo, see <see cref="CurrentUsesAmmo"/>).</summary>
        public int CurrentAmmo => AmmoOf(current);
        public bool CurrentUsesAmmo => CurrentDef != null && CurrentDef.ammo.capacity > 0;
        public int CurrentCapacity => CurrentDef != null ? CurrentDef.ammo.capacity : 0;

        /// <summary>A melee swing is on (from the button to the end of its recovery) at 'now'.</summary>
        public bool Swinging(float now) => now >= swingStart && now < swingEnd && swingEnd > swingStart;
        /// <summary>0 at the start of the swing to 1 at its end; for the visible swing.</summary>
        public float SwingProgress(float now) => swingEnd > swingStart ? Mathf.Clamp01((now - swingStart) / (swingEnd - swingStart)) : 1f;
        /// <summary>0 to 1 over the windup, the part of the swing before the hit lands.</summary>
        public float SwingWindupProgress(float now) => hitAt > swingStart ? Mathf.Clamp01((now - swingStart) / (hitAt - swingStart)) : 1f;
        public Vector2 SwingDirection => new Vector2(swingDirX, swingDirY);
        public int SwingStep => swingStep;
        public MeleeStepDef SwingStepDef
        {
            get
            {
                var def = world.Content.Weapons.TryGetValue(current, out var d) ? d : null;
                return def != null && def.kind == WeaponKind.Melee && swingStep >= 0 && swingStep < def.melee.steps.Count ? def.melee.steps[swingStep] : null;
            }
        }
        /// <summary>Where the hit box starts: the hero's upper body.</summary>
        public Vector2 SwingOrigin => world.PlayerAimPoint;

        int AmmoOf(string id)
        {
            int i = owned.IndexOf(id);
            return i >= 0 ? Mathf.FloorToInt(ammo[i] + 0.0001f) : 0;
        }

        // ─── IPlayerArmory ───────────────────────────────────────────

        public bool TryGetCurrent(out WeaponStats stats)
        {
            stats = default;
            if (!world.Content.Weapons.TryGetValue(current, out var def))
                return false;
            stats = new WeaponStats
            {
                id = def.id,
                displayName = def.displayName,
                melee = def.kind == WeaponKind.Melee,
                cooldown = def.cooldown,
                projectileSpeed = def.projectile.speed,
                projectileLifetime = def.projectile.lifetime,
                projectileFaceAim = def.projectile.faceAim,
                projectileSpriteAngle = def.projectile.spriteAngle,
                damageScale = def.projectile.damageScale,
                multiShot = def.projectile.multiShot,
                useSound = def.sounds.use,
                aimGuide = def.aimGuide.show,
                aimGuideLength = def.aimGuide.length,
            };
            return true;
        }

        public bool Empty => CurrentUsesAmmo && AmmoOf(current) < Mathf.Max(1, CurrentDef.ammo.perShot);

        public bool CanUse(float now)
        {
            if (now < usableAt || hitPending)
                return false;
            if (Empty)
                return false;
            // A rewind can leave a swing's end ahead of the clock; it only blocks while the clock is inside it.
            return !(now >= swingStart && now < swingEnd);
        }

        public void Dry(float now)
        {
            if (dryAt > now + DryClickSeconds * 2f)
                dryAt = now;
            if (now < dryAt)
                return;
            dryAt = now + DryClickSeconds;
            var def = CurrentDef;
            if (def != null && !string.IsNullOrEmpty(def.sounds.empty))
                Sfx.Play(def.sounds.empty);
        }

        public void Fired(float now)
        {
            SpendAmmo();
            world.RecordNow();
        }

        public bool Swing(float now, Vector2 origin, Vector2 direction, float attackSpeed, float damage)
        {
            var def = CurrentDef;
            if (def == null || def.kind != WeaponKind.Melee || def.melee.steps.Count == 0 || !CanUse(now))
                return false;
            var steps = def.melee.steps;
            int step = comboNext > 0 && comboNext < steps.Count && now <= comboUntil ? comboNext : 0;
            var s = steps[step];
            float speed = Mathf.Max(0.1f, attackSpeed);
            Vector2 d = direction.sqrMagnitude > 0.0001f ? direction.normalized : new Vector2(controller.FacingRight ? 1f : -1f, 0f);

            swingStart = now;
            hitAt = now + Mathf.Max(0.02f, s.windup) / speed;
            swingEnd = hitAt + Mathf.Max(0f, s.recovery) / speed;
            swingDirX = d.x;
            swingDirY = d.y;
            swingStep = step;
            swingDamage = Mathf.Max(0f, damage);
            hitPending = true;
            comboNext = step + 1;
            comboUntil = swingEnd + def.melee.comboWindow;
            usableAt = swingEnd;
            SpendAmmo();

            if (!string.IsNullOrEmpty(def.sounds.use))
                Sfx.Play(def.sounds.use);
            world.Telemetry.Record("swing", current, step, s.name);
            world.RecordNow();
            return true;
        }

        public bool Cycle(int step, float now)
        {
            if (owned.Count < 2 || hitPending)
                return false;
            int index = Mathf.Max(0, owned.IndexOf(current));
            int next = ((index + step) % owned.Count + owned.Count) % owned.Count;
            if (owned[next] == current)
                return false;
            Equip(owned[next], now);
            return true;
        }

        // ─── Giving and taking ───────────────────────────────────────

        /// <summary>Puts a weapon in his hands (giveWeapon). Ammo weapons come with 'rounds' (0 = a full load); giving one he holds adds ammo.</summary>
        public void Give(string id, bool equip, int rounds, bool announce)
        {
            if (!world.Content.Weapons.TryGetValue(id, out var def))
                return;
            float now = world.Now;
            int index = owned.IndexOf(id);
            int capacity = def.ammo.capacity;
            if (index < 0)
            {
                owned.Add(id);
                if (id != Launcher)
                    UnlockFlags.Set("weapon:" + id); // unlocks his weapon goods in Yooba's shop for good
                ammo.Add(capacity > 0 ? (rounds > 0 ? Mathf.Min(capacity, rounds) : def.ammo.start > 0 ? Mathf.Min(capacity, def.ammo.start) : capacity) : 0f);
            }
            else if (capacity > 0)
            {
                ammo[index] = Mathf.Min(capacity, ammo[index] + (rounds > 0 ? rounds : capacity));
            }
            world.Telemetry.Record("weaponGiven", id, owned.Count, equip ? "equip" : "");
            if (announce && world.Player != null)
                world.Effects.PopText(world.Player.Position + Vector2.up * 0.6f, def.displayName.ToUpperInvariant() + "!", def.color, 1.2f);
            if (equip)
                Equip(id, now);
            else
                Store(true);
            world.RecordNow();
        }

        /// <summary>Takes a weapon away (takeWeapon). The launcher stays.</summary>
        public void Take(string id, bool announce)
        {
            int index = owned.IndexOf(id);
            if (id == Launcher || index < 0)
                return;
            owned.RemoveAt(index);
            ammo.RemoveAt(index);
            world.Telemetry.Record("weaponTaken", id, owned.Count);
            if (announce && world.Player != null && world.Content.Weapons.TryGetValue(id, out var def))
                world.Effects.PopText(world.Player.Position + Vector2.up * 0.6f, def.displayName.ToUpperInvariant() + " GONE", def.color, 1.0f);
            if (current == id)
            {
                // A swing he was in the middle of goes with the weapon.
                hitPending = false;
                comboNext = 0;
                swingEnd = Mathf.Min(swingEnd, world.Now);
                Equip(Launcher, world.Now);
            }
            else
            {
                Store(true);
            }
            world.RecordNow();
        }

        void Equip(string id, float now)
        {
            current = id;
            comboNext = 0;
            var def = CurrentDef;
            usableAt = Mathf.Max(usableAt, now + (def != null ? def.switchDelay : 0f));
            if (def != null && !string.IsNullOrEmpty(def.sounds.equip))
                Sfx.Play(def.sounds.equip);
            world.Telemetry.Record("weapon", id, CurrentAmmo, def != null ? def.displayName : id);
            Store(true);
            world.RecordNow();
        }

        void SpendAmmo()
        {
            var def = CurrentDef;
            if (def == null || def.ammo.capacity <= 0)
                return;
            int index = owned.IndexOf(current);
            if (index < 0)
                return;
            ammo[index] = Mathf.Max(0f, ammo[index] - Mathf.Max(1, def.ammo.perShot));
            Store(false);
        }

        // ─── Each simulation tick ────────────────────────────────────

        public void Tick(float now, float dt)
        {
            for (int i = 0; i < owned.Count; i++)
            {
                if (!world.Content.Weapons.TryGetValue(owned[i], out var def) || def.ammo.capacity <= 0 || def.ammo.regenPerSecond <= 0f)
                    continue;
                ammo[i] = Mathf.Min(def.ammo.capacity, ammo[i] + def.ammo.regenPerSecond * dt);
            }
            if (hitPending && now >= hitAt)
            {
                hitPending = false;
                Strike(now);
                world.RecordNow();
            }
            if (storeDirty)
                Store(true);
        }

        /// <summary>The swing lands: every living enemy in the hit box in front of him takes the step's damage.</summary>
        void Strike(float now)
        {
            var def = CurrentDef;
            var player = world.Player;
            var s = SwingStepDef;
            if (def == null || s == null || player == null || !player.IsAlive)
                return;
            Vector2 dir = SwingDirection;
            Vector2 from = SwingOrigin;
            var shop = RogueliteManager.Instance;
            float reach = Mathf.Max(0.1f, s.reach * (1f + (shop != null ? shop.GetShopTotal(ShopEffect.MeleeReach) : 0f)));
            Vector2 center = from + dir * (reach * 0.5f);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            var filter = new ContactFilter2D { useTriggers = true };
            int n = Physics2D.OverlapBox(center, new Vector2(reach, Mathf.Max(0.2f, def.melee.height)), angle, filter, hitBuffer);
            struck.Clear();
            int damage = Mathf.Max(1, Mathf.RoundToInt(s.damage * swingDamage * (1f + (shop != null ? shop.GetShopTotal(ShopEffect.MeleeDamage) : 0f))));
            for (int i = 0; i < n; i++)
            {
                if (!world.TryGetEntity(hitBuffer[i], out var entity) || entity == player || struck.Contains(entity))
                    continue;
                if (!entity.IsAlive || !player.IsHostileTo(entity))
                    continue;
                struck.Add(entity);
                entity.TakeDamageFrom(damage, from);
                if (s.knockback > 0f)
                    entity.Knockback(dir * s.knockback);
                SickOfIt.Core.Fx.Fx.Spark(entity.Position, def.color, Mathf.Clamp(0.35f + 0.15f * s.size, 0.3f, 0.9f));
            }
            world.Telemetry.Record("weaponHit", current, struck.Count, s.name);
            if (struck.Count > 0 && !string.IsNullOrEmpty(def.sounds.hit))
                Sfx.Play(def.sounds.hit);
            if (struck.Count > 0)
                world.Effects.Shake(0.04f + 0.02f * s.size, 0.12f);
        }

        // ─── Kept between floors ─────────────────────────────────────

        void Restore()
        {
            owned.Clear();
            ammo.Clear();
            owned.Add(Launcher);
            ammo.Add(0f);
            foreach (var pair in WeaponProgress.Owned())
            {
                if (pair.Key == Launcher || owned.Contains(pair.Key) || !world.Content.Weapons.ContainsKey(pair.Key))
                    continue;
                owned.Add(pair.Key);
                ammo.Add(pair.Value);
            }
            string equipped = WeaponProgress.Equipped();
            current = !string.IsNullOrEmpty(equipped) && owned.Contains(equipped) ? equipped : Launcher;
            MarkStored();
        }

        void Store(bool flush)
        {
            storeScratch.Clear();
            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i] != Launcher)
                    storeScratch.Add(new KeyValuePair<string, int>(owned[i], Mathf.FloorToInt(ammo[i] + 0.0001f)));
            }
            WeaponProgress.Store(storeScratch, current == Launcher ? "" : current, flush);
            MarkStored();
        }

        void MarkStored()
        {
            storedIds.Clear();
            storedIds.AddRange(owned);
            storedCurrent = current;
            storeDirty = false;
        }

        // ─── Rewind ──────────────────────────────────────────────────

        public void SaveState(StateWriter w)
        {
            w.Int(owned.Count);
            for (int i = 0; i < owned.Count; i++)
            {
                w.String(owned[i]);
                w.Float(ammo[i]);
            }
            w.String(current);
            w.Float(usableAt);
            w.Bool(hitPending);
            w.Float(swingStart);
            w.Float(hitAt);
            w.Float(swingEnd);
            w.Float(swingDirX);
            w.Float(swingDirY);
            w.Int(swingStep);
            w.Int(comboNext);
            w.Float(comboUntil);
            w.Float(swingDamage);
        }

        public void LoadState(StateReader r)
        {
            int count = r.Int();
            owned.Clear();
            ammo.Clear();
            for (int i = 0; i < count; i++)
            {
                owned.Add(r.String());
                ammo.Add(r.Float());
            }
            current = r.String();
            usableAt = r.Float();
            hitPending = r.Bool();
            swingStart = r.Float();
            hitAt = r.Float();
            swingEnd = r.Float();
            swingDirX = r.Float();
            swingDirY = r.Float();
            swingStep = r.Int();
            comboNext = r.Int();
            comboUntil = r.Float();
            swingDamage = r.Float();

            // Rewinding past a pickup takes the weapon back out of the saved prefs too (written when play resumes).
            if (!storeDirty && (current != storedCurrent || !SameHeld()))
                storeDirty = true;
        }

        bool SameHeld()
        {
            if (owned.Count != storedIds.Count)
                return false;
            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i] != storedIds[i])
                    return false;
            }
            return true;
        }
    }
}

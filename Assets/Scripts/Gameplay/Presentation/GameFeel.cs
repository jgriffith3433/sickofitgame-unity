using System;
using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Core.Fx;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// How a hit feels (Docs/Story.md "The feel of a hit"; game.json "feel"): the hit-freeze on strong hits, kills and
    /// explosions (<see cref="HitFreeze"/>), the enemy's white flash, squash and recoil (<see cref="SpriteFeel"/>), the hero's
    /// kick on every shot, and the glow of fire, lava and explosions (<see cref="Glow"/>). Entities call it as they are hurt,
    /// die or blow up; it only draws (and slows Unity's clock), so the simulation and its rewind never see it.
    /// </summary>
    public sealed class GameFeel : MonoBehaviour
    {
        /// <summary>What it did, for the playtest: kind ("freeze", "hit", "kick", "glow", "burst") and a detail.</summary>
        public static event Action<string, string> Played;

        GameplayWorld world;
        readonly Dictionary<string, GlowRule> ruleOf = new Dictionary<string, GlowRule>(StringComparer.Ordinal);
        readonly Dictionary<Entity, SpriteRenderer> drawnBy = new Dictionary<Entity, SpriteRenderer>();
        readonly HashSet<GameObject> glowingShots = new HashSet<GameObject>();
        readonly List<SpriteRenderer> scratch = new List<SpriteRenderer>();
        int lastVolleys;

        /// <summary>Hits that flashed, kicks the hero made, freezes started on this floor.</summary>
        public int Hits { get; private set; }
        public int Kicks { get; private set; }
        public int Freezes { get; private set; }

        static FeelConfig Config
        {
            get
            {
                var cfg = Feel.Config;
                return cfg.enabled ? cfg : null;
            }
        }

        public static GameFeel Create(GameplayWorld owner)
        {
            var feel = owner.gameObject.AddComponent<GameFeel>();
            feel.world = owner;
            return feel;
        }

        static void Tell(string kind, string detail) => Played?.Invoke(kind, detail);

        // ─── The hit-freeze ───────────────────────────────────────────

        bool Freeze(float seconds, string reason)
        {
            var cfg = Config;
            if (cfg == null || cfg.freeze == null || !cfg.freeze.enabled || seconds <= 0f)
                return false;
            if (!HitFreeze.Freeze(seconds, cfg.freeze.timeScale, cfg.freeze.cooldownSeconds, reason))
                return false;
            Freezes++;
            Tell("freeze", $"{reason} {seconds * 1000f:0} ms at {world.Now:0.00} s");
            return true;
        }

        /// <summary>The hero was hurt (the HUD's red flash calls it).</summary>
        public void HeroHurt()
        {
            var cfg = Config;
            if (cfg != null && cfg.freeze != null)
                Freeze(cfg.freeze.heroHurtSeconds, "hero hurt");
        }

        // ─── Enemy hits ───────────────────────────────────────────────

        /// <summary>
        /// An enemy took 'damage' ('lethal' when it is about to die). 'from' is where the blow came from (the shot, the
        /// attacker), if known: the sprite is pushed away from it.
        /// </summary>
        public void EnemyHit(Entity entity, int damage, bool lethal, bool hasFrom, Vector2 from)
        {
            var cfg = Config;
            if (cfg == null || entity == null || entity.Def == null || entity.IsPlayer)
                return;
            bool boss = entity.Tags.Contains("boss") || (entity.Def.tags != null && entity.Def.tags.Contains("boss"));
            var f = cfg.freeze;
            bool strong = f != null && damage >= (boss ? Mathf.Max(f.strongDamage, f.bossStrongDamage) : f.strongDamage);

            if (f != null && f.enabled)
            {
                float seconds = 0f;
                string reason = "";
                if (lethal && boss)
                {
                    seconds = f.bossKillSeconds;
                    reason = "boss kill";
                }
                else if (lethal && entity.MaxHealth >= f.killMinHealth)
                {
                    seconds = f.killSeconds;
                    reason = "kill";
                }
                if (strong && f.strongSeconds > seconds)
                {
                    seconds = f.strongSeconds;
                    reason = "strong hit";
                }
                if (seconds > 0f)
                    Freeze(seconds, $"{reason} {entity.DefId} ({damage} dmg)");
            }

            // A killing blow shows its death clip and pop instead.
            var hit = cfg.hit;
            if (lethal || hit == null || !hit.enabled || !entity.Def.visual.hurtFlash)
                return;
            var renderer = RendererOf(entity);
            if (renderer == null)
                return;
            Vector2 away = hasFrom ? entity.Position - from : world.Player != null ? entity.Position - world.Player.Position : Vector2.up;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.up;
            float scale = (strong ? hit.strongSquashScale : 1f) * (boss ? hit.bossSquashScale : 1f);
            float s = hit.squash * scale;
            // Wider and shorter at once, a little narrower on the way back (the spring), pushed away from the blow.
            SpriteFeel.Play(renderer, hit.flashColor, hit.flashStrength, hit.flashSeconds, new Vector2(s, -s * 0.8f),
                away * (hit.recoilDistance * (strong ? 1.5f : 1f) * (boss ? 0.5f : 1f)), hit.squashSeconds * (strong ? 1.25f : 1f));
            Hits++;
            Tell("hit", $"{entity.DefId} {damage} dmg{(strong ? " strong" : "")}, squash {s:0.00}");
        }

        /// <summary>The sprite that shows the entity: its own, or a placeholder child when it has no art.</summary>
        SpriteRenderer RendererOf(Entity entity)
        {
            if (drawnBy.TryGetValue(entity, out var cached) && cached != null)
                return cached;
            SpriteRenderer found = entity.Sprite != null && entity.Sprite.sprite != null ? entity.Sprite : null;
            if (found == null)
            {
                scratch.Clear();
                entity.GetComponentsInChildren(true, scratch);
                foreach (var r in scratch)
                {
                    if (r != null && r.sprite != null && (r.gameObject.name == "Placeholder" || r.gameObject.name == "Shape"))
                    {
                        found = r;
                        break;
                    }
                }
            }
            if (found != null)
                drawnBy[entity] = found;
            return found;
        }

        /// <summary>An explosion (explodeNear) went off: a freeze and a big glow.</summary>
        public void Explosion(Vector2 at, float radius, Color color)
        {
            var cfg = Config;
            if (cfg == null)
                return;
            if (cfg.freeze != null)
                Freeze(cfg.freeze.explosionSeconds, "explosion");
            var g = cfg.glow;
            if (g != null && g.enabled)
            {
                Glow.Burst(at, "explosion", color, Mathf.Max(0.5f, radius) * g.explosionGlowScale, g.explosionGlowAlpha, g.explosionGlowSeconds);
                Tell("burst", $"explosion r {radius:0.0} at ({at.x:0.0}, {at.y:0.0})");
            }
        }

        /// <summary>An enemy popped (its death flash): a glow of its color.</summary>
        public void DeathPop(Entity entity, Vector2 at)
        {
            var cfg = Config;
            var g = cfg != null ? cfg.glow : null;
            if (g == null || !g.enabled || entity == null)
                return;
            float size = Mathf.Clamp(entity.Radius * 3f, 0.5f, 2.5f) * g.deathGlowScale;
            Glow.Burst(at, "death", entity.FxColor, size, g.deathGlowAlpha, g.deathGlowSeconds);
        }

        // ─── Glows that follow entities ───────────────────────────────

        /// <summary>A new spawn: fire, lava and embers get their glow (feel.glow.rules, first match).</summary>
        public void OnSpawned(Entity entity)
        {
            var cfg = Config;
            var g = cfg != null ? cfg.glow : null;
            if (g == null || !g.enabled || entity == null || entity.Def == null)
                return;
            var def = entity.Def;
            if (!ruleOf.TryGetValue(def.id ?? "", out var rule))
            {
                rule = Match(g, def);
                ruleOf[def.id ?? ""] = rule;
                if (rule != null)
                    Tell("glow", $"{def.id}: rule {rule.name}");
            }
            if (rule == null)
                return;

            var t = entity.transform;
            Vector3 ls = t.lossyScale;
            Vector2 scale = new Vector2(Mathf.Abs(ls.x), Mathf.Abs(ls.y));
            Vector2 body = def.body.shape == ShapeKind.Box
                ? Vector2.Scale(def.body.size, scale)
                : Vector2.one * (def.body.radius * 2f * Mathf.Max(scale.x, scale.y));
            Vector2 center = Vector2.Scale(def.body.offset, scale);
            Vector2 size;
            Vector2 offset = center;
            if (rule.shape == GlowShape.Top)
            {
                size = new Vector2(body.x + rule.padding * 2f, rule.size);
                offset = new Vector2(center.x, center.y + body.y * 0.5f);
            }
            else
            {
                float grow = Mathf.Min(body.x, body.y) * (rule.size - 1f) + rule.padding * 2f;
                size = body + Vector2.one * grow;
            }
            size = new Vector2(Mathf.Clamp(size.x, rule.minSize, rule.maxSize), Mathf.Clamp(size.y, rule.minSize, rule.maxSize));

            Color color = rule.color;
            if (rule.entityColor)
            {
                if (def.visual.fxColor.a > 0f)
                    color = def.visual.fxColor;
                else if (def.visual.color != Color.white)
                    color = def.visual.color;
            }
            Glow.Follow(t, rule.name, offset, size, color, rule.alpha, rule.flicker, rule.flickerSpeed, rule.shape,
                def.visual.layer, def.visual.SortingOrder + rule.orderOffset);
        }

        static GlowRule Match(GlowFeel g, EntityDef def)
        {
            if (g.rules == null)
                return null;
            string id = def.id ?? "";
            foreach (var rule in g.rules)
            {
                if (rule == null || Contains(rule.exceptIds, id))
                    continue;
                if (Contains(rule.ids, id))
                    return rule;
                if (rule.tags != null && def.tags != null)
                {
                    foreach (var tag in rule.tags)
                        if (!string.IsNullOrEmpty(tag) && def.tags.Contains(tag))
                            return rule;
                }
                if (rule.sheets != null && rule.sheets.Count > 0 && def.visual.animator != null && def.visual.animator.states != null)
                {
                    foreach (var state in def.visual.animator.states)
                        if (state != null && state.spriteSheet != null && Contains(rule.sheets, state.spriteSheet.name))
                            return rule;
                }
            }
            return null;
        }

        static bool Contains(List<string> parts, string text)
        {
            if (parts == null || string.IsNullOrEmpty(text))
                return false;
            foreach (var p in parts)
                if (!string.IsNullOrEmpty(p) && text.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        // ─── The hero's recoil and his shots' glow ────────────────────

        void LateUpdate()
        {
            var cfg = Config;
            var pc = world != null ? world.PlayerController : null;
            if (cfg == null || pc == null)
                return;
            var clock = GameClock.Instance;
            bool rewinding = clock != null && clock.IsRewinding;

            int volleys = pc.VolleysFired;
            if (volleys > lastVolleys && !rewinding && cfg.fire != null && cfg.fire.enabled && pc.LastVolley.Count > 0)
                Kick(pc, cfg.fire);
            lastVolleys = volleys;

            var g = cfg.glow;
            var pool = pc.ShotPool;
            if (g != null && g.enabled && g.heroShots && pool != null)
            {
                var shots = pool.ActiveObjects;
                for (int i = 0; i < shots.Count; i++)
                {
                    var shot = shots[i];
                    if (shot != null && glowingShots.Add(shot))
                        Glow.Follow(shot.transform, "heroShot", Vector2.zero, Vector2.one * g.heroShotSize, g.heroShotColor, g.heroShotAlpha, 0.15f, 14f,
                            GlowShape.Round, SpriteLayer.Player, SpriteLayers.Projectile - 1);
                }
            }
        }

        void Kick(SickOfIt.Core.Input.PlayerController pc, FireKickFeel fire)
        {
            Vector2 aim = pc.LastVolley[0];
            if (aim.sqrMagnitude < 0.0001f)
                return;
            aim.Normalize();
            bool melee = pc.Armory != null && pc.Armory.TryGetCurrent(out var weapon) && weapon.melee;
            // A shot throws him back from the aim; a swing leans him into it.
            float k = melee ? fire.meleeScale : 1f;
            Vector2 offset = (melee ? aim : -aim) * (fire.kickDistance * k);
            // Squeezed along the kick, a little taller across it.
            float s = fire.squash * k;
            Vector2 squash = new Vector2(-s * Mathf.Abs(aim.x) + s * 0.5f * Mathf.Abs(aim.y), -s * Mathf.Abs(aim.y) + s * 0.5f * Mathf.Abs(aim.x));
            var renderers = world.PlayerRenderers;
            if (renderers == null)
                return;
            foreach (var r in renderers)
            {
                if (r != null && r.enabled && r.sprite != null)
                    SpriteFeel.Play(r, Color.white, 0f, 0f, squash, offset, fire.seconds);
            }
            var g = Config != null ? Config.glow : null;
            if (g != null && g.enabled && g.muzzleGlowSize > 0f && !melee)
                Glow.Burst((Vector2)pc.transform.position + aim * 0.5f, "muzzle", g.heroShotColor, g.muzzleGlowSize, g.muzzleGlowAlpha, 0.08f);
            Kicks++;
            if (Kicks <= 3 || Kicks % 25 == 0)
                Tell("kick", $"volley {pc.VolleysFired}{(melee ? " (swing)" : "")}, kick {offset.magnitude:0.000} back from ({aim.x:0.00}, {aim.y:0.00})");
        }
    }
}

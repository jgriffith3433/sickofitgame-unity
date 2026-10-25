using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SickOfIt.Gameplay.Content;
using SickOfIt.Json;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// Checks what JSON parsing can't: references between content (entity ids, sequences, effects, phases,
    /// animation states), required fields, target selectors, and a few common mistakes.
    /// </summary>
    public static class ContentValidator
    {
        /// <summary>Checks content. 'soundIds' (audio.json's sounds) enables checking sound references; null skips it.</summary>
        public static void Validate(GameContent content, JsonIssueList issues, ICollection<string> soundIds = null)
        {
            ValidSoundIds = soundIds;
            foreach (var pair in content.Entities.OrderBy(p => p.Key, StringComparer.Ordinal))
                ValidateEntity(content, pair.Value, issues);
            foreach (var pair in content.Sequences.OrderBy(p => p.Key, StringComparer.Ordinal))
                ValidateObject(content, pair.Value, content.SourceOf(pair.Value), "", null, null, issues, inSequence: true);
            foreach (var pair in content.Effects.OrderBy(p => p.Key, StringComparer.Ordinal))
                ValidateObject(content, pair.Value, content.SourceOf(pair.Value), "", null, null, issues, inSequence: false);
            foreach (var pair in content.Upgrades.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                ValidateObject(content, pair.Value, content.SourceOf(pair.Value), "", null, null, issues, inSequence: false);
                foreach (string requirement in pair.Value.requires)
                {
                    string problem = SickOfIt.Data.Requirements.Check(requirement);
                    if (problem != null)
                        issues.Error(content.SourceOf(pair.Value), "requires", problem);
                }
            }
            foreach (var pair in content.Weapons.OrderBy(p => p.Key, StringComparer.Ordinal))
                ValidateWeapon(content, pair.Value, issues);
            foreach (var pair in content.Circles.OrderBy(p => p.Key, StringComparer.Ordinal))
                ValidateCircle(content, pair.Value, issues);
            foreach (var pair in content.LevelScripts.OrderBy(p => p.Key, StringComparer.Ordinal))
                ValidateScript(content, pair.Value, issues, "script");
            foreach (var pair in content.Scenes.OrderBy(p => p.Key, StringComparer.Ordinal))
                ValidateScript(content, pair.Value, issues, "");
        }

        static void ValidateWeapon(GameContent content, WeaponDef weapon, JsonIssueList issues)
        {
            string file = content.SourceOf(weapon);
            ValidateObject(content, weapon, file, "", null, null, issues, inSequence: false);
            if (weapon.kind == WeaponKind.Melee)
            {
                if (weapon.melee.steps.Count == 0)
                    issues.Error(file, "melee.steps", "A melee weapon needs at least one combo step.");
                for (int i = 0; i < weapon.melee.steps.Count; i++)
                {
                    var step = weapon.melee.steps[i];
                    if (step.reach <= 0f)
                        issues.Error(file, $"melee.steps[{i}].reach", "A swing must reach somewhere (reach above 0).");
                    if (step.windup < 0.02f)
                        issues.Warning(file, $"melee.steps[{i}].windup", "Under 0.02 s the swing is not seen before it lands; it is raised to 0.02.");
                }
            }
            else if (weapon.cooldown <= 0f)
            {
                issues.Error(file, "cooldown", "A projectile weapon needs a cooldown above 0.");
            }
            if (weapon.ammo.capacity < 0 || (weapon.ammo.capacity > 0 && weapon.ammo.perShot < 1))
                issues.Error(file, "ammo", "Ammo capacity can't be negative, and a weapon with ammo spends at least 1 per shot.");
            if (weapon.id == "rocketLauncher" && weapon.kind != WeaponKind.Projectile)
                issues.Error(file, "kind", "The rocket launcher is the hero's gun from the start: it must be a Projectile weapon.");
        }

        static void ValidateCircle(GameContent content, CircleDef circle, JsonIssueList issues)
        {
            string file = content.SourceOf(circle);
            ValidateObject(content, circle, file, "", null, null, issues, inSequence: false);
            if (circle.firstFloor < circle.lastFloor)
                issues.Error(file, "lastFloor", "Floors count down as the hero climbs: firstFloor (the bottom) must be the larger number.");
            for (int i = 0; i < circle.hazards.Count; i++)
            {
                if (circle.hazards[i].telegraph < 1f)
                    issues.Warning(file, $"hazards[{i}].telegraph", "Telegraph hazards at least 1 second ahead (Story.md pacing rules); it is raised to 1.");
            }
            for (int i = 0; i < circle.milestones.Count; i++)
            {
                int f = circle.milestones[i].floor;
                if (f > circle.firstFloor || f < circle.lastFloor)
                    issues.Warning(file, $"milestones[{i}].floor", $"Floor {f} is outside this circle ({circle.firstFloor}-{circle.lastFloor}), so it never plays here.");
            }
            if (circle.track.segmentLength < 1f)
                issues.Error(file, "track.segmentLength", "Segments must be at least 1 unit long.");
        }

        [ThreadStatic] static ICollection<string> ValidSoundIds;

        static void ValidateEntity(GameContent content, EntityDef entity, JsonIssueList issues)
        {
            string file = content.SourceOf(entity);
            ValidateObject(content, entity, file, "", entity, null, issues, inSequence: false);

            var stateNames = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < entity.states.Count; i++)
            {
                if (!stateNames.Add(entity.states[i].name))
                    issues.Error(file, $"states[{i}].name", $"State \"{entity.states[i].name}\" is defined twice.");
            }

            var animator = entity.visual.animator;
            var animStates = new HashSet<string>(animator.states.Select(s => s.stateName), StringComparer.Ordinal);
            var animParams = new HashSet<string>(animator.parameters.Select(p => p.name), StringComparer.Ordinal);
            var animEvents = new HashSet<string>(animator.states.SelectMany(s => s.events).Select(e => e.functionName), StringComparer.Ordinal);
            for (int i = 0; i < animator.transitions.Count; i++)
            {
                var t = animator.transitions[i];
                string from = t.fromState;
                if (from != "Any" && from != "AnyState" && !animStates.Contains(from))
                    issues.Error(file, $"visual.animator.transitions[{i}].fromState", $"No animation state \"{from}\".{UnityJsonSerializer.SuggestName(from, animStates)}");
                for (int c = 0; c < t.conditions.Count; c++)
                {
                    string p = t.conditions[c].parameterName;
                    if (!animParams.Contains(p))
                        issues.Error(file, $"visual.animator.transitions[{i}].conditions[{c}].parameterName", $"No animator parameter \"{p}\".{UnityJsonSerializer.SuggestName(p, animParams)}");
                }
            }

            // Abilities that wait for an animation event or drive a parameter should name ones that exist.
            ContentWalker.Walk(entity, (owner, field, value, path) =>
            {
                if (!(value is string s) || string.IsNullOrEmpty(s) || !(owner is Ability))
                    return;
                bool isEvent = field.Name == "fireOnEvent" || field.Name == "hitEvent" || (owner is OnAnimationEventAbility && field.Name == "event");
                bool isParam = field.Name == "animBool" || field.Name == "animTrigger" || field.Name == "windupBool" || field.Name == "chargeBool" || (owner is AnimParamAbility && field.Name == "param");
                if (isEvent && !animEvents.Contains(s))
                    issues.Warning(file, path, $"No animation has an event \"{s}\", so this never happens.{UnityJsonSerializer.SuggestName(s, animEvents)}");
                if (isParam && !animParams.Contains(s))
                    issues.Warning(file, path, $"The animator has no parameter \"{s}\".{UnityJsonSerializer.SuggestName(s, animParams)}");
            });
        }

        /// <summary>A level's script ('root' "script", in levels/{name}.json) or a scene (root "", content/scenes/{id}.json).</summary>
        static void ValidateScript(GameContent content, LevelScript script, JsonIssueList issues, string root)
        {
            string file = content.SourceOf(script);
            string At(string path) => string.IsNullOrEmpty(root) ? path : root + "." + path;
            bool scene = string.IsNullOrEmpty(root);
            var phases = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < script.phases.Count; i++)
            {
                if (!phases.Add(script.phases[i].name))
                    issues.Error(file, At($"phases[{i}].name"), $"Phase \"{script.phases[i].name}\" is defined twice.");
            }
            ValidateObject(content, script, file, root, null, phases, issues, inSequence: false);
            if (script.phases.Count == 0 && script.rules.Count == 0)
                issues.Warning(file, root, "The script has no phases and no rules, so nothing happens.");
            if (script.floors.unitsPerFloor > 0f && script.floors.start <= 0)
                issues.Warning(file, At("floors.unitsPerFloor"), "unitsPerFloor does nothing without floors.start.");
            if (script.floors.end > script.floors.start && script.floors.start > 0)
                issues.Error(file, At("floors.end"), "Floors count down as the hero climbs: end must be a lower floor than start.");
            if (script.world.camera == CameraMode.Travel && string.IsNullOrEmpty(script.floors.circle))
                issues.Warning(file, At("floors.circle"), "A Travel stage without a circle uses plain default ground and no props.");
            // A scene ends through its own actions (returnToMenu, playVideo, goToLevel); a level needs a way to win.
            if (script.objective.complete == null && !scene)
                issues.Warning(file, At("objective.complete"), "No win condition: the level only ends through a completeLevel action or the player dying.");
            if (script.world.camera != CameraMode.Static)
            {
                void CheckCamera(object value, string path)
                {
                    if (value is CameraToAction || value is CameraZoomAction || value is CameraResetAction)
                        issues.Warning(file, At(path), $"Camera actions only move the Static camera; this script's world.camera is {script.world.camera}, so it does nothing.");
                }
                // The walker hands over a list (onEnter, do, steps) as one value, so look inside it too.
                ContentWalker.Walk(script, (owner, field, value, path) =>
                {
                    CheckCamera(value, path);
                    if (value is List<GameAction> actions)
                    {
                        for (int i = 0; i < actions.Count; i++)
                            CheckCamera(actions[i], $"{path}[{i}]");
                    }
                });
            }
        }

        static void ValidateObject(GameContent content, object root, string file, string rootPath, EntityDef entity, HashSet<string> phases,
            JsonIssueList issues, bool inSequence)
        {
            // Every kind of id a field can refer to: which names exist, and how to say "not found". A null set means
            // the check doesn't apply here (phases outside a script, states outside an entity).
            var refs = new (Type attribute, ICollection<string> names, string missing)[]
            {
                (typeof(EntityRefAttribute), content.Entities.Keys, "entity \"{0}\" in content/entities"),
                (typeof(SequenceRefAttribute), content.Sequences.Keys, "sequence \"{0}\" in content/sequences"),
                (typeof(EffectRefAttribute), content.Effects.Keys, "status effect \"{0}\" in content/effects"),
                (typeof(UpgradeRefAttribute), content.Upgrades.Keys, "upgrade \"{0}\" in content/upgrades"),
                (typeof(WeaponRefAttribute), content.Weapons.Keys, "weapon \"{0}\" in content/weapons"),
                (typeof(CircleRefAttribute), content.Circles.Keys, "circle \"{0}\" in content/circles"),
                (typeof(SoundRefAttribute), ValidSoundIds, "sound \"{0}\" in audio.json"),
                (typeof(PhaseRefAttribute), phases, "phase \"{0}\" in this script"),
                (typeof(EntityStateRefAttribute), entity?.states.Select(s => s.name).ToList(), "state \"{0}\" in this entity"),
                (typeof(AnimStateRefAttribute), entity != null && !entity.visual.animator.IsEmpty
                    ? entity.visual.animator.states.Select(s => s.stateName).ToList() : null, "animation state \"{0}\""),
            };

            void CheckRef(FieldInfo field, string id, string path)
            {
                foreach (var (attribute, names, missing) in refs)
                {
                    if (names != null && field.IsDefined(attribute, false) && !names.Contains(id))
                        issues.Error(file, path, $"No {string.Format(missing, id)}.{UnityJsonSerializer.SuggestName(id, names)}");
                }
            }

            ContentWalker.Walk(root, (owner, field, value, path) =>
            {
                path = string.IsNullOrEmpty(rootPath) ? path : rootPath + "." + path;

                if (field.IsDefined(typeof(RequiredAttribute), false))
                {
                    bool missing = value == null || (value is string s && s.Length == 0);
                    if (missing)
                        issues.Error(file, path, $"\"{field.Name}\" is required.");
                }

                if (value is string text && text.Length > 0)
                    CheckRef(field, text, path);
                if (value is List<string> ids)
                {
                    for (int i = 0; i < ids.Count; i++)
                        CheckRef(field, ids[i], $"{path}[{i}]");
                }

                if (value is TargetSelector selector)
                {
                    int count = selector.SetCount();
                    bool defaultsToSelf = owner is HealthBelowTrigger;
                    if (count > 1)
                        issues.Error(file, path, $"Set only one of player, self, id, tag, entity, all (found {count})." +
                            (selector.self && count == 2 ? " This target defaults to self: add \"self\": false." : ""));
                    else if (count == 0 && !defaultsToSelf)
                        issues.Error(file, path, "Say what to target: one of player, self, id, tag, entity, all.");
                }

                if (owner is RuleDef && field.Name == "when" && value == null)
                    issues.Error(file, path, "The rule has no \"when\" trigger, so it never runs.");
                if (owner is TransitionDef && field.Name == "when" && value == null)
                    issues.Error(file, path, "The transition has no \"when\" trigger.");
                if (owner is SpawnAction spawn && field.Name == "id" && !string.IsNullOrEmpty(spawn.id) && spawn.count > 1)
                    issues.Warning(file, path, "Several entities share one instance id; target them by tag instead.");

                if (!inSequence && value is List<GameAction> actions && !(owner is RunSequenceAction))
                {
                    for (int i = 0; i < actions.Count; i++)
                    {
                        if (actions[i] is WaitAction)
                            issues.Warning(file, $"{path}[{i}]", "\"wait\" only works inside a sequence (runSequence steps or content/sequences); here it does nothing.");
                    }
                }
            });
        }
    }
}

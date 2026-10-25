#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using SickOfIt.Gameplay.Content;
using SickOfIt.Json;


namespace SickOfIt.Tools.Editor.GameConfig
{
    /// <summary>
    /// Writes Docs/GameplayReference.md from the content types: every entity field, ability, trigger, action,
    /// pattern, and enum, with defaults and [Doc] text. Generated, so it always matches the code.
    /// Regenerated after each successful config import and by SickOfIt > Game Config > Generate Gameplay Reference.
    /// </summary>
    public static class GameplayReferenceGenerator
    {
        public const string OutputPath = "Docs/GameplayReference.md";

        // Defaults never contain assets, so an empty registry is enough.
        static readonly UnityJsonSerializer Serializer = new UnityJsonSerializer(new SickOfIt.Gameplay.RegistryAssetResolver(null));

        [MenuItem("SickOfIt/Game Config/Generate Gameplay Reference", priority = 180)]
        static void Menu()
        {
            Write();
            EditorUtility.OpenWithDefaultApp(OutputPath);
        }

        /// <summary>Writes the reference; returns true if the file changed.</summary>
        public static bool Write()
        {
            string text = Generate();
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            if (File.Exists(OutputPath) && File.ReadAllText(OutputPath) == text)
                return false;
            File.WriteAllText(OutputPath, text, new UTF8Encoding(false));
            return true;
        }

        public static string Generate()
        {
            var sb = new StringBuilder();
            var enums = new SortedDictionary<string, Type>(StringComparer.Ordinal);
            var helpers = new SortedDictionary<string, Type>(StringComparer.Ordinal);

            sb.AppendLine("# Gameplay Reference");
            sb.AppendLine();
            sb.AppendLine("> Generated from the code by `GameplayReferenceGenerator` — don't edit by hand. See AGENTS.md for the workflow and Docs/GameplayCookbook.md for worked examples.");
            sb.AppendLine();
            sb.AppendLine("Gameplay is data. **Entities** (enemies, bosses, projectiles, hazards) live in `Assets/GameConfig/content/entities/{id}.json`, " +
                          "reusable **sequences** in `content/sequences/{id}.json`, **status effects** in `content/effects/{id}.json`, the **Circles of Hell** " +
                          "(biome, enemy table, hazards, difficulty) in `content/circles/{id}.json`, **scenes** (a level script played on a level's stage, " +
                          "for scripted scenes and their tests) in `content/scenes/{id}.json`, and each level's " +
                          "**script** in the `\"script\"` field of `levels/{name}.json`. Every content file starts with `\"schemaVersion\": 1`; " +
                          "the id is the file name. Polymorphic things (abilities, triggers, actions, patterns) are objects with a `\"type\"`.");
            sb.AppendLine();
            sb.AppendLine("Missing fields use the defaults below; unknown fields are errors. Assets are paths under `Assets/` (e.g. `\"Images/Fireball.png\"`).");
            sb.AppendLine();

            sb.AppendLine("## Contents");
            foreach (var title in new[] { "Entity", "Abilities", "Triggers", "Actions", "Patterns", "Level script", "Circle", "Weapon", "Sequence", "Status effect", "Shared types", "Enums" })
                sb.AppendLine($"- [{title}](#{Anchor(title)})");
            sb.AppendLine();

            sb.AppendLine("## Entity");
            sb.AppendLine();
            Class(sb, typeof(EntityDef), enums, helpers, 3);
            foreach (var t in new[] { typeof(BodyDef), typeof(VisualDef), typeof(AnimatorDef), typeof(AnimParameterDef), typeof(AnimStateDef), typeof(AnimEventDef),
                         typeof(AnimTransitionDef), typeof(AnimConditionDef), typeof(HealthBarDef), typeof(SoundsDef), typeof(LootDef), typeof(StateDef), typeof(TransitionDef) })
                Class(sb, t, enums, helpers, 3);

            Polymorphic(sb, "Abilities", typeof(Ability), enums, helpers);
            Polymorphic(sb, "Triggers", typeof(Trigger), enums, helpers);
            Polymorphic(sb, "Actions", typeof(GameAction), enums, helpers);
            Polymorphic(sb, "Patterns", typeof(Pattern), enums, helpers);

            sb.AppendLine("## Level script");
            sb.AppendLine();
            foreach (var t in new[] { typeof(LevelScript), typeof(PhaseDef), typeof(RuleDef), typeof(ObjectiveDef), typeof(WorldDef), typeof(FloorsDef),
                         typeof(CounterDef), typeof(ProgressMarkerDef) })
                Class(sb, t, enums, helpers, 3);

            sb.AppendLine("## Circle");
            sb.AppendLine();
            foreach (var t in new[] { typeof(CircleDef), typeof(TrackDef), typeof(PropDef), typeof(CircleEnemyDef), typeof(CircleHazardDef),
                         typeof(DifficultyDef), typeof(MilestoneDef), typeof(SickOfIt.Data.Config.BackgroundLayerConfig) })
                Class(sb, t, enums, helpers, 3);

            sb.AppendLine("## Weapon");
            sb.AppendLine();
            foreach (var t in new[] { typeof(WeaponDef), typeof(WeaponGuideDef), typeof(WeaponAmmoDef), typeof(WeaponProjectileDef), typeof(WeaponMeleeDef),
                         typeof(MeleeStepDef), typeof(WeaponSoundsDef) })
                Class(sb, t, enums, helpers, 3);

            sb.AppendLine("## Sequence");
            sb.AppendLine();
            Class(sb, typeof(SequenceDef), enums, helpers, 3);

            sb.AppendLine("## Status effect");
            sb.AppendLine();
            Class(sb, typeof(StatusEffectDef), enums, helpers, 3);

            sb.AppendLine("## Shared types");
            sb.AppendLine();
            foreach (var t in new[] { typeof(TargetSelector), typeof(StatModifierDef), typeof(WeightedActions) })
                Class(sb, t, enums, helpers, 3);

            sb.AppendLine("## Enums");
            sb.AppendLine();
            foreach (var pair in enums)
            {
                var type = pair.Value;
                sb.AppendLine($"### {type.Name}");
                sb.AppendLine();
                string doc = type.GetCustomAttribute<DocAttribute>()?.Text;
                if (!string.IsNullOrEmpty(doc))
                {
                    sb.AppendLine(doc);
                    sb.AppendLine();
                }
                foreach (var name in Enum.GetNames(type))
                {
                    string valueDoc = type.GetField(name).GetCustomAttribute<DocAttribute>()?.Text;
                    sb.AppendLine(string.IsNullOrEmpty(valueDoc) ? $"- `{name}`" : $"- `{name}` — {valueDoc}");
                }
                sb.AppendLine();
            }

            return sb.ToString().Replace("\r\n", "\n");
        }

        static void Polymorphic(StringBuilder sb, string title, Type baseType, SortedDictionary<string, Type> enums, SortedDictionary<string, Type> helpers)
        {
            sb.AppendLine($"## {title}");
            sb.AppendLine();
            string doc = baseType.GetCustomAttribute<DocAttribute>()?.Text;
            if (!string.IsNullOrEmpty(doc))
            {
                sb.AppendLine(doc);
                sb.AppendLine();
            }
            var subtypes = JsonTypeRegistry.Subtypes(baseType).OrderBy(p => p.Key, StringComparer.Ordinal).ToList();
            sb.AppendLine(string.Join(" · ", subtypes.Select(p => $"[`{p.Key}`](#{Anchor(p.Key)})")));
            sb.AppendLine();
            foreach (var pair in subtypes)
            {
                sb.AppendLine($"### {pair.Key}");
                sb.AppendLine();
                string typeDoc = pair.Value.GetCustomAttribute<DocAttribute>()?.Text;
                if (!string.IsNullOrEmpty(typeDoc))
                {
                    sb.AppendLine(typeDoc);
                    sb.AppendLine();
                }
                Fields(sb, pair.Value, enums, helpers);
            }
        }

        static void Class(StringBuilder sb, Type type, SortedDictionary<string, Type> enums, SortedDictionary<string, Type> helpers, int level)
        {
            sb.AppendLine($"{new string('#', level)} {type.Name}");
            sb.AppendLine();
            string doc = type.GetCustomAttribute<DocAttribute>()?.Text;
            if (!string.IsNullOrEmpty(doc))
            {
                sb.AppendLine(doc);
                sb.AppendLine();
            }
            Fields(sb, type, enums, helpers);
        }

        static void Fields(StringBuilder sb, Type type, SortedDictionary<string, Type> enums, SortedDictionary<string, Type> helpers)
        {
            var fields = UnitySerializationRules.GetSerializedFields(type);
            if (fields.Length == 0)
            {
                sb.AppendLine("No fields.");
                sb.AppendLine();
                return;
            }
            object instance = UnityJsonSerializer.CreateInstance(type);
            sb.AppendLine("| Field | Type | Default | Notes |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var field in fields)
            {
                string notes = field.GetCustomAttribute<DocAttribute>()?.Text ?? field.GetCustomAttribute<TooltipAttribute>()?.tooltip ?? "";
                if (field.IsDefined(typeof(RequiredAttribute), false)) notes = "**Required.** " + notes;
                if (field.IsDefined(typeof(EntityRefAttribute), false)) notes += " (entity id)";
                if (field.IsDefined(typeof(SequenceRefAttribute), false)) notes += " (sequence id)";
                if (field.IsDefined(typeof(EffectRefAttribute), false)) notes += " (status effect id)";
                if (field.IsDefined(typeof(PhaseRefAttribute), false)) notes += " (phase name)";
                if (field.IsDefined(typeof(CircleRefAttribute), false)) notes += " (circle id)";
                sb.AppendLine($"| `{field.Name}` | {TypeName(field.FieldType, enums, helpers)} | {Default(field, instance)} | {Escape(notes.Trim())} |");
            }
            sb.AppendLine();
        }

        static string TypeName(Type type, SortedDictionary<string, Type> enums, SortedDictionary<string, Type> helpers)
        {
            if (UnitySerializationRules.IsCollection(type, out var element))
                return "list of " + TypeName(element, enums, helpers);
            if (type == typeof(string)) return "string";
            if (type == typeof(float) || type == typeof(double)) return "number";
            if (type == typeof(int) || type == typeof(long)) return "integer";
            if (type == typeof(bool)) return "true/false";
            if (type == typeof(Vector2)) return "[x, y]";
            if (type == typeof(Color)) return "color";
            if (type.IsEnum)
            {
                enums[type.Name] = type;
                return $"[{type.Name}](#{Anchor(type.Name)})";
            }
            if (UnitySerializationRules.IsUnityObject(type)) return $"asset ({type.Name})";
            if (JsonTypeRegistry.IsPolymorphic(type))
            {
                string section = type == typeof(Ability) ? "Abilities" : type == typeof(Trigger) ? "Triggers" : type == typeof(GameAction) ? "Actions" : "Patterns";
                return $"[{(type == typeof(GameAction) ? "Action" : type.Name)}](#{Anchor(section)})";
            }
            helpers[type.Name] = type;
            return $"[{type.Name}](#{Anchor(type.Name)})";
        }

        static string Default(FieldInfo field, object instance)
        {
            object value = field.GetValue(instance);
            if (value == null)
                return "—";
            if (UnitySerializationRules.IsUnityObject(field.FieldType))
                return "null";
            if (JsonTypeRegistry.IsPolymorphic(field.FieldType))
                return "—";
            var issues = new JsonIssueList();
            var token = Serializer.WriteValue(value, field.FieldType, issues, "", field.Name);
            string text = JsonText.Compact(token);
            if (text.Length > 40)
                return "(object)";
            return "`" + text + "`";
        }

        static string Escape(string text) => text.Replace("|", "\\|").Replace("\n", " ");

        static string Anchor(string title) => title.ToLowerInvariant().Replace(" ", "-");
    }
}
#endif

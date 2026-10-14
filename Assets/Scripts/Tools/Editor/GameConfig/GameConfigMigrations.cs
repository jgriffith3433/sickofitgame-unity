#if UNITY_EDITOR
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using SickOfIt.Json;

namespace SickOfIt.Tools.Editor.GameConfig
{
    /// <summary>
    /// Upgrades config files written in an older format.
    ///
    /// To change the format: append a step that rewrites a file from the previous version to the next,
    /// and bump <see cref="CurrentVersion"/>. Old files are upgraded on import and rewritten on disk.
    /// A step receives the file's path relative to the config root so it can act on one kind of file.
    /// </summary>
    public static class GameConfigMigrations
    {
        public const int CurrentVersion = 1;

        public delegate void Step(JObject file, string relativePath);

        /// <summary>Steps[n] upgrades a file from version n + 1 to n + 2.</summary>
        public static readonly Step[] Steps = { };

        public static bool Upgrade(JObject file, string relativePath, JsonIssueList issues, out bool changed)
        {
            return Upgrade(file, relativePath, issues, Steps, CurrentVersion, out changed);
        }

        public static bool Upgrade(JObject file, string relativePath, JsonIssueList issues, IReadOnlyList<Step> steps, int currentVersion, out bool changed)
        {
            changed = false;
            string key = GameConfigLayout.VersionKey;
            var token = file[key];
            if (token == null)
            {
                issues.Error(relativePath, "", $"Missing \"{key}\". Add \"{key}\": {currentVersion} as the first field.", JsonText.LineOf(file));
                return false;
            }
            if (token.Type != JTokenType.Integer)
            {
                issues.Error(relativePath, key, "Must be a whole number.", JsonText.LineOf(token));
                return false;
            }

            long version = (long)token;
            if (version < 1 || version > currentVersion)
            {
                issues.Error(relativePath, key, version > currentVersion
                    ? $"Version {version} is newer than this project understands ({currentVersion}). Update the project's code first."
                    : $"Version {version} is not valid.", JsonText.LineOf(token));
                return false;
            }

            while (version < currentVersion)
            {
                steps[(int)version - 1](file, relativePath);
                version++;
                changed = true;
            }
            file[key] = version;
            return true;
        }
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using SickOfIt.Json;

namespace SickOfIt.Tools.Editor.GameConfig
{
    /// <summary>
    /// The outcome of one config operation. The latest report is always written to Logs/GameConfig.log
    /// so it can be read without the editor's console.
    /// </summary>
    public sealed class GameConfigReport
    {
        public const string LogPath = "Logs/GameConfig.log";
        const int ConsoleIssueLimit = 25;

        public readonly string Operation;
        public readonly string Reason;
        public readonly JsonIssueList Issues;
        public readonly List<string> Notes = new List<string>();
        public readonly DateTime Time = DateTime.Now;

        public GameConfigReport(string operation, string reason, JsonIssueList issues = null)
        {
            Operation = operation;
            Reason = reason;
            Issues = issues ?? new JsonIssueList();
        }

        public bool Success => !Issues.HasErrors;

        public string Summary =>
            $"Game config {Operation.ToLowerInvariant()} {(Success ? "succeeded" : "FAILED")}: {Issues.ErrorCount} error(s), {Issues.WarningCount} warning(s).";

        public string ToText()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Summary);
            sb.AppendLine($"When: {Time:yyyy-MM-dd HH:mm:ss}");
            if (!string.IsNullOrEmpty(Reason))
                sb.AppendLine($"Why: {Reason}");
            foreach (string note in Notes)
                sb.AppendLine(note);
            if (Issues.Count > 0)
            {
                sb.AppendLine();
                foreach (var issue in Issues.OrderByDescending(i => i.Severity))
                    sb.AppendLine(issue.ToString());
            }
            return sb.ToString();
        }

        public void Write()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.WriteAllText(LogPath, ToText());

            if (!Success)
            {
                var errors = Issues.Where(i => i.Severity == JsonIssueSeverity.Error).Take(ConsoleIssueLimit).Select(i => i.ToString());
                Debug.LogError($"{Summary} Full report: {LogPath}\n{string.Join("\n", errors)}");
            }
            else
            {
                Debug.Log($"{Summary} {string.Join(" ", Notes)} Full report: {LogPath}");
            }
        }
    }
}
#endif

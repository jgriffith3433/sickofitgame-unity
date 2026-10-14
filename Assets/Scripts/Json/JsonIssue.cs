using System.Collections.Generic;
using System.Text;

namespace SickOfIt.Json
{
    public enum JsonIssueSeverity
    {
        Warning,
        Error
    }

    /// <summary>
    /// One problem found while reading or writing config JSON, located by file, line, and JSON path
    /// so a person or an agent can go straight to it.
    /// </summary>
    public sealed class JsonIssue
    {
        public JsonIssueSeverity Severity;
        public string File;
        public int Line;
        public string Path;
        public string Message;

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Severity == JsonIssueSeverity.Error ? "ERROR   " : "WARNING ");
            if (!string.IsNullOrEmpty(File))
            {
                sb.Append(File);
                if (Line > 0)
                    sb.Append(':').Append(Line);
                sb.Append(' ');
            }
            if (!string.IsNullOrEmpty(Path))
                sb.Append('@').Append(Path).Append(' ');
            sb.Append("- ").Append(Message);
            return sb.ToString();
        }
    }

    public sealed class JsonIssueList : List<JsonIssue>
    {
        public int ErrorCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Count; i++)
                {
                    if (this[i].Severity == JsonIssueSeverity.Error)
                        count++;
                }
                return count;
            }
        }

        public int WarningCount => Count - ErrorCount;
        public bool HasErrors => ErrorCount > 0;

        public void Error(string file, string path, string message, int line = 0)
        {
            Add(new JsonIssue { Severity = JsonIssueSeverity.Error, File = file, Path = path, Message = message, Line = line });
        }

        public void Warning(string file, string path, string message, int line = 0)
        {
            Add(new JsonIssue { Severity = JsonIssueSeverity.Warning, File = file, Path = path, Message = message, Line = line });
        }
    }
}

using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SickOfIt.Tools.Editor
{
    /// <summary>
    /// The website content this repo owns (Tools/Web/site: the lore pages, their art and script, blog posts), installed
    /// into the website by the deploy (GameDeployWindow step 7d), so it reaches the beta site with the build and goes
    /// live with the game. Every file under the folder is copied over the website at the same path, except
    /// <c>site.json</c>, <c>README.md</c> and anything in a folder starting with "_". <c>site.json</c>'s inserts put
    /// snippets (a post card, a note) into website pages this repo doesn't own, between
    /// <c>&lt;!-- Hawkes site: id --&gt;</c> comments, so re-running replaces them instead of adding more.
    /// </summary>
    public static class SiteContent
    {
        public const string Manifest = "site.json";

        [Serializable]
        public class Insert
        {
            /// <summary>Names the marked block in the page.</summary>
            public string id;
            /// <summary>The website page, relative to the site root ("blog/index.html").</summary>
            public string page;
            /// <summary>Regex: the block goes right after its first match.</summary>
            public string after;
            /// <summary>Optional regex: what's between 'after' and its first match is replaced by the block.</summary>
            public string until;
            /// <summary>The snippet, relative to Tools/Web/site.</summary>
            public string file;
        }

        [Serializable]
        public class SiteManifest
        {
            public int schemaVersion;
            public Insert[] inserts;
        }

        /// <summary>This project's Tools/Web/site.</summary>
        public static string SourceFolder =>
            Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", "Tools", "Web", "site");

        /// <summary>Whether a file in the source folder is copied to the website (relative path, either slash).</summary>
        public static bool IsCopied(string relativePath)
        {
            string rel = relativePath.Replace('\\', '/');
            if (rel == Manifest || rel == "README.md")
                return false;
            foreach (string part in rel.Split('/'))
                if (part.StartsWith("_") || part.StartsWith("."))
                    return false;
            return true;
        }

        /// <summary>
        /// Copies the site content into the website and applies the inserts. Returns how many inserts couldn't find
        /// their place (each one logged); copying never stops for them.
        /// </summary>
        public static int Install(string sourceFolder, string websiteFolder, Action<string> log)
        {
            if (!Directory.Exists(sourceFolder))
                throw new Exception("Site content folder not found: " + sourceFolder);
            if (!Directory.Exists(websiteFolder))
                throw new Exception("Website folder not found: " + websiteFolder);

            int copied = 0;
            foreach (string file in Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories))
            {
                string rel = file.Substring(sourceFolder.Length).TrimStart('\\', '/').Replace('\\', '/');
                if (!IsCopied(rel))
                    continue;
                string target = Path.Combine(websiteFolder, rel.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(target) ?? websiteFolder);
                File.Copy(file, target, true);
                copied++;
            }
            log($"  Copied {copied} file(s) from Tools/Web/site.");

            string manifestPath = Path.Combine(sourceFolder, Manifest);
            if (!File.Exists(manifestPath))
                return 0;
            var manifest = JsonUtility.FromJson<SiteManifest>(File.ReadAllText(manifestPath));
            int missed = 0;
            foreach (var insert in manifest?.inserts ?? Array.Empty<Insert>())
            {
                string page = Path.Combine(websiteFolder, (insert.page ?? "").Replace('/', Path.DirectorySeparatorChar));
                string snippetPath = Path.Combine(sourceFolder, (insert.file ?? "").Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(page) || !File.Exists(snippetPath))
                {
                    log($"  WARNING: insert '{insert.id}': {(File.Exists(page) ? "no snippet " + insert.file : "no page " + insert.page)}, skipped");
                    missed++;
                    continue;
                }
                string html = File.ReadAllText(page);
                string updated = ApplyInsert(html, insert.id, insert.after, insert.until, File.ReadAllText(snippetPath));
                if (updated == null)
                {
                    log($"  WARNING: insert '{insert.id}': '{insert.after}' not found in {insert.page}, skipped");
                    missed++;
                    continue;
                }
                if (updated != html)
                {
                    File.WriteAllText(page, updated);
                    log($"  {insert.page}: {insert.id}");
                }
            }
            return missed;
        }

        /// <summary>
        /// The page with exactly one marked block for <paramref name="id"/> holding <paramref name="snippet"/>: an
        /// existing block is replaced where it is; else the block goes after the first match of
        /// <paramref name="after"/>, replacing everything up to the first match of <paramref name="until"/> when given.
        /// Null when the page has neither the block nor the place for it. Keeps the page's line endings.
        /// </summary>
        public static string ApplyInsert(string html, string id, string after, string until, string snippet)
        {
            string newline = html.Contains("\r\n") ? "\r\n" : "\n";
            string start = $"<!-- Hawkes site: {id} -->";
            string end = $"<!-- /Hawkes site: {id} -->";
            string body = snippet.Replace("\r\n", "\n").TrimEnd('\n').Replace("\n", newline);
            string block = start + newline + body + newline + end;

            var existing = new Regex(Regex.Escape(start) + ".*?" + Regex.Escape(end), RegexOptions.Singleline).Match(html);
            if (existing.Success)
                return html.Substring(0, existing.Index) + block + html.Substring(existing.Index + existing.Length);

            if (string.IsNullOrEmpty(after))
                return null;
            var place = Regex.Match(html, after);
            if (!place.Success)
                return null;
            int at = place.Index + place.Length;
            if (string.IsNullOrEmpty(until))
                return html.Substring(0, at) + newline + block + html.Substring(at); // the page's next line follows as it was

            var stop = new Regex(until).Match(html, at);
            if (!stop.Success)
                return null;
            // The "until" match goes back on its own line with the indentation it had.
            int lineStart = html.LastIndexOf('\n', Math.Max(0, stop.Index - 1)) + 1;
            string before = html.Substring(lineStart, stop.Index - lineStart);
            string indent = lineStart > at && before.Trim().Length == 0 ? before : "";
            return html.Substring(0, at) + newline + block + newline + indent + html.Substring(stop.Index);
        }
    }
}

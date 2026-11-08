using System;
using System.IO;
using System.Linq;

namespace SickOfIt.Tools.Editor
{
    /// <summary>
    /// GitHub refuses any file over 100 MB, so a Game/v&lt;n&gt; with one can never go live (v183: a 159.9 MB
    /// SickOfIt.data.unityweb, "pre-receive hook declined"). After the build, this lists the largest files and fails
    /// the deploy if any is over <see cref="MaxBytes"/>.
    /// </summary>
    public static class BuildOutputGuard
    {
        public const long MaxBytes = 95L * 1024 * 1024;

        public static void Check(string folder, Action<string> log)
        {
            if (!Directory.Exists(folder))
                throw new Exception("Build output not found: " + folder);

            WriteSizeReport(folder, log);
            var files = new DirectoryInfo(folder).GetFiles("*", SearchOption.AllDirectories).OrderByDescending(f => f.Length).ToList();
            long total = files.Sum(f => f.Length);
            log($"  {files.Count} files, {Mb(total)} in {folder}. Largest:");
            foreach (var file in files.Take(8))
                log($"    {Mb(file.Length),9}  {Relative(folder, file.FullName)}");

            var tooBig = files.Where(f => f.Length > MaxBytes).ToList();
            if (tooBig.Count == 0)
            {
                log($"  Every file is under {Mb(MaxBytes)} (GitHub refuses files over 100 MB).");
                return;
            }
            string list = string.Join(", ", tooBig.Select(f => $"{Relative(folder, f.FullName)} ({Mb(f.Length)})"));
            throw new Exception($"Build output has files over {Mb(MaxBytes)}, which GitHub refuses (100 MB per file), so it can't go live: {list}. " +
                                "Move the heavy content into Addressables (see PlayerDataSplit) or split the group that grew.");
        }

        /// <summary>The report's file name at the top of the build output.</summary>
        public const string SizeReportFile = "build-size.json";

        /// <summary>
        /// What the build weighs (Docs/BuildSize.md): every file, and each part (the player's Build and StreamingAssets, each
        /// Addressables variant), written as <see cref="SizeReportFile"/> next to the build and to the log, so two builds can
        /// be compared. The baseline it is measured against is v17 (2026-11-07): 3,658 files, 1,267.4 MB (decimal).
        /// </summary>
        public static void WriteSizeReport(string folder, Action<string> log)
        {
            string report = Path.Combine(folder, SizeReportFile);
            if (File.Exists(report))
                File.Delete(report);
            var files = new DirectoryInfo(folder).GetFiles("*", SearchOption.AllDirectories);
            long total = files.Sum(f => f.Length);
            var parts = files
                .GroupBy(f => Part(Relative(folder, f.FullName)))
                .Select(g => (part: g.Key, count: g.Count(), bytes: g.Sum(f => f.Length)))
                .OrderByDescending(p => p.bytes)
                .ToList();
            log($"  Build size: {files.Length} files, {total / 1000000.0:F1} MB ({Mb(total)}); v17 before the size work: 3658 files, 1267.4 MB.");
            foreach (var p in parts)
                log($"    {p.bytes / 1000000.0,8:F1} MB {p.count,6} files  {p.part}");

            var json = new System.Text.StringBuilder();
            json.Append("{\n  \"files\": ").Append(files.Length).Append(",\n  \"bytes\": ").Append(total).Append(",\n  \"parts\": [");
            for (int i = 0; i < parts.Count; i++)
            {
                json.Append(i == 0 ? "\n" : ",\n").Append("    { \"part\": \"").Append(parts[i].part.Replace("\\", "/").Replace("\"", "'"))
                    .Append("\", \"files\": ").Append(parts[i].count).Append(", \"bytes\": ").Append(parts[i].bytes).Append(" }");
            }
            json.Append("\n  ]\n}\n");
            File.WriteAllText(report, json.ToString());
        }

        /// <summary>The part a file belongs to: its first two folders (SickOfIt/Build, ServerData/WebGL_ASTC, ...).</summary>
        static string Part(string relative)
        {
            string[] bits = relative.Split('/');
            if (bits.Length <= 1)
                return "(top)";
            if (bits.Length == 2)
                return bits[0];
            return bits[0] + "/" + bits[1];
        }

        static string Mb(long bytes) => $"{bytes / (1024.0 * 1024.0):F1} MB";

        static string Relative(string root, string path) => path.Substring(Path.GetFullPath(root).TrimEnd('\\', '/').Length).TrimStart('\\', '/').Replace('\\', '/');
    }
}

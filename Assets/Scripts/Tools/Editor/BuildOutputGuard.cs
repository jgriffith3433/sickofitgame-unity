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

        static string Mb(long bytes) => $"{bytes / (1024.0 * 1024.0):F1} MB";

        static string Relative(string root, string path) => path.Substring(Path.GetFullPath(root).TrimEnd('\\', '/').Length).TrimStart('\\', '/').Replace('\\', '/');
    }
}

using System;
using System.IO;

namespace COTL_AL_NPCs
{
    internal static partial class FollowerAiSidecarBridge
    {
        private const string ReadyFileName = "sidecar-ready.json";

        private static string RootDirectory => Path.Combine(FollowerAiSaveScope.CurrentScopedDirectory, "Sidecar");
        private static string SnapshotDirectory => Path.Combine(RootDirectory, "snapshots");
        private static string RequestDirectory => Path.Combine(RootDirectory, "requests");
        private static string ResponseDirectory => Path.Combine(RootDirectory, "responses");
        private static string ProgressDirectory => Path.Combine(RootDirectory, "progress");
        private static string ArchiveDirectory => Path.Combine(RootDirectory, "archive");
        private static string InternetSourcesDirectory => Path.Combine(RootDirectory, "internet-sources");
        private static string ReadyPath => Path.Combine(RootDirectory, ReadyFileName);

        private static void EnsureDirectories()
        {
            Directory.CreateDirectory(RootDirectory);
            Directory.CreateDirectory(SnapshotDirectory);
            Directory.CreateDirectory(RequestDirectory);
            Directory.CreateDirectory(ResponseDirectory);
            Directory.CreateDirectory(ProgressDirectory);
            Directory.CreateDirectory(ArchiveDirectory);
            Directory.CreateDirectory(InternetSourcesDirectory);
        }

        private static void CleanupOldFiles()
        {
            try
            {
                var cutoff = DateTime.UtcNow.AddMinutes(-15);
                foreach (var directory in new[] { RequestDirectory, ResponseDirectory, ArchiveDirectory, ProgressDirectory })
                {
                    if (!Directory.Exists(directory))
                        continue;

                    foreach (var file in Directory.GetFiles(directory, "*.json"))
                    {
                        if (File.GetLastWriteTimeUtc(file) < cutoff)
                            File.Delete(file);
                    }
                }
            }
            catch
            {
                // Cleanup is best effort only.
            }
        }

        private static void WriteJsonAtomic(string path, string json)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var tempPath = $"{path}.tmp";
            File.WriteAllText(tempPath, json);
            if (File.Exists(path))
                File.Delete(path);
            File.Move(tempPath, path);
        }

        private static void TryArchiveRequest(string requestPath)
        {
            try
            {
                if (!File.Exists(requestPath))
                    return;

                var destination = Path.Combine(ArchiveDirectory, Path.GetFileName(requestPath));
                if (File.Exists(destination))
                    File.Delete(destination);
                File.Move(requestPath, destination);
            }
            catch
            {
                // Archive is best effort only.
            }
        }

        private static string Trim(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value.Length <= maxLength
                ? value
                : value.Substring(0, Math.Max(0, maxLength - 3)) + "...";
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;

namespace COTL_AL_NPCs
{
    internal static class FollowerAiFileStore
    {
        internal static void WriteAllTextAtomic(string path, string content)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            EnsureParentDirectory(path);
            var temporaryPath = BuildTemporaryPath(path);
            File.WriteAllText(temporaryPath, content ?? string.Empty);
            ReplaceFile(temporaryPath, path);
        }

        internal static void WriteAllLinesAtomic(string path, IEnumerable<string> lines)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            EnsureParentDirectory(path);
            var temporaryPath = BuildTemporaryPath(path);
            File.WriteAllLines(temporaryPath, lines ?? Array.Empty<string>());
            ReplaceFile(temporaryPath, path);
        }

        internal static void EnsureParentDirectory(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
        }

        private static string BuildTemporaryPath(string path)
        {
            var directory = Path.GetDirectoryName(path) ?? string.Empty;
            var fileName = Path.GetFileName(path);
            return Path.Combine(directory, $".{fileName}.{Guid.NewGuid():N}.tmp");
        }

        private static void ReplaceFile(string temporaryPath, string destinationPath)
        {
            if (File.Exists(destinationPath))
            {
                File.Replace(temporaryPath, destinationPath, null);
                return;
            }

            File.Move(temporaryPath, destinationPath);
        }
    }
}

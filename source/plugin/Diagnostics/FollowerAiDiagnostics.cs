using System;
using System.Collections.Generic;
using System.Linq;

namespace COTL_AL_NPCs
{
    internal static class FollowerAiDiagnostics
    {
        private const int MaxRecentLines = 80;
        private static readonly object Sync = new object();
        private static readonly List<string> RecentLines = new List<string>();
        private static string currentScopeKey = string.Empty;

        internal static void Record(string source, string detail, int speakerID = -1, int actorID = -1, string actionName = null, string playerText = null)
        {
            if (string.IsNullOrWhiteSpace(detail))
                return;

            var line = $"{DateTime.Now:HH:mm:ss} source={Clean(source)} speaker={speakerID} actor={actorID} action={Clean(actionName)} text={Clean(playerText)} detail={Clean(detail)}";
            lock (Sync)
            {
                EnsureScopeLocked();
                RecentLines.Add(line);
                if (RecentLines.Count > MaxRecentLines)
                    RecentLines.RemoveRange(0, RecentLines.Count - MaxRecentLines);
            }

            AICharacterPlugin.LogInfoVerbose($"AI diagnostic: {line}");
        }

        internal static List<string> GetRecent(int maxLines = 12)
        {
            lock (Sync)
            {
                EnsureScopeLocked();
                return RecentLines
                    .Skip(Math.Max(0, RecentLines.Count - Math.Max(1, maxLines)))
                    .ToList();
            }
        }

        internal static string BuildHiddenDiagnosticPanel(int maxLines = 12)
        {
            var lines = GetRecent(maxLines);
            return lines.Count == 0
                ? "--- Recent AI diagnostics ---\nnone recorded since this save session began."
                : "--- Recent AI diagnostics ---\n" + string.Join(Environment.NewLine, lines);
        }

        internal static void ResetForSaveScopeChange()
        {
            lock (Sync)
            {
                currentScopeKey = FollowerAiSaveScope.CurrentSaveKey;
                RecentLines.Clear();
            }
        }

        private static void EnsureScopeLocked()
        {
            var scopeKey = FollowerAiSaveScope.CurrentSaveKey;
            if (string.Equals(currentScopeKey, scopeKey, StringComparison.OrdinalIgnoreCase))
                return;

            currentScopeKey = scopeKey;
            RecentLines.Clear();
        }

        private static string Clean(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "-";

            var flat = text
                .Replace(Environment.NewLine, " ")
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();

            while (flat.Contains("  "))
                flat = flat.Replace("  ", " ");

            return flat.Length <= 320 ? flat : flat.Substring(0, 317) + "...";
        }
    }
}

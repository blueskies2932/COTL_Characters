using System;
using System.Collections.Generic;
using System.Linq;

namespace COTL_AL_NPCs
{
    public static partial class FollowerAIManager
    {
        public static void AddConversationLine(int followerID, string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            var ai = GetOrCreateAI(followerID);
            ai.LastInteraction = DateTime.Now;
            ai.ConversationHistory.Add(line.Trim());

            const int maxConversationLines = 80;
            if (ai.ConversationHistory.Count > maxConversationLines)
                ai.ConversationHistory.RemoveRange(0, ai.ConversationHistory.Count - maxConversationLines);

            MarkStateDirty();
        }

        public static List<string> GetConversationHistory(int followerID)
        {
            return new List<string>();
        }

        public static List<string> GetSavedConversationHistory(int followerID)
        {
            var lines = GetOrCreateAI(followerID).ConversationHistory
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();
            return lines.Skip(Math.Max(0, lines.Count - 80)).ToList();
        }

        public static List<string> GetOutcomeMemory(int followerID)
        {
            return GetOrCreateAI(followerID).OutcomeMemory
                .Select(SanitizeAiVisibleMemoryLine)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();
        }

        public static void AddOutcomeLine(int followerID, string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;
            if (IsObsoleteMemoryLine(line))
                return;

            var ai = GetOrCreateAI(followerID);
            ai.OutcomeMemory.Add(line.Trim());

            const int maxOutcomeLines = 24;
            if (ai.OutcomeMemory.Count > maxOutcomeLines)
                ai.OutcomeMemory.RemoveRange(0, ai.OutcomeMemory.Count - maxOutcomeLines);

            MarkStateDirty();
        }

        private static string SanitizeAiVisibleMemoryLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return string.Empty;

            var text = line.Trim();
            if (text.IndexOf("farm_menu_step result:", StringComparison.OrdinalIgnoreCase) >= 0 &&
                text.IndexOf("failed_mod_micro_step_queue", StringComparison.OrdinalIgnoreCase) >= 0 &&
                (text.IndexOf("waiting_for_native_path", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 text.IndexOf("Follower.GoTo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 text.IndexOf("Follower.StartPath", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 text.IndexOf("Follower.SetDirectPath", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 text.IndexOf("ABPath", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return "farm menu outcome: attempted one visible fertilize unit, but the follower did not reach the fertilizer source before timeout; no farm plot changed.";
            }

            if (text.IndexOf("Farm micro-step native movement started", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Farm micro-step: started walking toward the selected farm target.";

            if (text.IndexOf("waiting_for_native_path", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Farm micro-step: still trying to reach the selected farm target.";

            if (text.IndexOf("Follower.StartPath", StringComparison.OrdinalIgnoreCase) < 0 &&
                text.IndexOf("Follower.SetDirectPath", StringComparison.OrdinalIgnoreCase) < 0 &&
                text.IndexOf("Follower.GoTo", StringComparison.OrdinalIgnoreCase) < 0 &&
                text.IndexOf("ABPath", StringComparison.OrdinalIgnoreCase) < 0 &&
                text.IndexOf("Pathfinding.", StringComparison.OrdinalIgnoreCase) < 0 &&
                text.IndexOf("SetBodyAnimation", StringComparison.OrdinalIgnoreCase) < 0 &&
                text.IndexOf("transform.position", StringComparison.OrdinalIgnoreCase) < 0)
                return text;

            return "Internal movement/executor diagnostic was hidden from AI-facing memory.";
        }

        private static bool IsObsoleteMemoryLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;

            return ContainsAny(line.ToLowerInvariant(),
                "exclusive job",
                "reclaim failed",
                "trait_manipulator reclaim",
                "brew reclaim",
                "faith_enforce",
                "native scheduler-backed personal override");
        }

        private static bool ContainsAny(string normalized, params string[] terms)
        {
            return terms.Any(term => normalized.Contains(term));
        }

    }
}

using System;
using System.Linq;

namespace COTL_AL_NPCs
{
    internal static class FollowerAiFollowerEventClassifier
    {
        internal static bool TryDescribeObservedDeath(FollowerAiFollowerFact follower, string combinedState, out string eventSummary)
        {
            eventSummary = string.Empty;
            if (!ContainsAny(combinedState, "Dead", "Dying", "Death"))
                return false;

            eventSummary = $"{SafeName(follower)} died today. Cause of death was observed as {DescribeCause(combinedState)}.";
            return true;
        }

        internal static bool IsHardGoneEventType(string eventType)
        {
            return string.Equals(eventType, "death", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(eventType, "sin_taken_away", StringComparison.OrdinalIgnoreCase);
        }

        internal static string ExtractNameFromHardEventSummary(string summary)
        {
            if (string.IsNullOrWhiteSpace(summary))
                return string.Empty;

            var marker = summary.IndexOf(" was ", StringComparison.OrdinalIgnoreCase);
            return marker > 0 ? summary.Substring(0, marker).Trim() : string.Empty;
        }

        internal static string SafeName(FollowerAiFollowerFact follower)
        {
            return string.IsNullOrWhiteSpace(follower?.Name) ? $"Follower {follower?.ID ?? -1}" : follower.Name.Trim();
        }

        internal static string SafeName(FollowerInfo followerInfo)
        {
            return string.IsNullOrWhiteSpace(followerInfo?.Name) ? $"Follower {followerInfo?.ID ?? -1}" : followerInfo.Name.Trim();
        }

        internal static FollowerInfo GetFollowerInfo(Follower follower)
        {
            try
            {
                return follower?.Brain?._directInfoAccess;
            }
            catch
            {
                return null;
            }
        }

        internal static string NormalizeName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }

        private static string DescribeCause(string combinedState)
        {
            if (ContainsAny(combinedState, "Sacrifice", "Sacrific"))
                return "sacrifice";
            if (ContainsAny(combinedState, "Murder", "Kill", "Killed"))
                return "being killed";
            if (ContainsAny(combinedState, "OldAge", "Old Age"))
                return "old age";
            if (ContainsAny(combinedState, "Ill", "Sick", "Disease"))
                return "illness";
            if (ContainsAny(combinedState, "Starv", "Hunger"))
                return "starvation";
            return "unknown";
        }

        private static bool ContainsAny(string value, params string[] needles)
        {
            return needles.Any(needle => (value ?? string.Empty).IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}

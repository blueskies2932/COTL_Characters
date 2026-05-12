using System;
using System.Collections.Generic;
using System.Linq;

namespace COTL_AL_NPCs
{
    internal static class FollowerAiCurrentEvents
    {
        private static readonly Dictionary<string, DateTime> recentEventKeys = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<int, string> lastObservedHardEventKeyByFollower = new Dictionary<int, string>();
        private static readonly Dictionary<int, string> lastObservedHardEventTypeByFollower = new Dictionary<int, string>();
        private static readonly Dictionary<string, string> lastObservedHardEventTypeByFollowerName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly ActiveRitualTracker activeRitual = new ActiveRitualTracker();

        internal static void RecordSermon(string source)
        {
            RecordRateLimited("sermon", "sermon", "A sermon was held.", Array.Empty<int>(), TimeSpan.FromSeconds(45));
        }

        internal static void RecordRitual(string source)
        {
            RecordRateLimited("ritual", "ritual", "filler text", Array.Empty<int>(), TimeSpan.FromSeconds(45));
        }

        internal static void RecordRitual(string source, UpgradeSystem.Type ritualType)
        {
            var ritualName = FollowerAiRitualCatalog.GetPlayerFacingName(ritualType);
            var description = FollowerAiRitualCatalog.GetDescription(ritualName);
            var key = RecordRateLimited(
                $"ritual:{ritualName}",
                "ritual",
                $"{ritualName}: {description}",
                Array.Empty<int>(),
                TimeSpan.FromSeconds(45));

            if (!string.IsNullOrWhiteSpace(key))
                activeRitual.Begin(key, ritualName, description);
        }

        internal static void NoteFollowerSelectionForRitual(UpgradeSystem.Type ritualType, FollowerInfo followerInfo)
        {
            if (followerInfo == null || !activeRitual.HasActiveEvent)
                return;

            var ritualName = FollowerAiRitualCatalog.GetPlayerFacingName(ritualType);
            if (!activeRitual.IsForRitual(ritualName))
                return;

            if (activeRitual.TryAddSelectedFollower(FollowerAiFollowerEventClassifier.SafeName(followerInfo), out var summary))
                FollowerAiSocialMemory.SetEventSummary(activeRitual.EventKey, summary);
        }

        internal static void ObserveFollowers(IEnumerable<FollowerAiFollowerFact> followers)
        {
            foreach (var follower in followers ?? Enumerable.Empty<FollowerAiFollowerFact>())
            {
                if (follower == null || follower.ID < 0)
                    continue;

                var combinedState = $"{follower.CurrentTask} {follower.CurrentOverrideTask} {follower.CurrentState} {follower.AvailabilityStatus}";
                if (FollowerAiFollowerEventClassifier.TryDescribeObservedDeath(follower, combinedState, out var deathSummary))
                    RecordFollowerHardEvent(follower.ID, "death", deathSummary);
            }
        }

        internal static string BuildPromptContext(int maxLines = 8, bool includeTournament = true)
        {
            var lines = FollowerAiSocialMemory.GetRecentDayLines(dayWindow: 3, maxLines: maxLines);
            if (includeTournament)
            {
                var tournament = BuildTournamentPromptContext();
                if (!string.IsNullOrWhiteSpace(tournament))
                    lines.Add(tournament);
            }

            return lines.Count == 0 ? string.Empty : string.Join("\n", lines);
        }

        internal static string BuildTournamentPromptContext()
        {
            var lines = new List<string>();
            var tournamentLineup = FollowerAiTournamentLedger.BuildCurrentMatchPromptContext();
            if (!string.IsNullOrWhiteSpace(tournamentLineup))
                lines.Add(tournamentLineup);

            var tournamentChampion = FollowerAiTournamentLedger.BuildChampionPromptContext();
            if (!string.IsNullOrWhiteSpace(tournamentChampion))
                lines.Add(tournamentChampion);

            return string.Join("\n", lines);
        }

        internal static bool HasObservedHardGoneEvent(int followerID, string followerName)
        {
            if (followerID >= 0 &&
                lastObservedHardEventTypeByFollower.TryGetValue(followerID, out var byID) &&
                FollowerAiFollowerEventClassifier.IsHardGoneEventType(byID))
            {
                return true;
            }

            var normalizedName = FollowerAiFollowerEventClassifier.NormalizeName(followerName);
            return !string.IsNullOrWhiteSpace(normalizedName) &&
                   lastObservedHardEventTypeByFollowerName.TryGetValue(normalizedName, out var byName) &&
                   FollowerAiFollowerEventClassifier.IsHardGoneEventType(byName);
        }

        internal static void RecordResurrection(int followerID, string source)
        {
            if (followerID < 0)
                return;

            ClearHardGoneEvent(followerID, string.Empty);
            FollowerAiTournamentLedger.MarkEntrantAlive(followerID, string.Empty, source);
            FollowerAiSocialMemory.RecordEvent(
                $"resurrection:{SafeCurrentDay()}:{followerID}:{DateTime.UtcNow.Ticks}",
                "resurrection",
                $"Follower {followerID} was resurrected.",
                new[] { followerID });
        }

        internal static void RecordResurrection(Follower follower, string source)
        {
            var info = FollowerAiFollowerEventClassifier.GetFollowerInfo(follower);
            if (info == null)
                return;

            var name = FollowerAiFollowerEventClassifier.SafeName(info);
            ClearHardGoneEvent(info.ID, name);
            FollowerAiTournamentLedger.MarkEntrantAlive(info.ID, name, source);
            FollowerAiSocialMemory.RecordEvent(
                $"resurrection:{SafeCurrentDay()}:{info.ID}:{DateTime.UtcNow.Ticks}",
                "resurrection",
                $"{name} was resurrected.",
                new[] { info.ID });
        }

        internal static void RecordSinTakenAway(Follower follower, string source)
        {
            var info = FollowerAiFollowerEventClassifier.GetFollowerInfo(follower);
            if (info == null)
                return;

            var name = FollowerAiFollowerEventClassifier.SafeName(info);
            RecordFollowerHardEvent(info.ID, "sin_taken_away", $"{name} was possessed by Sin and dragged to hell.", "Possessed by Sin");
            FollowerAiTournamentLedger.MarkEntrantDead(info.ID, name, source);
        }

        internal static void RecordDeath(Follower follower, string source)
        {
            var info = FollowerAiFollowerEventClassifier.GetFollowerInfo(follower);
            if (info == null)
                return;

            var name = FollowerAiFollowerEventClassifier.SafeName(info);
            RecordFollowerHardEvent(info.ID, "death", $"{name} died today. Cause of death was observed as {source}.");
            FollowerAiTournamentLedger.MarkEntrantDead(info.ID, name, source);
        }

        internal static void RecordDeath(int followerID, string source)
        {
            if (followerID < 0)
                return;

            RecordFollowerHardEvent(followerID, "death", $"Follower {followerID} died today. Cause of death was observed as {source}.");
            FollowerAiTournamentLedger.MarkEntrantDead(followerID, string.Empty, source);
        }

        private static void RecordFollowerHardEvent(int followerID, string eventType, string summary, string modelFacingEventType = null)
        {
            var key = $"{eventType}:{SafeCurrentDay()}:{followerID}";
            if (lastObservedHardEventKeyByFollower.TryGetValue(followerID, out var previous) &&
                string.Equals(previous, key, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            lastObservedHardEventKeyByFollower[followerID] = key;
            lastObservedHardEventTypeByFollower[followerID] = eventType;

            var name = FollowerAiFollowerEventClassifier.ExtractNameFromHardEventSummary(summary);
            if (!string.IsNullOrWhiteSpace(name))
                lastObservedHardEventTypeByFollowerName[FollowerAiFollowerEventClassifier.NormalizeName(name)] = eventType;

            FollowerAiSocialMemory.RecordEvent(
                key,
                string.IsNullOrWhiteSpace(modelFacingEventType) ? eventType : modelFacingEventType,
                summary,
                Array.Empty<int>());
        }

        private static string RecordRateLimited(string keyPrefix, string eventType, string summary, IEnumerable<int> participantIDs, TimeSpan cooldown)
        {
            var key = $"{keyPrefix}:{SafeCurrentDay()}";
            var now = DateTime.UtcNow;
            if (recentEventKeys.TryGetValue(key, out var last) && now - last < cooldown)
                return string.Empty;

            recentEventKeys[key] = now;
            var eventKey = $"{key}:{now.Ticks}";
            return FollowerAiSocialMemory.RecordEvent(eventKey, eventType, summary, participantIDs)
                ? eventKey
                : string.Empty;
        }

        private static void ClearHardGoneEvent(int followerID, string followerName)
        {
            if (followerID >= 0)
                lastObservedHardEventTypeByFollower.Remove(followerID);

            var normalizedName = FollowerAiFollowerEventClassifier.NormalizeName(followerName);
            if (!string.IsNullOrWhiteSpace(normalizedName))
                lastObservedHardEventTypeByFollowerName.Remove(normalizedName);
        }

        private static int SafeCurrentDay()
        {
            try
            {
                return TimeManager.CurrentDay;
            }
            catch
            {
                return DateTime.Now.DayOfYear;
            }
        }

        private sealed class ActiveRitualTracker
        {
            private readonly List<string> selectedFollowers = new List<string>();
            private string ritualName = string.Empty;
            private string ritualDescription = string.Empty;

            public string EventKey { get; private set; } = string.Empty;
            public bool HasActiveEvent => !string.IsNullOrWhiteSpace(EventKey);

            public void Begin(string eventKey, string name, string description)
            {
                EventKey = eventKey ?? string.Empty;
                ritualName = name ?? string.Empty;
                ritualDescription = description ?? string.Empty;
                selectedFollowers.Clear();
            }

            public bool IsForRitual(string name)
            {
                return string.Equals(name, ritualName, StringComparison.OrdinalIgnoreCase);
            }

            public bool TryAddSelectedFollower(string name, out string summary)
            {
                summary = string.Empty;
                if (string.IsNullOrWhiteSpace(name) ||
                    selectedFollowers.Any(item => string.Equals(item, name, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }

                selectedFollowers.Add(name.Trim());
                var selected = selectedFollowers.Count == 1
                    ? $" Selected follower: {selectedFollowers[0]}."
                    : $" Selected followers: {string.Join(", ", selectedFollowers)}.";
                summary = $"{ritualName}: {ritualDescription}{selected}";
                return true;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace COTL_AL_NPCs
{
    internal static partial class FollowerAiTournamentLedger
    {
        internal static List<FollowerAiFollowerFact> GetAvailableFollowerChoices()
        {
            EnsureLoaded();
            var selectedIDs = new HashSet<int>((state?.Draft?.Entrants ?? new List<FollowerAiTournamentEntrant>())
                .Where(entrant => entrant != null && entrant.FollowerID > 0)
                .Select(entrant => entrant.FollowerID));
            var selectedNames = new HashSet<string>((state?.Draft?.Entrants ?? new List<FollowerAiTournamentEntrant>())
                .Where(entrant => entrant != null && !string.IsNullOrWhiteSpace(entrant.Name))
                .Select(entrant => Normalize(entrant.Name)));

            return FollowerAiFollowerFacts.GetCurrentFollowers()
                .Where(fact => fact != null &&
                               !fact.IsAiNpc &&
                               fact.AvailabilityStatus == Lamb.UI.FollowerSelect.FollowerSelectEntry.Status.Available &&
                               !selectedIDs.Contains(fact.ID) &&
                               !selectedNames.Contains(Normalize(fact.Name)))
                .OrderBy(fact => fact.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(fact => fact.ID)
                .ToList();
        }

        internal static int ReconcileLiveFollowerStatuses(bool saveIfChanged)
        {
            EnsureLoadedNoReconcile();
            FollowerAiCurrentEvents.ObserveFollowers(FollowerAiFollowerFacts.GetCurrentFollowers());
            var facts = FollowerAiFollowerFacts.GetCurrentFollowers();
            var byID = facts
                .Where(fact => fact != null && fact.ID > 0)
                .GroupBy(fact => fact.ID)
                .ToDictionary(group => group.Key, group => group.First());
            var byName = facts
                .Where(fact => fact != null && !string.IsNullOrWhiteSpace(fact.Name))
                .GroupBy(fact => Normalize(fact.Name))
                .ToDictionary(group => group.Key, group => group.First());

            var changed = 0;
            foreach (var entrant in state.Draft.Entrants)
            {
                if (entrant == null || (entrant.FollowerID <= 0 && string.IsNullOrWhiteSpace(entrant.Name)))
                    continue;

                FollowerAiFollowerFact fact = null;
                if (entrant.FollowerID > 0)
                    byID.TryGetValue(entrant.FollowerID, out fact);
                if (fact == null && !string.IsNullOrWhiteSpace(entrant.Name))
                    byName.TryGetValue(Normalize(entrant.Name), out fact);

                if (fact == null)
                {
                    if (SetStatus(entrant, "Dead"))
                        changed++;
                    continue;
                }

                if (entrant.FollowerID <= 0)
                    entrant.FollowerID = fact.ID;
                if (!string.Equals(entrant.Name, fact.Name, StringComparison.Ordinal))
                    entrant.Name = fact.Name;

                if (SetStatus(entrant, BuildStatus(fact, entrant.Status)))
                    changed++;
            }

            if (changed > 0 && saveIfChanged)
                Save();

            return changed;
        }

        internal static bool MarkEntrantDead(int followerID, string followerName, string source)
        {
            if (followerID < 0 && string.IsNullOrWhiteSpace(followerName))
                return false;

            EnsureLoadedNoReconcile();
            EnsureDraftShape(state.Draft);

            var changed = SetMatchingEntrantsStatus(followerID, followerName, "Dead");
            if (changed)
            {
                Save();
                AICharacterPlugin.Log?.LogInfo($"Tournament entrant marked dead: follower={followerID}; name={followerName}; source={source}.");
            }

            return changed;
        }

        internal static bool MarkEntrantAlive(int followerID, string followerName, string source)
        {
            if (followerID < 0 && string.IsNullOrWhiteSpace(followerName))
                return false;

            EnsureLoadedNoReconcile();
            EnsureDraftShape(state.Draft);

            var changed = SetMatchingEntrantsStatus(followerID, followerName, "Alive");
            if (changed)
            {
                Save();
                AICharacterPlugin.Log?.LogInfo($"Tournament entrant marked alive: follower={followerID}; name={followerName}; source={source}.");
            }

            return changed;
        }

        private static bool SetMatchingEntrantsStatus(int followerID, string followerName, string status)
        {
            var normalizedName = Normalize(followerName);
            var changed = false;
            foreach (var entrant in state.Draft.Entrants)
            {
                if (entrant == null)
                    continue;

                var idMatches = followerID >= 0 && entrant.FollowerID == followerID;
                var nameMatches = !string.IsNullOrWhiteSpace(normalizedName) &&
                                  string.Equals(Normalize(entrant.Name), normalizedName, StringComparison.OrdinalIgnoreCase);
                if (!idMatches && !nameMatches)
                    continue;

                if (SetStatus(entrant, status))
                    changed = true;
            }

            return changed;
        }

        private static string BuildStatus(FollowerAiFollowerFact fact, string currentStatus)
        {
            if (fact == null)
                return string.IsNullOrWhiteSpace(currentStatus) ? "Alive" : currentStatus;

            var stateText = fact.CurrentState.ToString();
            var taskText = fact.CurrentTask.ToString();
            var overrideTaskText = fact.CurrentOverrideTask.ToString();
            var combined = $"{stateText} {taskText} {overrideTaskText} {fact.AvailabilityStatus} {fact.CursedState} {fact.Location} {fact.DesiredLocation}";
            if (IsTemporaryTournamentHold(combined))
                return "Alive";

            if (FollowerAiCurrentEvents.HasObservedHardGoneEvent(fact.ID, fact.Name))
                return "Dead";

            if (IsTournamentDeadOrGoneText(combined))
                return "Dead";

            return fact.AvailabilityStatus == Lamb.UI.FollowerSelect.FollowerSelectEntry.Status.Available
                ? "Alive"
                : string.IsNullOrWhiteSpace(currentStatus) ? "Alive" : currentStatus;
        }

        private static bool IsTemporaryTournamentHold(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return ContainsAny(value,
                "TraitManipulat",
                "Trait Manipulat",
                "Confession",
                "Prison",
                "Imprison",
                "Jail",
                "Stocks",
                "Rack",
                "Ritual",
                "Sermon",
                "Church",
                "Ceremony");
        }

        private static bool IsTournamentDeadOrGoneText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return ContainsAny(value,
                "Dead",
                "Dying",
                "Death",
                "Possessed",
                "Damned",
                "Dragged",
                "Hell",
                "LeftCult",
                "Left Cult");
        }

        private static bool ContainsAny(string value, params string[] needles)
        {
            return needles.Any(needle => value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool SetStatus(FollowerAiTournamentEntrant entrant, string status)
        {
            status = string.IsNullOrWhiteSpace(status) ? "Alive" : status;
            if (string.Equals(entrant.Status, status, StringComparison.Ordinal))
                return false;

            entrant.Status = status;
            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace COTL_AL_NPCs
{
    internal static partial class FollowerAiTournamentLedger
    {
        internal static FollowerAiTournamentCurrentMatch GetCurrentMatch()
        {
            EnsureLoaded();
            var matches = state?.Draft?.Matches ?? new List<FollowerAiTournamentMatch>();
            for (var i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                if (match == null || !string.IsNullOrWhiteSpace(match.Winner))
                    continue;

                return new FollowerAiTournamentCurrentMatch
                {
                    Index = i + 1,
                    Round = match.Round ?? string.Empty,
                    A = match.A ?? string.Empty,
                    B = match.B ?? string.Empty,
                    ARoll = match.ARoll ?? string.Empty,
                    BRoll = match.BRoll ?? string.Empty
                };
            }

            return null;
        }

        internal static string BuildCurrentMatchPromptContext()
        {
            var current = GetCurrentMatch();
            if (current == null)
                return string.Empty;

            var leftName = string.IsNullOrWhiteSpace(current.A) ? "TBD" : current.A.Trim();
            var rightName = string.IsNullOrWhiteSpace(current.B) ? "TBD" : current.B.Trim();
            if (string.Equals(leftName, "TBD", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(rightName, "TBD", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            var round = string.IsNullOrWhiteSpace(current.Round)
                ? $"Match {current.Index}"
                : $"{current.Round} Match {current.Index}";
            var facts = FollowerAiFollowerFacts.GetCurrentFollowers();
            var left = FormatTournamentContestantForPrompt(leftName, facts);
            var right = FormatTournamentContestantForPrompt(rightName, facts);

            return $"day={SafeCurrentDay()} type=Tournament Upcoming Match: {round}: {left} vs {right}.";
        }

        internal static string BuildChampionPromptContext()
        {
            EnsureLoaded();
            EnsureDraftShape(state.Draft);
            UpdateChampionCurrentEventFromFinal();

            var champion = state.Draft.Champion;
            if (champion == null ||
                string.IsNullOrWhiteSpace(champion.CurrentEventWinner) ||
                champion.CurrentEventDecidedDay < 0)
            {
                return string.Empty;
            }

            var currentDay = SafeCurrentDay();
            if (champion.CurrentEventDecidedDay < currentDay - 2 || champion.CurrentEventDecidedDay > currentDay)
                return string.Empty;

            var facts = FollowerAiFollowerFacts.GetCurrentFollowers();
            var winner = FormatTournamentContestantForPrompt(champion.CurrentEventWinner, facts);
            return $"day={champion.CurrentEventDecidedDay} type=Tournament Champion: {winner} won the tournament.";
        }

        private static string FormatTournamentContestantForPrompt(string name, List<FollowerAiFollowerFact> facts)
        {
            if (string.IsNullOrWhiteSpace(name) || string.Equals(name, "TBD", StringComparison.OrdinalIgnoreCase))
                return "TBD";

            var fact = (facts ?? new List<FollowerAiFollowerFact>())
                .FirstOrDefault(item => item != null && string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
            if (fact == null)
                return $"{name} (level=unknown; personal_traits=unknown)";

            var traits = fact.Traits == null || fact.Traits.Count == 0
                ? "none"
                : string.Join(", ", fact.Traits
                    .Select(trait => string.IsNullOrWhiteSpace(trait.Title) ? trait.Name : trait.Title)
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
            if (string.IsNullOrWhiteSpace(traits))
                traits = "none";

            return $"{fact.Name} (level={fact.Level}; personal_traits=[{traits}])";
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
    }
}

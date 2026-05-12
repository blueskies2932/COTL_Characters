using System;
using System.Collections.Generic;
using System.Linq;

namespace COTL_AL_NPCs
{
    internal static partial class FollowerAiTournamentLedger
    {
        internal static void AddTenFollowerBracketTemplate()
        {
            EnsureLoaded();
            EnsureDraftShape(state.Draft);

            var matches = new List<FollowerAiTournamentMatch>
            {
                BuildTemplateMatch("Play-in 1", "Seed 7 vs Seed 10", "7", "10"),
                BuildTemplateMatch("Play-in 2", "Seed 8 vs Seed 9", "8", "9"),
                BuildTemplateMatch("Quarterfinal 1", "Seed 1 vs Play-in 2 winner", "1", string.Empty),
                BuildTemplateMatch("Quarterfinal 2", "Seed 4 vs Seed 5", "4", "5"),
                BuildTemplateMatch("Quarterfinal 3", "Seed 3 vs Seed 6", "3", "6"),
                BuildTemplateMatch("Quarterfinal 4", "Seed 2 vs Play-in 1 winner", "2", string.Empty),
                BuildTemplateMatch("Semifinal 1", string.Empty, string.Empty, string.Empty),
                BuildTemplateMatch("Semifinal 2", string.Empty, string.Empty, string.Empty),
                BuildTemplateMatch("Final", string.Empty, string.Empty, string.Empty)
            };

            state.Draft.Matches.AddRange(matches);
            Save();
        }

        internal static int ApplyEntrantsToBracketTemplate()
        {
            EnsureLoaded();
            EnsureDraftShape(state.Draft);

            var changed = 0;
            changed += ApplyBracketMatch("Play-in 1", "7", "10");
            changed += ApplyBracketMatch("Play-in 2", "8", "9");
            changed += ApplyBracketMatch("Quarterfinal 1", "1", string.Empty);
            changed += ApplyBracketMatch("Quarterfinal 2", "4", "5");
            changed += ApplyBracketMatch("Quarterfinal 3", "3", "6");
            changed += ApplyBracketMatch("Quarterfinal 4", "2", string.Empty);

            if (changed > 0)
                Save();

            return changed;
        }

        internal static bool ApplyRollOutcome(FollowerAiTournamentMatch match)
        {
            if (match == null ||
                string.IsNullOrWhiteSpace(match.A) ||
                string.IsNullOrWhiteSpace(match.B) ||
                !TryParseRoll(match.ARoll, out var aRoll) ||
                !TryParseRoll(match.BRoll, out var bRoll) ||
                aRoll == bRoll)
            {
                return false;
            }

            var winner = aRoll > bRoll ? match.A : match.B;
            var badTarget = aRoll > bRoll ? match.B : match.A;
            return ApplyOutcome(match, winner, badTarget);
        }

        internal static bool ApplyManualOutcome(FollowerAiTournamentMatch match, string winner)
        {
            if (match == null ||
                string.IsNullOrWhiteSpace(match.A) ||
                string.IsNullOrWhiteSpace(match.B) ||
                string.IsNullOrWhiteSpace(winner))
            {
                return false;
            }

            var normalizedWinner = Normalize(winner);
            if (string.Equals(normalizedWinner, Normalize(match.A), StringComparison.OrdinalIgnoreCase))
                return ApplyOutcome(match, match.A, match.B);

            if (string.Equals(normalizedWinner, Normalize(match.B), StringComparison.OrdinalIgnoreCase))
                return ApplyOutcome(match, match.B, match.A);

            return false;
        }

        internal static bool MatchHasDecisiveRolls(FollowerAiTournamentMatch match)
        {
            return match != null &&
                   TryParseRoll(match.ARoll, out var aRoll) &&
                   TryParseRoll(match.BRoll, out var bRoll) &&
                   aRoll != bRoll;
        }

        internal static bool ApplyWinnerPropagation(bool saveIfChanged)
        {
            EnsureLoaded();
            EnsureDraftShape(state.Draft);

            var changed = 0;
            changed += ApplyWinnerToBracketSide("Play-in 2", "Quarterfinal 1", sideA: false);
            changed += ApplyWinnerToBracketSide("Play-in 1", "Quarterfinal 4", sideA: false);
            changed += ApplyWinnerToBracketSide("Quarterfinal 1", "Semifinal 1", sideA: true);
            changed += ApplyWinnerToBracketSide("Quarterfinal 2", "Semifinal 1", sideA: false);
            changed += ApplyWinnerToBracketSide("Quarterfinal 3", "Semifinal 2", sideA: true);
            changed += ApplyWinnerToBracketSide("Quarterfinal 4", "Semifinal 2", sideA: false);
            changed += ApplyWinnerToBracketSide("Semifinal 1", "Final", sideA: true);
            changed += ApplyWinnerToBracketSide("Semifinal 2", "Final", sideA: false);
            changed += UpdateChampionCurrentEventFromFinal();

            if (changed > 0 && saveIfChanged)
                Save();

            return changed > 0;
        }

        private static bool ApplyOutcome(FollowerAiTournamentMatch match, string winner, string badTarget)
        {
            var changed = false;
            if (!string.Equals(match.Winner ?? string.Empty, winner, StringComparison.Ordinal))
            {
                match.Winner = winner;
                changed = true;
            }

            if (!string.Equals(match.BadTarget ?? string.Empty, badTarget, StringComparison.Ordinal))
            {
                match.BadTarget = badTarget;
                changed = true;
            }

            if (changed)
            {
                ApplyWinnerPropagation(saveIfChanged: false);
                UpdateChampionCurrentEventFromFinal();
                Save();
            }

            return changed;
        }

        private static FollowerAiTournamentMatch BuildTemplateMatch(string round, string notes, string seedA, string seedB)
        {
            return new FollowerAiTournamentMatch
            {
                Round = round,
                A = FindEntrantNameBySeed(seedA),
                B = FindEntrantNameBySeed(seedB),
                Notes = notes
            };
        }

        private static bool TryParseRoll(string value, out int roll)
        {
            return int.TryParse((value ?? string.Empty).Trim(), out roll);
        }

        private static string FindEntrantNameBySeed(string seed)
        {
            if (string.IsNullOrWhiteSpace(seed))
                return string.Empty;

            var normalized = Normalize(seed);
            var entrant = state?.Draft?.Entrants?
                .FirstOrDefault(item => item != null && Normalize(item.Seed) == normalized);
            if (entrant != null && !string.IsNullOrWhiteSpace(entrant.Name))
                return entrant.Name;

            if (int.TryParse(seed, out var slot))
            {
                entrant = state?.Draft?.Entrants?
                    .FirstOrDefault(item => item != null && item.Slot == slot);
                if (entrant != null && !string.IsNullOrWhiteSpace(entrant.Name))
                    return entrant.Name;
            }

            return string.Empty;
        }

        private static int ApplyBracketMatch(string round, string seedA, string seedB)
        {
            var match = state?.Draft?.Matches?
                .FirstOrDefault(item => item != null && string.Equals(item.Round, round, StringComparison.OrdinalIgnoreCase));
            if (match == null)
                return 0;

            var changed = 0;
            if (ApplyBracketSide(value => match.A = value, match.A, seedA))
                changed++;
            if (ApplyBracketSide(value => match.B = value, match.B, seedB))
                changed++;

            return changed;
        }

        private static int ApplyWinnerToBracketSide(string sourceRound, string targetRound, bool sideA)
        {
            var source = FindMatchByRound(sourceRound);
            var target = FindMatchByRound(targetRound);
            if (source == null || target == null || string.IsNullOrWhiteSpace(source.Winner))
                return 0;

            var currentValue = sideA ? target.A : target.B;
            if (string.Equals(currentValue ?? string.Empty, source.Winner, StringComparison.Ordinal))
                return 0;

            if (sideA)
                target.A = source.Winner;
            else
                target.B = source.Winner;

            return 1;
        }

        private static int UpdateChampionCurrentEventFromFinal()
        {
            EnsureDraftShape(state.Draft);
            var final = FindMatchByRound("Final");
            var champion = state.Draft.Champion;
            var winner = final?.Winner?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(winner))
            {
                if (string.IsNullOrWhiteSpace(champion.CurrentEventWinner) &&
                    champion.CurrentEventDecidedDay < 0)
                {
                    return 0;
                }

                champion.CurrentEventWinner = string.Empty;
                champion.CurrentEventDecidedDay = -1;
                return 1;
            }

            var changed = 0;
            if (!string.Equals(champion.CurrentEventWinner ?? string.Empty, winner, StringComparison.Ordinal))
            {
                champion.CurrentEventWinner = winner;
                champion.CurrentEventDecidedDay = SafeCurrentDay();
                changed++;
            }
            else if (champion.CurrentEventDecidedDay < 0)
            {
                champion.CurrentEventDecidedDay = SafeCurrentDay();
                changed++;
            }

            if (string.IsNullOrWhiteSpace(champion.WinnerOriginal))
            {
                champion.WinnerOriginal = winner;
                changed++;
            }

            return changed;
        }

        private static FollowerAiTournamentMatch FindMatchByRound(string round)
        {
            if (string.IsNullOrWhiteSpace(round))
                return null;

            return state?.Draft?.Matches?
                .FirstOrDefault(item => item != null && string.Equals(item.Round, round, StringComparison.OrdinalIgnoreCase));
        }

        private static bool ApplyBracketSide(Action<string> setter, string currentValue, string seed)
        {
            if (string.IsNullOrWhiteSpace(seed))
                return false;

            var entrantName = FindEntrantNameBySeed(seed);
            if (string.IsNullOrWhiteSpace(entrantName) || string.Equals(currentValue, entrantName, StringComparison.Ordinal))
                return false;

            setter(entrantName);
            return true;
        }
    }
}

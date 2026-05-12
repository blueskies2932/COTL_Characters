using System;
using System.Linq;

namespace COTL_AL_NPCs
{
    internal static partial class FollowerAiTournamentLedger
    {
        internal static bool ArchiveCurrentDraft(out string message)
        {
            message = string.Empty;
            EnsureLoaded();
            var draft = state.Draft;
            EnsureDraftShape(draft);
            ApplyWinnerPropagation(saveIfChanged: false);
            var final = FindMatchByRound("Final");
            var finalWinner = final?.Winner?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(finalWinner))
            {
                message = "Archive blocked: decide the Final match winner first.";
                return false;
            }

            if (draft.Champion == null)
                draft.Champion = new FollowerAiTournamentChampion();
            if (string.IsNullOrWhiteSpace(draft.Champion.WinnerOriginal))
                draft.Champion.WinnerOriginal = finalWinner;
            if (string.IsNullOrWhiteSpace(draft.Champion.WinnerName))
                draft.Champion.WinnerName = finalWinner;

            state.Archive.Add(new FollowerAiTournamentArchiveEntry
            {
                ID = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow,
                TournamentName = draft.TournamentName,
                TournamentDate = draft.TournamentDate,
                TournamentTheme = draft.TournamentTheme,
                SinEarned = draft.SinEarned,
                SpecialRewards = draft.SpecialRewards,
                TournamentNotes = draft.TournamentNotes,
                Entrants = draft.Entrants.Select(CloneEntrant).ToList(),
                Matches = draft.Matches.Select(CloneMatch).ToList(),
                Champion = CloneChampion(draft.Champion)
            });

            state.Draft = new FollowerAiTournamentDraft();
            EnsureDraftShape(state.Draft);
            Save();
            message = $"Archived tournament champion {finalWinner}. Started a fresh draft.";
            return true;
        }

        private static FollowerAiTournamentEntrant CloneEntrant(FollowerAiTournamentEntrant entrant)
        {
            return new FollowerAiTournamentEntrant
            {
                Slot = entrant?.Slot ?? 0,
                FollowerID = entrant?.FollowerID ?? 0,
                Name = entrant?.Name ?? string.Empty,
                Seed = entrant?.Seed ?? string.Empty,
                Status = entrant?.Status ?? "Alive",
                Notes = entrant?.Notes ?? string.Empty
            };
        }

        private static FollowerAiTournamentMatch CloneMatch(FollowerAiTournamentMatch match)
        {
            return new FollowerAiTournamentMatch
            {
                Round = match?.Round ?? string.Empty,
                A = match?.A ?? string.Empty,
                ARoll = match?.ARoll ?? string.Empty,
                B = match?.B ?? string.Empty,
                BRoll = match?.BRoll ?? string.Empty,
                Winner = match?.Winner ?? string.Empty,
                BadTarget = match?.BadTarget ?? string.Empty,
                BadThing = match?.BadThing ?? string.Empty,
                Notes = match?.Notes ?? string.Empty
            };
        }

        private static FollowerAiTournamentChampion CloneChampion(FollowerAiTournamentChampion champion)
        {
            return new FollowerAiTournamentChampion
            {
                WinnerOriginal = champion?.WinnerOriginal ?? string.Empty,
                WinnerName = champion?.WinnerName ?? string.Empty,
                WinnerTitle = champion?.WinnerTitle ?? string.Empty,
                WinnerRole = champion?.WinnerRole ?? string.Empty,
                WinnerJob = champion?.WinnerJob ?? string.Empty,
                AvatarNotes = champion?.AvatarNotes ?? string.Empty,
                ChampionRewards = champion?.ChampionRewards ?? string.Empty,
                CurrentEventWinner = champion?.CurrentEventWinner ?? string.Empty,
                CurrentEventDecidedDay = champion?.CurrentEventDecidedDay ?? -1
            };
        }
    }
}

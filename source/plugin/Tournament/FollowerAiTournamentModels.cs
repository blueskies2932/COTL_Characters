using System;
using System.Collections.Generic;

namespace COTL_AL_NPCs
{
    internal sealed class FollowerAiTournamentState
    {
        public FollowerAiTournamentDraft Draft = new FollowerAiTournamentDraft();
        public List<FollowerAiTournamentArchiveEntry> Archive = new List<FollowerAiTournamentArchiveEntry>();
    }

    internal class FollowerAiTournamentDraft
    {
        public string TournamentName = string.Empty;
        public string TournamentDate = string.Empty;
        public string TournamentTheme = string.Empty;
        public int SinEarned;
        public string SpecialRewards = string.Empty;
        public string TournamentNotes = string.Empty;
        public List<FollowerAiTournamentEntrant> Entrants = new List<FollowerAiTournamentEntrant>();
        public List<FollowerAiTournamentMatch> Matches = new List<FollowerAiTournamentMatch>();
        public FollowerAiTournamentChampion Champion = new FollowerAiTournamentChampion();
    }

    internal sealed class FollowerAiTournamentEntrant
    {
        public int Slot;
        public int FollowerID;
        public string Name = string.Empty;
        public string Seed = string.Empty;
        public string Status = "Alive";
        public string Notes = string.Empty;
    }

    internal sealed class FollowerAiTournamentMatch
    {
        public string Round = string.Empty;
        public string A = string.Empty;
        public string ARoll = string.Empty;
        public string B = string.Empty;
        public string BRoll = string.Empty;
        public string Winner = string.Empty;
        public string BadTarget = string.Empty;
        public string BadThing = string.Empty;
        public string Notes = string.Empty;
    }

    internal sealed class FollowerAiTournamentCurrentMatch
    {
        public int Index;
        public string Round = string.Empty;
        public string A = string.Empty;
        public string B = string.Empty;
        public string ARoll = string.Empty;
        public string BRoll = string.Empty;
    }

    internal sealed class FollowerAiTournamentChampion
    {
        public string WinnerOriginal = string.Empty;
        public string WinnerName = string.Empty;
        public string WinnerTitle = string.Empty;
        public string WinnerRole = string.Empty;
        public string WinnerJob = string.Empty;
        public string AvatarNotes = string.Empty;
        public string ChampionRewards = string.Empty;
        public string CurrentEventWinner = string.Empty;
        public int CurrentEventDecidedDay = -1;
    }

    internal sealed class FollowerAiTournamentArchiveEntry : FollowerAiTournamentDraft
    {
        public string ID = string.Empty;
        public DateTime CreatedAt;
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lamb.UI.FollowerSelect;

namespace COTL_AL_NPCs
{
    internal static partial class FollowerAiFollowerFacts
    {
        private static readonly object Sync = new object();
        private static List<FollowerAiFollowerFact> latestNativeMenuFacts = new List<FollowerAiFollowerFact>();
        private static float nextRequestFileCheckRealtime;

        internal static void CaptureNativeFollowerMenuEntries(List<FollowerSelectEntry> followerSelectEntries)
        {
            var facts = BuildFactsFromNativeMenuEntries(followerSelectEntries);
            lock (Sync)
                latestNativeMenuFacts = facts;

        }

        internal static void Update()
        {
            if (AICharacterPlugin.FollowerFactsDumpRequest?.Value == true)
            {
                AICharacterPlugin.FollowerFactsDumpRequest.Value = false;
                WriteLiveReport("BepInEx config FollowerFactsDumpRequest");
            }

            UpdateRequestFile();
        }

        internal static string GetReportRequestPath()
        {
            return Path.Combine(BepInEx.Paths.ConfigPath, "COTL_AL_NPCs", "RequestCurrentFollowerFactsReport.txt");
        }

        internal static List<FollowerAiFollowerFact> GetLatestNativeMenuFollowers()
        {
            lock (Sync)
                return latestNativeMenuFacts.Select(CloneFact).ToList();
        }

        internal static List<FollowerAiFollowerFact> GetCurrentFollowers()
        {
            var latest = GetLatestNativeMenuFollowers();
            return latest.Count > 0 ? latest : BuildFactsFromLiveFollowers();
        }

        internal static List<FollowerAiFollowerFact> BuildFactsFromNativeMenuEntries(List<FollowerSelectEntry> followerSelectEntries)
        {
            return OrderedUniqueFacts((followerSelectEntries ?? new List<FollowerSelectEntry>())
                .Select(entry => TryBuildFact(entry, out var fact) ? fact : null));
        }

        internal static List<FollowerAiFollowerFact> BuildFactsFromLiveFollowers()
        {
            return OrderedUniqueFacts((Follower.Followers ?? new List<Follower>())
                .Select(follower => TryBuildFact(follower, null, out var fact) ? fact : null));
        }

        private static List<FollowerAiFollowerFact> OrderedUniqueFacts(IEnumerable<FollowerAiFollowerFact> facts)
        {
            var seen = new HashSet<int>();
            return (facts ?? Enumerable.Empty<FollowerAiFollowerFact>())
                .Where(fact => fact != null && seen.Add(fact.ID))
                .OrderBy(fact => fact.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(fact => fact.ID)
                .ToList();
        }

        private static bool TryBuildFact(FollowerSelectEntry entry, out FollowerAiFollowerFact fact)
        {
            fact = null;
            var info = entry?.FollowerInfo;
            if (info == null)
                return false;

            var follower = FollowerManager.FindFollowerByID(info.ID);
            return TryBuildFact(follower, entry, out fact, info);
        }

        private static bool TryBuildFact(Follower follower, FollowerSelectEntry entry, out FollowerAiFollowerFact fact, FollowerInfo fallbackInfo = null)
        {
            fact = null;
            var brain = follower?.Brain;
            var info = brain?._directInfoAccess ?? fallbackInfo;
            var brainInfo = brain?.Info;
            var stats = brain?.Stats;
            if (info == null || brain?.LeftCult == true)
                return false;

            fact = new FollowerAiFollowerFact
            {
                ID = info.ID,
                Name = SafeText(info.Name),
                Role = info.FollowerRole,
                Location = info.Location,
                DesiredLocation = brain?.DesiredLocation ?? info.Location,
                AvailabilityStatus = entry?.AvailabilityStatus ?? GetAvailabilityStatus(info),
                CurrentState = brain?.CurrentState ?? default,
                CurrentTask = brain?.CurrentTaskType ?? default,
                CurrentOverrideTask = brain?.CurrentOverrideTaskType ?? default,
                Age = info.Age,
                MemberDays = SafeMemberDays(info),
                Level = info.XPLevel,
                OldAge = info.OldAge,
                Necklace = info.Necklace,
                ShowingNecklace = info.ShowingNecklace,
                CursedState = info.CursedState,
                Special = info.Special,
                Clothing = info.Clothing,
                Outfit = info.Outfit,
                Hat = info.Hat,
                Faith = info.Faith,
                Happiness = stats?.Happiness ?? -1f,
                Illness = stats?.Illness ?? -1f,
                Dissent = info.Dissent,
                Satiation = stats?.Satiation ?? -1f,
                Starvation = stats?.Starvation ?? -1f,
                Exhaustion = stats?.Exhaustion ?? -1f,
                Rest = stats?.Rest ?? -1f,
                Drunk = stats?.Drunk ?? -1f,
                Bathroom = stats?.Bathroom ?? -1f,
                Reeducation = stats?.Reeducation ?? -1f,
                Social = stats?.Social ?? -1f,
                Pleasure = brainInfo?.Pleasure ?? -1,
                TotalPleasure = brainInfo?.TotalPleasure ?? -1,
                Traits = BuildTraitFacts(info),
                IsAiNpc = FollowerAIManager.IsNPC(info.ID)
            };

            return fact.ID >= 0;
        }

        private static void UpdateRequestFile()
        {
            if (UnityEngine.Time.realtimeSinceStartup < nextRequestFileCheckRealtime)
                return;

            nextRequestFileCheckRealtime = UnityEngine.Time.realtimeSinceStartup + 1f;
            var requestPath = GetReportRequestPath();
            if (!File.Exists(requestPath))
                return;

            TryDeleteRequestFile(requestPath);
            WriteLiveReport("request file RequestCurrentFollowerFactsReport.txt");
        }

        private static void WriteLiveReport(string source)
        {
            TryWriteCurrentFollowerFactsReport(BuildFactsFromLiveFollowers(), DateTime.UtcNow, source);
        }

        private static void TryDeleteRequestFile(string requestPath)
        {
            try
            {
                File.Delete(requestPath);
            }
            catch (Exception ex)
            {
                AICharacterPlugin.Log.LogWarning($"AI follower facts request file delete failed: {ex.Message}");
            }
        }

        private static FollowerAiFollowerFact CloneFact(FollowerAiFollowerFact fact)
        {
            return new FollowerAiFollowerFact
            {
                ID = fact.ID,
                Name = fact.Name,
                Role = fact.Role,
                Location = fact.Location,
                DesiredLocation = fact.DesiredLocation,
                AvailabilityStatus = fact.AvailabilityStatus,
                CurrentState = fact.CurrentState,
                CurrentTask = fact.CurrentTask,
                CurrentOverrideTask = fact.CurrentOverrideTask,
                Age = fact.Age,
                MemberDays = fact.MemberDays,
                Level = fact.Level,
                OldAge = fact.OldAge,
                Necklace = fact.Necklace,
                ShowingNecklace = fact.ShowingNecklace,
                CursedState = fact.CursedState,
                Special = fact.Special,
                Clothing = fact.Clothing,
                Outfit = fact.Outfit,
                Hat = fact.Hat,
                Faith = fact.Faith,
                Happiness = fact.Happiness,
                Illness = fact.Illness,
                Dissent = fact.Dissent,
                Satiation = fact.Satiation,
                Starvation = fact.Starvation,
                Exhaustion = fact.Exhaustion,
                Rest = fact.Rest,
                Drunk = fact.Drunk,
                Bathroom = fact.Bathroom,
                Reeducation = fact.Reeducation,
                Social = fact.Social,
                Pleasure = fact.Pleasure,
                TotalPleasure = fact.TotalPleasure,
                IsAiNpc = fact.IsAiNpc,
                Traits = (fact.Traits ?? new List<FollowerAiTraitFact>())
                    .Select(trait => new FollowerAiTraitFact
                    {
                        Type = trait.Type,
                        Name = trait.Name,
                        Title = trait.Title,
                        IsPositive = trait.IsPositive
                    })
                    .ToList()
            };
        }

        private static FollowerSelectEntry.Status GetAvailabilityStatus(FollowerInfo info)
        {
            try
            {
                return FollowerManager.GetFollowerAvailabilityStatus(info, excludeStarving: false, excludeChildren: true);
            }
            catch
            {
                return FollowerSelectEntry.Status.Unavailable;
            }
        }

        private static int SafeMemberDays(FollowerInfo info)
        {
            try
            {
                return Math.Max(0, TimeManager.CurrentDay - info.DayJoined);
            }
            catch
            {
                return -1;
            }
        }

        private static string SafeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Replace("\r", " ").Replace("\n", " ");
        }
    }
}

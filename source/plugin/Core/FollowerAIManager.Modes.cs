using System.Collections.Generic;

namespace COTL_AL_NPCs
{
    public static partial class FollowerAIManager
    {
        public static void SetNPCStatus(int followerID, bool isNPC)
        {
            SetNPCMode(followerID, isNPC ? FollowerAiMode.Character : FollowerAiMode.Vanilla);
        }

        public static void SetNPCMode(int followerID, FollowerAiMode mode)
        {
            var normalized = mode == FollowerAiMode.Character ? FollowerAiMode.Character : FollowerAiMode.Vanilla;
            var ai = GetOrCreateAI(followerID);
            ai.Mode = normalized;
            ai.IsNPC = normalized == FollowerAiMode.Character;
            SaveNPCStatuses();
            AICharacterPlugin.Log.LogInfo($"Follower {followerID} NPC mode set to {normalized}");
        }

        internal static bool ResetForFreshIndoctrination(int followerID, string source)
        {
            EnsureLoaded();
            if (followerID < 0)
                return false;

            var existed = followerAIs.ContainsKey(followerID);
            var wasNpc = existed && followerAIs[followerID].IsNPC;
            followerAIs.Remove(followerID);
            SaveNPCStatuses();
            SaveMemory();
            AICharacterPlugin.Log?.LogInfo($"Reset AI NPC saved identity for fresh indoctrination: follower={followerID}; was_npc={wasNpc}; source={source}.");
            return wasNpc || existed;
        }

        public static bool IsNPC(int followerID)
        {
            return IsModNPC(followerID);
        }

        public static bool IsModNPC(int followerID)
        {
            EnsureLoaded();
            return followerAIs.ContainsKey(followerID) &&
                   followerAIs[followerID].IsNPC &&
                   NormalizeMode(followerAIs[followerID]) == FollowerAiMode.Character;
        }

        public static FollowerAiMode GetMode(int followerID)
        {
            EnsureLoaded();
            return followerAIs.TryGetValue(followerID, out var ai) && ai.IsNPC
                ? NormalizeMode(ai)
                : FollowerAiMode.Vanilla;
        }

        public static List<int> GetNPCFollowerIDs()
        {
            EnsureLoaded();

            var ids = new List<int>();
            foreach (var followerAI in followerAIs)
            {
                if (followerAI.Value.IsNPC)
                    ids.Add(followerAI.Key);
            }

            return ids;
        }

        private static FollowerAiMode NormalizeMode(FollowerAI ai)
        {
            return ai != null && ai.IsNPC && ai.Mode == FollowerAiMode.Character
                ? FollowerAiMode.Character
                : FollowerAiMode.Vanilla;
        }
    }
}

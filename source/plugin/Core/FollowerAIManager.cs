using System;
using System.Collections.Generic;
using System.IO;

namespace COTL_AL_NPCs
{
    public static partial class FollowerAIManager
    {
        private static Dictionary<int, FollowerAI> followerAIs = new Dictionary<int, FollowerAI>();
        private static bool loaded;
        private static string loadedScopeKey = string.Empty;
        private static bool stateDirty;
        private static string SavePath => Path.Combine(FollowerAiSaveScope.CurrentScopedDirectory, "AiFollowers.json");

        public static FollowerAI GetOrCreateAI(int followerID)
        {
            EnsureLoaded();

            if (!followerAIs.ContainsKey(followerID))
                followerAIs[followerID] = new FollowerAI(followerID);

            return followerAIs[followerID];
        }

        public static void UpdateDeferredSaves()
        {
            // Product AI state commits only when the game save succeeds, so mod
            // memory cannot advance beyond the last in-game save point.
        }

        private static void MarkStateDirty()
        {
            stateDirty = true;
        }

        private static FollowerAI GetOrCreateAIWithoutLoading(int followerID)
        {
            if (!followerAIs.ContainsKey(followerID))
                followerAIs[followerID] = new FollowerAI(followerID);

            return followerAIs[followerID];
        }

        private static void EnsureLoaded()
        {
            var currentScopeKey = FollowerAiSaveScope.CurrentSaveKey;
            if (!loaded || !string.Equals(loadedScopeKey, currentScopeKey, StringComparison.OrdinalIgnoreCase))
                LoadNPCStatuses();
        }
    }
}

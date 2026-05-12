using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace COTL_AL_NPCs
{
    internal static class FollowerAiGameSaveCommit
    {
        internal static void Commit(string source)
        {
            var commitSource = string.IsNullOrWhiteSpace(source)
                ? "SaveAndLoad.Save"
                : source.Trim();

            CommitStore("follower mode and memory", commitSource, () => FollowerAIManager.CommitForGameSave(commitSource));
            CommitStore("current events", commitSource, () => FollowerAiSocialMemory.CommitForGameSave(commitSource));
        }

        private static void CommitStore(string storeName, string source, Action commit)
        {
            try
            {
                commit();
            }
            catch (Exception ex)
            {
                AICharacterPlugin.Log?.LogWarning($"AI {storeName} commit failed after game save ({source}): {ex.Message}");
            }
        }
    }

    [HarmonyPatch]
    internal static class FollowerAiSaveAndLoadSavePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools.GetDeclaredMethods(typeof(SaveAndLoad))
                .Where(method => string.Equals(method.Name, "Save", StringComparison.Ordinal));
        }

        private static void Postfix(MethodBase __originalMethod)
        {
            FollowerAiGameSaveCommit.Commit(DescribeMethod(__originalMethod));
        }

        private static string DescribeMethod(MethodBase method)
        {
            if (method == null)
            {
                return "SaveAndLoad.Save";
            }

            return $"{method.DeclaringType?.Name ?? "SaveAndLoad"}.{method.Name}";
        }
    }
}

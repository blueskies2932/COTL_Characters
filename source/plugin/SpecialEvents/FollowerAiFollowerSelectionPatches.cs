using HarmonyLib;
using Lamb.UI.FollowerSelect;
using System;
using System.Collections.Generic;

namespace COTL_AL_NPCs
{
    [HarmonyPatch(typeof(UIFollowerSelectMenuController), "Show", new[]
    {
        typeof(List<FollowerSelectEntry>),
        typeof(bool),
        typeof(UpgradeSystem.Type),
        typeof(bool),
        typeof(bool),
        typeof(bool),
        typeof(bool),
        typeof(bool)
    })]
    internal static class FollowerAiFollowerSelectionCapturePatch
    {
        private static void Prefix(List<FollowerSelectEntry> followerSelectEntries)
        {
            FollowerAiFollowerFacts.CaptureNativeFollowerMenuEntries(followerSelectEntries);
        }
    }

    [HarmonyPatch(typeof(UIFollowerSelectMenuController), "FollowerSelected")]
    internal static class FollowerAiFollowerSelectionRitualTargetPatch
    {
        private static void Prefix(UIFollowerSelectMenuController __instance, FollowerInfo followerInfo)
        {
            if (__instance == null || followerInfo == null)
                return;

            try
            {
                var selectionType = Traverse.Create(__instance).Field("_followerSelectionType").GetValue<UpgradeSystem.Type>();
                FollowerAiCurrentEvents.NoteFollowerSelectionForRitual(selectionType, followerInfo);
            }
            catch (Exception ex)
            {
                AICharacterPlugin.LogInfoVerbose($"AI follower selection ritual-target capture failed: {ex.Message}");
            }
        }
    }
}

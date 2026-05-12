using HarmonyLib;
using System;
using System.Linq;

namespace COTL_AL_NPCs
{
    [HarmonyPatch(typeof(interaction_FollowerInteraction), "OnInteract")]
    public static class FollowerAiInteractOpenPatch
    {
        private static bool Prefix(interaction_FollowerInteraction __instance)
        {
            try
            {
                if (!TryGetModNpcFollowerID(__instance, out var followerID))
                    return true;

                AICharacterPlugin.Log.LogInfo($"AI follower interact opened conversation alongside vanilla menu for follower {followerID}.");
                FollowerAiConversationOverlay.Open(followerID);
                return true;
            }
            catch (Exception ex)
            {
                AICharacterPlugin.Log.LogWarning($"AI follower direct interact hook failed; allowing vanilla interaction. {ex.Message}");
                return true;
            }
        }

        internal static bool TryGetModNpcFollowerID(interaction_FollowerInteraction interaction, out int followerID)
        {
            followerID = 0;
            var follower = FollowerAiInteractionReflection.GetMemberValue(interaction, "follower");
            return FollowerAiNativeRoleTools.TryGetFollowerIDFromFollower(follower as Follower, out followerID) &&
                   FollowerAIManager.IsModNPC(followerID);
        }
    }

    [HarmonyPatch(typeof(interaction_FollowerInteraction), "OnFollowerCommandFinalized")]
    public static class FollowerAiInteractionPatch
    {
        private static bool Prefix(interaction_FollowerInteraction __instance, FollowerCommands[] followerCommands)
        {
            try
            {
                if (followerCommands == null)
                    return true;

                if (!FollowerAiInteractOpenPatch.TryGetModNpcFollowerID(__instance, out var followerID))
                    return true;

                if (followerCommands.Contains(FollowerCommands.Murder))
                    return true;

                if (followerCommands.Contains(FollowerCommands.Talk))
                    FollowerAiConversationOverlay.Open(followerID);

                return true;
            }
            catch (Exception ex)
            {
                AICharacterPlugin.Log.LogWarning($"AI follower Talk hook failed; allowing vanilla Talk. {ex.Message}");
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(interaction_FollowerInteraction), "Close", new Type[] { })]
    public static class FollowerAiInteractionClosePatch
    {
        private static void Postfix(interaction_FollowerInteraction __instance)
        {
            NotifyClosed(__instance);
        }

        internal static void NotifyClosed(interaction_FollowerInteraction instance)
        {
            try
            {
                if (FollowerAiInteractOpenPatch.TryGetModNpcFollowerID(instance, out var followerID))
                    FollowerAiConversationOverlay.NotifyFollowerInteractionClosed(followerID);
            }
            catch (Exception ex)
            {
                AICharacterPlugin.Log.LogWarning($"AI follower interaction close hook failed: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(interaction_FollowerInteraction), "Close", new[] { typeof(bool), typeof(bool), typeof(bool) })]
    public static class FollowerAiInteractionCloseArgsPatch
    {
        private static void Postfix(interaction_FollowerInteraction __instance)
        {
            FollowerAiInteractionClosePatch.NotifyClosed(__instance);
        }
    }

    [HarmonyPatch(typeof(interaction_FollowerInteraction), "ResetFollower")]
    public static class FollowerAiInteractionResetFollowerPatch
    {
        private static void Prefix(interaction_FollowerInteraction __instance)
        {
            FollowerAiInteractionClosePatch.NotifyClosed(__instance);
        }
    }

    [HarmonyPatch(typeof(interaction_FollowerInteraction), "OnDisableInteraction")]
    public static class FollowerAiInteractionDisablePatch
    {
        private static void Postfix(interaction_FollowerInteraction __instance)
        {
            FollowerAiInteractionClosePatch.NotifyClosed(__instance);
        }
    }

    [HarmonyPatch(typeof(Lamb.UI.FollowerInteractionWheel.UIFollowerInteractionWheelOverlayController), "OnHideStarted")]
    public static class FollowerAiInteractionWheelHidePatch
    {
        private static void Postfix()
        {
            FollowerAiConversationOverlay.NotifyNativeInteractionMenuClosed();
        }
    }
}

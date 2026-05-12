using HarmonyLib;
using System;

namespace COTL_AL_NPCs
{
    internal static class FollowerAiFollowerLifecycleEvents
    {
        internal static void HandleDeath(Follower follower, string source)
        {
            FollowerAiCurrentEvents.RecordDeath(follower, source);
        }

        internal static void HandleDeath(int followerID, string source)
        {
            FollowerAiCurrentEvents.RecordDeath(followerID, source);
        }

        internal static void HandleSinTakenAway(Follower follower, string source)
        {
            FollowerAiCurrentEvents.RecordSinTakenAway(follower, source);
        }

        internal static void HandleResurrection(int followerID, string source)
        {
            FollowerAiCurrentEvents.RecordResurrection(followerID, source);
        }

        internal static void HandleResurrection(Follower follower, string source)
        {
            FollowerAiCurrentEvents.RecordResurrection(follower, source);
        }
    }

    [HarmonyPatch(typeof(Follower), "Die")]
    internal static class FollowerAiFollowerDiePatch
    {
        private static void Prefix(Follower __instance)
        {
            FollowerAiFollowerLifecycleEvents.HandleDeath(__instance, "Follower.Die");
        }
    }

    [HarmonyPatch(typeof(FollowerManager), "FollowerDie")]
    internal static class FollowerAiFollowerManagerDiePatch
    {
        private static void Prefix(int ID, NotificationCentre.NotificationType deathNotificationType)
        {
            FollowerAiFollowerLifecycleEvents.HandleDeath(ID, $"FollowerManager.FollowerDie:{deathNotificationType}");
        }
    }

    [HarmonyPatch(typeof(Follower), "Leave")]
    internal static class FollowerAiFollowerLeavePatch
    {
        private static void Prefix(Follower __instance, NotificationCentre.NotificationType leaveNotificationType)
        {
            if (IsActualSinTakenAwayNotification(leaveNotificationType))
                FollowerAiFollowerLifecycleEvents.HandleSinTakenAway(__instance, $"Follower.Leave:{leaveNotificationType}");
        }

        private static bool IsActualSinTakenAwayNotification(NotificationCentre.NotificationType notificationType)
        {
            var text = notificationType.ToString();
            return ContainsAny(text, "Sin", "Possess", "Damn");
        }

        private static bool ContainsAny(string value, params string[] terms)
        {
            foreach (var term in terms)
            {
                if (!string.IsNullOrWhiteSpace(term) &&
                    value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(FollowerManager), "ReviveFollower")]
    internal static class FollowerAiReviveFollowerPatch
    {
        private static void Postfix(int ID)
        {
            FollowerAiFollowerLifecycleEvents.HandleResurrection(ID, "FollowerManager.ReviveFollower");
        }
    }

    [HarmonyPatch(typeof(FollowerManager), "ResurrectFollower")]
    internal static class FollowerAiResurrectFollowerPatch
    {
        private static void Postfix(Follower follower)
        {
            FollowerAiFollowerLifecycleEvents.HandleResurrection(follower, "FollowerManager.ResurrectFollower");
        }
    }
}

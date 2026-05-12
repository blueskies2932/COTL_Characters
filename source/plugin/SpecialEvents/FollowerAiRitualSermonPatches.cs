using HarmonyLib;

namespace COTL_AL_NPCs
{
    [HarmonyPatch(typeof(ChurchFollowerManager), "StartRitualOverlay")]
    internal static class FollowerAiStartRitualOverlayPatch
    {
        private static void Prefix()
        {
            FollowerAiSpecialEventGuard.SuppressGlobalInterference("ChurchFollowerManager.StartRitualOverlay", 60f);
        }
    }

    [HarmonyPatch(typeof(Interaction_TempleAltar), "PerformRitual")]
    internal static class FollowerAiPerformRitualPatch
    {
        private static void Prefix(UpgradeSystem.Type RitualType)
        {
            FollowerAiCurrentEvents.RecordRitual("Interaction_TempleAltar.PerformRitual", RitualType);
        }
    }

    [HarmonyPatch(typeof(ChurchFollowerManager), "EndRitualOverlay")]
    internal static class FollowerAiEndRitualOverlayPatch
    {
        private static void Postfix()
        {
            FollowerAiSpecialEventGuard.SuppressGlobalInterference("ChurchFollowerManager.EndRitualOverlay", 8f);
        }
    }

    [HarmonyPatch(typeof(ChurchFollowerManager), "StartSermonOverlay")]
    internal static class FollowerAiStartSermonOverlayPatch
    {
        private static void Prefix()
        {
            FollowerAiSpecialEventGuard.SuppressGlobalInterference("ChurchFollowerManager.StartSermonOverlay", 60f);
            FollowerAiCurrentEvents.RecordSermon("ChurchFollowerManager.StartSermonOverlay");
        }
    }

    [HarmonyPatch(typeof(ChurchFollowerManager), "EndSermonOverlay")]
    internal static class FollowerAiEndSermonOverlayPatch
    {
        private static void Postfix()
        {
            FollowerAiSpecialEventGuard.SuppressGlobalInterference("ChurchFollowerManager.EndSermonOverlay", 8f);
        }
    }

    [HarmonyPatch(typeof(ChurchFollowerManager), "StartSermonEffect")]
    internal static class FollowerAiStartSermonEffectPatch
    {
        private static void Prefix()
        {
            FollowerAiSpecialEventGuard.SuppressGlobalInterference("ChurchFollowerManager.StartSermonEffect", 60f);
            FollowerAiCurrentEvents.RecordSermon("ChurchFollowerManager.StartSermonEffect");
        }
    }

    [HarmonyPatch(typeof(ChurchFollowerManager), "EndSermonEffect")]
    internal static class FollowerAiEndSermonEffectPatch
    {
        private static void Postfix()
        {
            FollowerAiSpecialEventGuard.SuppressGlobalInterference("ChurchFollowerManager.EndSermonEffect", 8f);
        }
    }

    [HarmonyPatch(typeof(FollowerTask_AttendRitual), "Setup")]
    internal static class FollowerAiAttendRitualSetupPatch
    {
        private static bool Prefix()
        {
            FollowerAiSpecialEventGuard.SuppressGlobalInterference("FollowerTask_AttendRitual.Setup", 45f);
            return true;
        }
    }

    [HarmonyPatch(typeof(FollowerTask_AssistRitual), "Setup")]
    internal static class FollowerAiAssistRitualSetupPatch
    {
        private static bool Prefix()
        {
            FollowerAiSpecialEventGuard.SuppressGlobalInterference("FollowerTask_AssistRitual.Setup", 45f);
            return true;
        }
    }
}

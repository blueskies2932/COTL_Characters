using HarmonyLib;

namespace COTL_AL_NPCs
{
    [HarmonyPatch(typeof(Lamb.UI.UIDoctrineMenuController), "DoShowAnimation")]
    internal static class FollowerAiCultAboutDoctrineShowPatch
    {
        private static void Postfix()
        {
            FollowerAiCultAboutOverlay.Show();
        }
    }

    [HarmonyPatch(typeof(Lamb.UI.UIDoctrineMenuController), "OnHideCompleted")]
    internal static class FollowerAiCultAboutDoctrineHidePatch
    {
        private static void Postfix()
        {
            FollowerAiCultAboutOverlay.Hide();
        }
    }
}

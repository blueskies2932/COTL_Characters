using System;

namespace COTL_AL_NPCs
{
    internal static partial class FollowerAiInvocations
    {
        private const int WindowID = 192093;
        private const string InputBlockerOwner = "invocations";
        private const string MaxCultFaithID = "max_cult_faith";
        private const string ClearVanillaFollowerRolesID = "clear_vanilla_follower_roles";
        private static readonly object Sync = new object();
        private static FollowerAiInvocationState state;

        internal static void ResetForSaveScopeChange()
        {
            lock (Sync)
                state = null;

            visible = false;
            FollowerAiOverlayInputBlocker.Hide(InputBlockerOwner);
            windowRectInitialized = false;
        }

        private static string FormatInvocationReceipt(bool success)
        {
            return success ? "Invocation:successful" : "Invocation:failed";
        }

        private static bool IsPauseScreenOpen()
        {
            return FollowerAiGameState.IsSimulationPaused();
        }
    }
}

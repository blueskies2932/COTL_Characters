namespace COTL_AL_NPCs
{
    internal static class FollowerAiSpecialEventGuard
    {
        internal static void SuppressGlobalInterference(string source, float seconds)
        {
            FollowerAiGameState.SuppressTaskInterferenceForSeconds(source, seconds);
        }
    }
}

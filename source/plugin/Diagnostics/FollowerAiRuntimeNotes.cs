namespace COTL_AL_NPCs
{
    internal static class FollowerAiRuntimeNotes
    {
        internal static void Record(int followerID, string line)
        {
            FollowerAiDiagnostics.Record("runtime note", line, actorID: followerID);
        }
    }
}

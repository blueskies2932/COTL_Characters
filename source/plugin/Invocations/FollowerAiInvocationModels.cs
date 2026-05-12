using System.Collections.Generic;

namespace COTL_AL_NPCs
{
    internal sealed class FollowerAiInvocationEntry
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string Code = string.Empty;
    }

    internal sealed class FollowerAiInvocationState
    {
        public List<FollowerAiInvocationEntry> Invocations = new List<FollowerAiInvocationEntry>();
    }
}

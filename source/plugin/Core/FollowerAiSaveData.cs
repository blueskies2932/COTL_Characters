using Newtonsoft.Json;
using System.Collections.Generic;

namespace COTL_AL_NPCs
{
    internal sealed class FollowerAiSaveData
    {
        [JsonProperty("schema")]
        public string Schema = "COTL_AL_NPCs.Product.AiFollowers.v1";

        [JsonProperty("save_scope")]
        public string SaveScope = string.Empty;

        [JsonProperty("followers")]
        public List<FollowerAiSavedFollower> Followers = new List<FollowerAiSavedFollower>();
    }

    internal sealed class FollowerAiSavedFollower
    {
        [JsonProperty("follower_id")]
        public int FollowerID;

        [JsonProperty("mode")]
        public string Mode = nameof(FollowerAiMode.Vanilla);

        [JsonProperty("last_interaction")]
        public string LastInteraction = string.Empty;

        [JsonProperty("conversation_history")]
        public List<string> ConversationHistory = new List<string>();

        [JsonProperty("outcome_memory")]
        public List<string> OutcomeMemory = new List<string>();
    }
}

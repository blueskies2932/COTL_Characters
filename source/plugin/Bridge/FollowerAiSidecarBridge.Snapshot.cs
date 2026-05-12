using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;

namespace COTL_AL_NPCs
{
    internal static partial class FollowerAiSidecarBridge
    {
        private static void ExportSnapshot()
        {
            try
            {
                EnsureDirectories();

                var followers = FollowerAiFollowerFacts.GetCurrentFollowers();
                var aiFollowers = FollowerAIManager.GetNPCFollowerIDs();
                var snapshot = new JObject
                {
                    ["schema_version"] = 1,
                    ["created_utc"] = DateTime.UtcNow.ToString("O"),
                    ["save_scope"] = FollowerAiSaveScope.CurrentSaveKey,
                    ["save_display"] = FollowerAiSaveScope.CurrentDisplayName,
                    ["game_running"] = FollowerAiGameState.ShouldRunBackgroundBrainWork(),
                    ["counts"] = new JObject
                    {
                        ["followers"] = followers.Count,
                        ["ai_followers"] = aiFollowers.Count
                    },
                    ["followers"] = new JArray(followers.Select(BuildFollowerSnapshotJson)),
                    ["sidecar_contract"] = "The sidecar may think, call the configured AI provider, read exported snapshots/files, and write response JSON. The game remains the only executor of Unity state changes."
                };

                WriteJsonAtomic(
                    System.IO.Path.Combine(SnapshotDirectory, "live-state.json"),
                    snapshot.ToString(Formatting.Indented));
            }
            catch (Exception ex)
            {
                AICharacterPlugin.LogInfoVerbose($"Sidecar snapshot export skipped: {ex.Message}");
            }
        }

        private static JObject BuildFollowerSnapshotJson(FollowerAiFollowerFact follower)
        {
            return new JObject
            {
                ["id"] = follower.ID,
                ["name"] = follower.Name ?? string.Empty,
                ["role"] = follower.Role.ToString(),
                ["current_task"] = follower.CurrentTask.ToString(),
                ["override_task"] = follower.CurrentOverrideTask.ToString(),
                ["state"] = follower.CurrentState.ToString(),
                ["age"] = follower.Age,
                ["old"] = follower.OldAge,
                ["level"] = follower.Level,
                ["necklace"] = follower.Necklace.ToString(),
                ["faith"] = follower.Faith,
                ["happiness"] = follower.Happiness,
                ["illness"] = follower.Illness,
                ["dissent"] = follower.Dissent,
                ["satiation"] = follower.Satiation,
                ["exhaustion"] = follower.Exhaustion,
                ["npc"] = follower.IsAiNpc,
                ["traits"] = new JArray((follower.Traits ?? new System.Collections.Generic.List<FollowerAiTraitFact>())
                    .Where(trait => trait != null && !string.IsNullOrWhiteSpace(trait.Name))
                    .Select(trait => trait.Name))
            };
        }
    }
}

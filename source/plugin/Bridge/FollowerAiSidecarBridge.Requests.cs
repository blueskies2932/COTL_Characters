using BepInEx;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace COTL_AL_NPCs
{
    internal static partial class FollowerAiSidecarBridge
    {
        private static string BuildDecisionRequestJson(
            OpenAiFollowerDecisionContext context,
            string requestID,
            string progressPath)
        {
            var models = OpenAiFollowerDecisionClient.GetModelCandidates().ToList();
            var responseFormat = OpenAiFollowerDecisionClient.BuildResponseFormatJson(context);
            var speaker = context.Followers?.FirstOrDefault(follower => follower.ID == context.SpeakerID);
            var awareness = context.CharacterAwareness ?? new FollowerAiCharacterAwarenessSettings();
            var characterMode = context.CharacterModeEnabled && !context.IsInvocationReply;
            var recentCultEvents = characterMode
                ? BuildCharacterModeRecentEvents(awareness)
                : FollowerAiCurrentEvents.BuildPromptContext();

            var request = new JObject
            {
                ["schema_version"] = 2,
                ["request_type"] = "ai_decision",
                ["request_id"] = requestID,
                ["created_utc"] = DateTime.UtcNow.ToString("O"),
                ["save_scope"] = FollowerAiSaveScope.CurrentSaveKey,
                ["save_display"] = FollowerAiSaveScope.CurrentDisplayName,
                ["snapshot_path"] = Path.Combine(SnapshotDirectory, "live-state.json"),
                ["progress_path"] = progressPath ?? string.Empty,
                ["internet_sources_path"] = Path.Combine(InternetSourcesDirectory, $"{requestID}.sources.json"),
                ["model_candidates"] = new JArray(models),
                ["response_format"] = responseFormat,
                ["context"] = new JObject
                {
                    ["speaker_id"] = context.SpeakerID,
                    ["speaker_name"] = context.SpeakerName ?? string.Empty,
                    ["speaker_trait_profile"] = !characterMode || awareness.PersonalTraits ? FollowerTraitVoiceProfile.BuildPersonal(speaker) : string.Empty,
                    ["speaker_cult_trait_profile"] = FollowerTraitVoiceProfile.BuildCult(),
                    ["speaker_necklace"] = !characterMode || awareness.PersonalTraits ? (speaker != null ? speaker.Necklace.ToString() : "NONE") : string.Empty,
                    ["current_day"] = SafeCurrentDay(),
                    ["recent_cult_events_today"] = recentCultEvents,
                    ["world_state_context"] = !characterMode || awareness.WorldState ? FollowerAiWorldStateContext.BuildPromptContext() : string.Empty,
                    ["cult_about_context"] = !characterMode || awareness.CultAbout ? FollowerAiCultAbout.Get() : string.Empty,
                    ["character_lore_context"] = characterMode && awareness.Lore ? awareness.LoreText ?? string.Empty : string.Empty,
                    ["player_text"] = context.PlayerText ?? string.Empty,
                    ["is_directive_bridge_reply"] = context.IsInvocationReply,
                    ["directive_bridge_source"] = context.IsInvocationReply ? "Invocation receipt" : string.Empty,
                    ["directive_bridge_response"] = context.InvocationReceipt ?? string.Empty,
                    ["special_system_message"] = context.SpecialSystemMessage ?? string.Empty,
                    ["internet_access_enabled"] = !context.IsInvocationReply && FollowerAiInternetAccessOverlay.IsEnabled,
                    ["character_mode_enabled"] = context.CharacterModeEnabled,
                    ["reply_length"] = characterMode ? context.ReplyLength.ToString().ToLowerInvariant() : string.Empty,
                    ["character_mode_conversation_history"] = context.CharacterModeConversationHistory ?? string.Empty,
                    ["character_awareness"] = new JObject
                    {
                        ["personal_traits"] = awareness.PersonalTraits,
                        ["cult_about"] = awareness.CultAbout,
                        ["current_events"] = awareness.CurrentEvents,
                        ["tournament_details"] = awareness.TournamentDetails,
                        ["world_state"] = awareness.WorldState,
                        ["long_term_conversation_history"] = awareness.LongTermConversationHistory,
                        ["lore"] = awareness.Lore
                    },
                    ["conversation_history"] = string.IsNullOrWhiteSpace(context.ActiveConversationTranscript)
                        ? new JArray()
                        : new JArray(context.ActiveConversationTranscript)
                }
            };

            return request.ToString(Formatting.Indented);
        }

        private static int SafeCurrentDay()
        {
            try
            {
                return TimeManager.CurrentDay;
            }
            catch
            {
                return 0;
            }
        }

        private static string BuildCharacterModeRecentEvents(FollowerAiCharacterAwarenessSettings awareness)
        {
            var lines = new List<string>();
            if (awareness.CurrentEvents)
            {
                var eventsText = FollowerAiCurrentEvents.BuildPromptContext(includeTournament: false);
                if (!string.IsNullOrWhiteSpace(eventsText))
                    lines.Add(eventsText);
            }

            if (awareness.TournamentDetails)
            {
                var tournamentText = FollowerAiCurrentEvents.BuildTournamentPromptContext();
                if (!string.IsNullOrWhiteSpace(tournamentText))
                    lines.Add(tournamentText);
            }

            return string.Join("\n", lines);
        }

        private static bool TryReadProgress(string progressPath, out string message)
        {
            message = string.Empty;
            try
            {
                var json = JObject.Parse(File.ReadAllText(progressPath));
                message = json["message"]?.Value<string>() ?? string.Empty;
                return !string.IsNullOrWhiteSpace(message);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadSidecarResponse(
            string responsePath,
            out OpenAiFollowerDecision decision,
            out string message)
        {
            decision = null;
            message = string.Empty;

            JObject response;
            try
            {
                response = JObject.Parse(File.ReadAllText(responsePath));
            }
            catch
            {
                return false;
            }

            var success = response["success"]?.Value<bool>() ?? false;
            message = response["message"]?.Value<string>() ?? string.Empty;
            if (!success)
            {
                if (string.IsNullOrWhiteSpace(message))
                    message = "Sidecar returned an unsuccessful response.";
                return false;
            }

            var outputText = response["decision_json"]?.Value<string>()
                ?? response["output_text"]?.Value<string>()
                ?? response["decision"]?.ToString(Formatting.None)
                ?? string.Empty;
            if (string.IsNullOrWhiteSpace(outputText))
            {
                message = "Sidecar response did not include decision_json/output_text.";
                return false;
            }

            decision = OpenAiFollowerDecisionClient.ParseDecision(outputText);
            if (decision == null)
            {
                message = $"Sidecar response was not a usable decision: {Trim(outputText, 500)}";
                return false;
            }

            if (string.IsNullOrWhiteSpace(message))
                message = "Sidecar returned a character reply.";
            else
                message = $"{message} Sidecar returned a character reply.";
            return true;
        }
    }
}

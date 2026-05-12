using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace COTL_AL_NPCs
{
    internal sealed class OpenAiFollowerDecision
    {
        public string Reply = string.Empty;
    }

    internal sealed class OpenAiFollowerDecisionContext
    {
        public int SpeakerID;
        public string SpeakerName = string.Empty;
        public string PlayerText = string.Empty;
        public List<FollowerAiFollowerFact> Followers = new List<FollowerAiFollowerFact>();
        public string ActiveConversationTranscript = string.Empty;
        public bool IsInvocationReply;
        public string InvocationReceipt = string.Empty;
        public string SpecialSystemMessage = string.Empty;
        public bool CharacterModeEnabled;
        public FollowerAiCharacterAwarenessSettings CharacterAwareness = new FollowerAiCharacterAwarenessSettings();
        public string CharacterModeConversationHistory = string.Empty;
        public FollowerAiReplyLength ReplyLength = FollowerAiReplyLength.Medium;
    }

    internal static class OpenAiFollowerDecisionClient
    {
        internal static bool IsConfigured =>
            AICharacterPlugin.OpenAIEnabled != null &&
            AICharacterPlugin.OpenAIEnabled.Value &&
            FollowerAiProviderSetup.IsConfigured();

        internal static bool CanAcceptRequests =>
            AICharacterPlugin.OpenAIEnabled != null &&
            AICharacterPlugin.OpenAIEnabled.Value &&
            FollowerAiProviderSetup.IsConfigured() &&
            FollowerAiSidecarBridge.IsEnabledForDecisionRequests();

        internal static OpenAiFollowerDecisionContext CreateContext(int speakerID, string playerText)
        {
            var followers = FollowerAiFollowerFacts.GetCurrentFollowers();
            var speaker = followers.FirstOrDefault(follower => follower.ID == speakerID);
            return new OpenAiFollowerDecisionContext
            {
                SpeakerID = speakerID,
                SpeakerName = speaker?.Name ?? $"Follower {speakerID}",
                PlayerText = playerText ?? string.Empty,
                Followers = followers,
                CharacterModeEnabled = FollowerAIManager.GetMode(speakerID) == FollowerAiMode.Character,
                CharacterAwareness = FollowerAiCharacterModeSettings.Get(speakerID),
                ReplyLength = FollowerAiCharacterModeSettings.Get(speakerID).ReplyLength
            };
        }

        internal static OpenAiFollowerDecisionContext CreateInvocationReplyContext(
            int speakerID,
            string receipt,
            string specialSystemMessage)
        {
            var context = CreateContext(speakerID, receipt ?? string.Empty);
            context.IsInvocationReply = true;
            context.InvocationReceipt = receipt ?? string.Empty;
            context.SpecialSystemMessage = specialSystemMessage ?? string.Empty;
            return context;
        }

        internal static bool TryDecide(OpenAiFollowerDecisionContext context, out OpenAiFollowerDecision decision, out string message, Action<string> onProgress = null)
        {
            decision = null;
            message = string.Empty;

            if (FollowerAiSidecarBridge.TryDecide(context, out decision, out var sidecarMessage, onProgress))
            {
                message = sidecarMessage;
                return true;
            }

            message = string.IsNullOrWhiteSpace(sidecarMessage)
                ? "Sidecar AI route is unavailable."
                : sidecarMessage;
            FollowerAiDiagnostics.Record("sidecar AI unavailable", message, context?.SpeakerID ?? -1, -1, null, context?.PlayerText);
            return false;
        }

        internal static JObject BuildResponseFormatJson(OpenAiFollowerDecisionContext context)
        {
            return new JObject
            {
                ["max_output_tokens"] = 2200,
                ["reasoning_effort"] = AICharacterPlugin.OpenAIReasoningEffort.Value?.Trim() ?? string.Empty
            };
        }

        internal static IEnumerable<string> GetModelCandidates()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var model in SplitModels(FollowerAiProviderSetup.GetConfiguredModel()).Concat(SplitModels(AICharacterPlugin.OpenAIModelFallbacks.Value)))
            {
                if (seen.Add(model))
                    yield return model;
            }
        }

        internal static OpenAiFollowerDecision ParseDecision(string outputText)
        {
            var json = JObject.Parse(outputText);
            return new OpenAiFollowerDecision
            {
                Reply = json["reply"]?.Value<string>() ?? string.Empty
            };
        }

        private static IEnumerable<string> SplitModels(string value)
        {
            return (value ?? string.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(model => model.Trim())
                .Where(model => !string.IsNullOrWhiteSpace(model));
        }
    }
}

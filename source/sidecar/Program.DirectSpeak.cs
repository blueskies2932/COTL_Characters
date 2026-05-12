using System.Text.Json;
using System.Text.Json.Nodes;

internal static partial class Program
{
    private static async Task<string> SendDirectSpeakReply(JsonElement requestRoot, string model)
    {
        var preparedLongTermMemory = await PrepareCharacterLongTermMemory(requestRoot, model);
        var aiRequest = BuildDirectSpeakAiRequest(requestRoot, model, preparedLongTermMemory);
        var outputText = string.Empty;
        if (aiRequest != null)
        {
            var internetEnabled = IsInternetAccessEnabled(requestRoot);
            var sourcesPath = internetEnabled ? GetString(requestRoot, "internet_sources_path") : string.Empty;
            outputText = internetEnabled && ActiveProvider?.SupportsNativeWebSearch == true
                ? await SendAiRequestWithSourceArchive(aiRequest, sourcesPath)
                : await SendAiRequest(aiRequest);
        }

        if (string.IsNullOrWhiteSpace(outputText))
        {
            return new JsonObject
            {
                ["reply"] = string.Empty
            }.ToJsonString(JsonOptions);
        }

        var reply = NormalizeDirectSpeakReply(outputText);
        if (ShouldRunDialogueEditorialPass(reply))
        {
            var editedReply = NormalizeDirectSpeakReply(await SendDialogueEditorialPass(requestRoot, model, reply));
            if (!string.IsNullOrWhiteSpace(editedReply))
                reply = editedReply;
        }

        return new JsonObject
        {
            ["reply"] = reply
        }.ToJsonString(JsonOptions);
    }

    private static AiRequest BuildDirectSpeakAiRequest(JsonElement requestRoot, string model, string preparedLongTermMemory)
    {
        var requestParts = requestRoot.GetProperty("response_format");
        var internetEnabled = IsInternetAccessEnabled(requestRoot) && ActiveProvider?.SupportsNativeWebSearch == true;
        var requireWebSearch = internetEnabled && IsExplicitSearchRequest(requestRoot);
        return BuildTextAiRequestFromPrompts(
            model,
            BuildCharacterSystemPrompt(
                requestRoot,
                includeTraitGuidance: true,
                includeConversation: true,
                includeWebSearchInstruction: internetEnabled,
                preparedLongTermMemory: preparedLongTermMemory),
            BuildSidecarUserPrompt(requestRoot),
            GetInt(requestParts, "max_output_tokens", 2200),
            GetString(requestParts, "reasoning_effort"),
            internetEnabled,
            requireWebSearch);
    }
}

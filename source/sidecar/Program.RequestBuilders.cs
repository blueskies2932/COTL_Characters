using System.Text.Json.Nodes;

internal static partial class Program
{
    private static string BuildPlainReplyDecisionJson(string outputText)
    {
        var reply = NormalizeDirectSpeakReply(outputText);
        if (string.IsNullOrWhiteSpace(reply))
            return string.Empty;

        return new JsonObject
        {
            ["reply"] = reply
        }.ToJsonString(JsonOptions);
    }

    private static AiRequest BuildStructuredAiRequestFromPrompts(
        string model,
        string systemPrompt,
        string userPrompt,
        AiResponseFormat responseFormat,
        int maxOutputTokens,
        string reasoningEffort)
    {
        return new AiRequest
        {
            Model = model,
            SystemPrompt = systemPrompt,
            Messages = new[]
            {
                new AiMessage
                {
                    Role = "user",
                    Content = userPrompt
                }
            },
            ResponseFormat = responseFormat,
            MaxTokens = maxOutputTokens,
            ReasoningEffort = reasoningEffort,
            Stream = false
        };
    }

    private static AiRequest BuildTextAiRequestFromPrompts(
        string model,
        string systemPrompt,
        string userPrompt,
        int maxOutputTokens,
        string reasoningEffort,
        bool enableWebSearch = false,
        bool requireWebSearch = false)
    {
        return new AiRequest
        {
            Model = model,
            SystemPrompt = systemPrompt,
            Messages = new[]
            {
                new AiMessage
                {
                    Role = "user",
                    Content = userPrompt
                }
            },
            MaxTokens = Math.Max(900, maxOutputTokens),
            ReasoningEffort = reasoningEffort,
            EnableWebSearch = enableWebSearch,
            RequireWebSearch = requireWebSearch,
            Stream = false
        };
    }

    private static AiResponseFormat BuildJsonSchemaResponseFormat(string name, JsonObject schema)
    {
        return new AiResponseFormat
        {
            Name = name,
            Strict = true,
            Schema = schema
        };
    }
}

using System.Text.Json;
using System.Text.Json.Nodes;

internal static partial class Program
{
    private static bool IsReceiptReplyRequest(JsonElement requestRoot)
    {
        return requestRoot.TryGetProperty("context", out var context)
            && GetBool(context, "is_directive_bridge_reply");
    }

    private static bool IsCharacterModeRequest(JsonElement requestRoot)
    {
        return requestRoot.TryGetProperty("context", out var context)
            && GetBool(context, "character_mode_enabled");
    }

    private static AiRequest BuildReceiptReplyAiRequest(JsonElement requestRoot, string model)
    {
        var invocationReply = IsInvocationReceiptReplyRequest(requestRoot);
        var requestParts = requestRoot.GetProperty("response_format");
        var schema = new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["properties"] = new JsonObject
            {
                ["reply"] = new JsonObject { ["type"] = "string" }
            },
            ["required"] = new JsonArray("reply")
        };

        return BuildStructuredAiRequestFromPrompts(
            model,
            BuildCharacterSystemPrompt(
                requestRoot,
                includeReceipt: true,
                includeTraitGuidance: true,
                includeRecentCultEvents: false,
                includeDirectReplyGuidance: invocationReply,
                includeWebSearchInstruction: false),
            BuildSidecarUserPrompt(requestRoot),
            BuildJsonSchemaResponseFormat("cotl_follower_receipt_reply", schema),
            Math.Min(900, GetInt(requestParts, "max_output_tokens", 2200)),
            GetString(requestParts, "reasoning_effort"));
    }

    private static bool IsInvocationReceiptReplyRequest(JsonElement requestRoot)
    {
        if (!requestRoot.TryGetProperty("context", out var context))
            return false;

        var receipt = GetString(context, "directive_bridge_response");
        if (receipt.StartsWith("Invocation:", StringComparison.OrdinalIgnoreCase))
            return true;

        return !string.IsNullOrWhiteSpace(GetString(context, "special_system_message"));
    }
}

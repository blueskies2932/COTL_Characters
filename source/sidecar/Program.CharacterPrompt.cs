using System.Text;
using System.Text.Json;

internal static partial class Program
{
    private sealed class CharacterPromptOptions
    {
        public bool IncludeReceipt { get; init; }
        public bool IncludeTraitGuidance { get; init; }
        public bool IncludeConversation { get; init; }
        public bool IncludeRecentCultEvents { get; init; } = true;
        public bool IncludeDirectReplyGuidance { get; init; }
        public bool IncludeWebSearchInstruction { get; init; } = true;
        public string PreparedLongTermMemory { get; init; } = string.Empty;
    }

    private static string BuildCharacterSystemPrompt(
        JsonElement requestRoot,
        bool includeReceipt = false,
        bool includeTraitGuidance = false,
        bool includeConversation = false,
        bool includeRecentCultEvents = true,
        bool includeDirectReplyGuidance = false,
        bool includeWebSearchInstruction = true,
        string preparedLongTermMemory = "")
    {
        return BuildCharacterSystemPrompt(
            requestRoot,
            new CharacterPromptOptions
            {
                IncludeReceipt = includeReceipt,
                IncludeTraitGuidance = includeTraitGuidance,
                IncludeConversation = includeConversation,
                IncludeRecentCultEvents = includeRecentCultEvents,
                IncludeDirectReplyGuidance = includeDirectReplyGuidance,
                IncludeWebSearchInstruction = includeWebSearchInstruction,
                PreparedLongTermMemory = preparedLongTermMemory ?? string.Empty
            });
    }

    private static string BuildCharacterSystemPrompt(JsonElement requestRoot, CharacterPromptOptions options)
    {
        var context = requestRoot.GetProperty("context");
        var builder = new StringBuilder();
        var specialSystemMessage = GetString(context, "special_system_message");

        AppendCharacterIdentitySection(builder, context, options);
        AppendCharacterWorldStateSection(builder, context);
        AppendCharacterRecentEventsSection(builder, context, options);
        AppendCharacterCurrentConversationSection(builder, context, options);
        AppendCharacterLongTermHistorySection(builder, context, options.PreparedLongTermMemory);
        AppendCharacterSettingSection(builder, context, options);
        AppendCharacterReceiptSection(builder, context, options);
        AppendCharacterCurrentSituationSection(builder, specialSystemMessage);

        return builder.ToString().Trim();
    }
}

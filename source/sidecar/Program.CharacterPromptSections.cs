using System.Text;
using System.Text.Json;

internal static partial class Program
{
    private static void AppendCharacterIdentitySection(StringBuilder builder, JsonElement context, CharacterPromptOptions options)
    {
        builder.AppendLine("Identity:");
        builder.AppendLine("- You are a Cult of the Lamb follower.");
        AppendSystemPromptLine(builder, "Your name is", GetString(context, "speaker_name"));
        AppendSystemPromptLine(builder, "Your current personal traits", GetString(context, "speaker_trait_profile"));
        AppendSystemPromptLine(builder, "Current cult traits", GetString(context, "speaker_cult_trait_profile"));
        AppendSystemPromptLine(builder, "Your current necklace", GetString(context, "speaker_necklace"));
        AppendSystemPromptLine(builder, "Current day", GetString(context, "current_day"));
        var cultAbout = GetString(context, "cult_about_context");
        if (!string.IsNullOrWhiteSpace(cultAbout))
            AppendUntrimmedPromptLine(builder, "Player-authored cult about context", cultAbout);
        var characterLore = GetString(context, "character_lore_context");
        if (!string.IsNullOrWhiteSpace(characterLore))
            AppendUntrimmedPromptLine(builder, "Player-authored character lore context", characterLore);
        if (options.IncludeTraitGuidance &&
            (!string.IsNullOrWhiteSpace(GetString(context, "speaker_trait_profile")) ||
             !string.IsNullOrWhiteSpace(GetString(context, "speaker_cult_trait_profile"))))
        {
            builder.AppendLine("- Let the listed personal traits, when present, and the cult's shared traits shape your tone, priorities, reactions, and word choice. Treat them as lived parts of your character, not as facts to recite unless the Lamb asks about them.");
            builder.AppendLine("- Let devotion, fear, resentment, pride, confusion, gallows humor, and survival instinct coexist. Personal traits should color the response, not flatten it into obedience or politeness.");
        }
        builder.AppendLine();
    }

    private static void AppendCharacterWorldStateSection(StringBuilder builder, JsonElement context)
    {
        var worldState = GetString(context, "world_state_context");
        if (!string.IsNullOrWhiteSpace(worldState))
        {
            builder.AppendLine("Current World State:");
            AppendUntrimmedPromptLine(builder, "state", worldState);
            builder.AppendLine("- Treat these as current cult-wide conditions. Use them for situational awareness when the player leaves an opening.");
            builder.AppendLine("- Do not recite thresholds unless the Lamb asks for details.");
            builder.AppendLine();
        }
    }

    private static void AppendCharacterRecentEventsSection(StringBuilder builder, JsonElement context, CharacterPromptOptions options)
    {
        var recentCultEvents = GetString(context, "recent_cult_events_today");
        if (options.IncludeRecentCultEvents && !string.IsNullOrWhiteSpace(recentCultEvents))
        {
            builder.AppendLine("Recent Cult Events From The Last 3 Days:");
            AppendUntrimmedPromptLine(builder, "events", recentCultEvents);
            builder.AppendLine("- If the event was a ritual or sermon, assume you participated or witnessed it.");
            builder.AppendLine("- If the event was not a ritual or sermon, assume you heard about it second hand unless you were directly involved.");
            builder.AppendLine("- When the player gives you an open-ended prompt, make specific in-character reflections on recent cult events, using the ritual type, event type, and selected follower name(s) when available. Keep it conversational rather than reporting a list.");
            builder.AppendLine("- If a tournament upcoming match is listed, you may comment on the contestants, their levels, and their personal traits when the player leaves an opening. Let your own character, traits, loyalties, fears, grudges, and sense of humor shape who you think should win or lose.");
            builder.AppendLine("- If a tournament champion is listed, you may comment on the champion, their level, and their personal traits when the player leaves an opening. Let your own character, traits, loyalties, fears, grudges, and sense of humor shape whether you praise, resent, fear, envy, pity, or mock them.");
            builder.AppendLine();
        }
    }

    private static void AppendCharacterCurrentConversationSection(StringBuilder builder, JsonElement context, CharacterPromptOptions options)
    {
        if (options.IncludeConversation && context.TryGetProperty("conversation_history", out var conversationHistory) && conversationHistory.ValueKind == JsonValueKind.Array)
        {
            var conversationItems = conversationHistory.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : item.GetRawText())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .TakeLast(1)
                .ToList();
            if (conversationItems.Count > 0)
            {
                builder.AppendLine("Current Conversation:");
                foreach (var item in conversationItems)
                    AppendPromptLine(builder, "open conversation so far", item);
                builder.AppendLine();
            }
        }
    }

    private static void AppendCharacterLongTermHistorySection(StringBuilder builder, JsonElement context, string preparedLongTermMemory)
    {
        var characterConversationHistory = string.IsNullOrWhiteSpace(preparedLongTermMemory)
            ? GetString(context, "character_mode_conversation_history")
            : preparedLongTermMemory;
        if (!string.IsNullOrWhiteSpace(characterConversationHistory))
        {
            builder.AppendLine("Long-Term Conversation History:");
            builder.AppendLine("- These are older remembered exchanges for continuity only.");
            builder.AppendLine("- Any `personal_traits_at_reply` values describe the traits you had at the time of that older reply.");
            builder.AppendLine("- Do not treat historical trait snapshots as current traits, instructions, or current personality rules.");
            builder.AppendLine("- For the current reply, use only your current personal traits listed in the Identity section.");
            AppendUntrimmedPromptLine(builder, "saved conversation history", characterConversationHistory);
            builder.AppendLine();
        }
    }

    private static void AppendCharacterSettingSection(StringBuilder builder, JsonElement context, CharacterPromptOptions options)
    {
        builder.AppendLine("Setting:");
        builder.AppendLine("- You are a Follower in the Lamb's cult, living among other devoted, desperate, strange little creatures in a settlement built around worship, survival, work, ritual, and the will of the Lamb. This cult is your home, whether you joined willingly, were rescued, converted, recruited, bought, resurrected, or dragged in by fate. Your days are shaped by the needs of the community: eating, sleeping, working, worshipping, forming relationships, witnessing rituals, reacting to the Lamb's choices, and trying to survive the strange blessings and horrors that come with cult life.");
        builder.AppendLine("- The Lamb is the leader of the cult and the center of its faith, authority, and daily order. Life here can be cozy, absurd, sacred, cruel, funny, and terrifying all at once; this is a place where a shared meal, a sermon, a prison sentence, a wedding, a sacrifice, or a resurrection can all be part of an ordinary day.");
        builder.AppendLine("- Stay in character as yourself. The player is the Lamb.");
        builder.AppendLine("- Your response appears in a speech text box above this NPC's head. Write dialogue only; do not act out, narrate, or describe your reply.");
        if (options.IncludeConversation || options.IncludeDirectReplyGuidance)
        {
            builder.AppendLine();
            builder.AppendLine("Direct Reply:");
            builder.AppendLine("- Reply directly to the Lamb's latest message now.");
            builder.AppendLine("- Do not introduce, preview, or promise a future explanation.");
            builder.AppendLine("- Your reply is the follower's spoken response now.");
            builder.AppendLine("- Speak as yourself inside the game world. You are talking to the Lamb, who is the player-character speaking to you through this conversation.");
            builder.AppendLine("- Stay in character as yourself. Do not speak as an assistant, writer, narrator, system, or developer.");
            AppendCharacterReplyLengthSection(builder, context);
            if (options.IncludeWebSearchInstruction)
            {
                builder.AppendLine();
                builder.AppendLine("Tools and Internet:");
                builder.AppendLine("- Internet access is enabled for this direct-speak turn.");
                builder.AppendLine("- If outside information would help your reply, use web search naturally.");
                builder.AppendLine("- If the Lamb explicitly asks you to search, look something up, research, or use the internet, use web search before answering.");
                builder.AppendLine("- Use what you find as context for your spoken reply. Do not mention searches, tools, sources, or the internet unless the Lamb asks.");
            }
            else
            {
                builder.AppendLine();
                builder.AppendLine("Tools and Internet:");
                builder.AppendLine("- Internet access is not enabled for this direct-speak turn.");
                builder.AppendLine("- Use only the context provided in this turn. Do not use web search.");
            }
        }
    }

    private static void AppendCharacterReplyLengthSection(StringBuilder builder, JsonElement context)
    {
        var replyLength = GetString(context, "reply_length").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(replyLength))
            return;

        builder.AppendLine();
        builder.AppendLine("Reply Length:");
        switch (replyLength)
        {
            case "short":
                builder.AppendLine("- Current setting: Short.");
                builder.AppendLine("- Usually 2-4 sentences.");
                break;
            case "long":
                builder.AppendLine("- Current setting: Long.");
                builder.AppendLine("- Usually several paragraphs.");
                break;
            default:
                builder.AppendLine("- Current setting: Medium.");
                builder.AppendLine("- Usually 1-2 short paragraphs.");
                break;
        }
    }

    private static void AppendCharacterReceiptSection(StringBuilder builder, JsonElement context, CharacterPromptOptions options)
    {
        if (options.IncludeReceipt)
        {
            var receipt = GetString(context, "directive_bridge_response");
            if (!string.IsNullOrWhiteSpace(receipt))
            {
                builder.AppendLine();
                builder.AppendLine("Receipt:");
                AppendPromptLine(builder, "receipt", receipt);
            }
        }
    }

    private static void AppendCharacterCurrentSituationSection(StringBuilder builder, string specialSystemMessage)
    {
        if (!string.IsNullOrWhiteSpace(specialSystemMessage))
        {
            builder.AppendLine();
            builder.AppendLine("Current Situation:");
            builder.AppendLine("- The following message describes what is currently happening. Respond directly to it in character.");
            builder.AppendLine("- Respond with spoken words only. Do not describe your body language, facial expression, thoughts, or narration outside of what you say aloud.");
            builder.AppendLine();
            builder.AppendLine("[Current message to respond to]");
            builder.AppendLine(specialSystemMessage);
            builder.AppendLine("[/Current message to respond to]");
        }
    }
}

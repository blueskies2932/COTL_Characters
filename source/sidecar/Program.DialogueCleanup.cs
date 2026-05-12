using System.Text.Json;
using System.Text.RegularExpressions;

internal static partial class Program
{
    private static string NormalizeDirectSpeakReply(string text)
    {
        var reply = (text ?? string.Empty).Trim();
        reply = Regex.Replace(reply, @"\s*\u3010[^\u3011]*\u3011", string.Empty);
        reply = Regex.Replace(reply, @"\s*\[\d+\]", string.Empty);
        reply = Regex.Replace(reply, @"\s*\(https?://[^\s)]+\)", string.Empty, RegexOptions.IgnoreCase);
        reply = Regex.Replace(reply, @"https?://\S+", string.Empty, RegexOptions.IgnoreCase);
        reply = Regex.Replace(reply, @"[ \t]{2,}", " ").Trim();
        if (reply.Length >= 2 &&
            ((reply[0] == '"' && reply[^1] == '"') ||
             (reply[0] == '\'' && reply[^1] == '\'')))
        {
            reply = reply.Substring(1, reply.Length - 2).Trim();
        }

        return reply;
    }

    private static bool ShouldRunDialogueEditorialPass(string reply)
    {
        if (string.IsNullOrWhiteSpace(reply))
            return false;

        if (Regex.IsMatch(reply, @"^\s*\[[^\]]*\b(lowers|raises|touches|looks|glances|smiles|frowns|laughs|whispers|mutters|says|replies|steps|turns|tilts|pauses|sighs|shudders|trembles|stares|gestures)\b[^\]]*\]", RegexOptions.IgnoreCase | RegexOptions.Singleline))
            return true;
        if (Regex.IsMatch(reply, @"\[(?:[^\]]{0,80})\b(he|she|they|the follower|the npc|subject x|raven)\b(?:[^\]]{0,120})\]", RegexOptions.IgnoreCase | RegexOptions.Singleline))
            return true;
        if (Regex.IsMatch(reply, @"\b(he|she|they|the follower|the npc|subject x|raven)\s+(lowers|raises|touches|looks|glances|smiles|frowns|laughs|whispers|mutters|says|replies|steps|turns|tilts|pauses|sighs|shudders|trembles|stares|gestures)\b", RegexOptions.IgnoreCase))
            return true;

        return false;
    }

    private static async Task<string> SendDialogueEditorialPass(JsonElement requestRoot, string model, string draft)
    {
        if (string.IsNullOrWhiteSpace(draft))
            return string.Empty;

        var requestParts = requestRoot.GetProperty("response_format");
        var systemPrompt =
            "Editorial Pass:\n" +
            "- Your first draft used formatting that does not fit the in-game speech text box.\n" +
            "- Keep the same meaning, intent, character voice, and situational awareness from the draft.\n" +
            "- Rewrite the draft as spoken dialogue suitable for a text box above your NPC's head.\n" +
            "- Remove bracketed narration, stage directions, action tags, third-person self-description, and prose that describes what your body, face, or posture is doing.\n" +
            "- Do not add new facts or new events.\n" +
            "- Do not change what the reply is saying.";
        var userPrompt =
            "Original draft:\n" +
            draft.Trim();
        var aiRequest = BuildTextAiRequestFromPrompts(
            model,
            systemPrompt,
            userPrompt,
            Math.Max(1200, GetInt(requestParts, "max_output_tokens", 2200)),
            GetString(requestParts, "reasoning_effort"),
            enableWebSearch: false,
            requireWebSearch: false);

        return await SendAiRequest(aiRequest);
    }
}

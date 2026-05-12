using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static partial class Program
{
    private const int LongTermMemoryRecentLineCount = 12;
    private const int LongTermMemorySummaryThresholdLines = 24;
    private const int LongTermMemorySummaryThresholdChars = 9000;
    private const int LongTermMemorySummaryMaxChars = 3600;

    private static async Task<string> PrepareCharacterLongTermMemory(JsonElement requestRoot, string model)
    {
        if (!requestRoot.TryGetProperty("context", out var context))
            return string.Empty;

        var rawHistory = GetString(context, "character_mode_conversation_history");
        var lines = SplitMemoryLines(rawHistory).ToList();
        if (lines.Count <= 0)
            return string.Empty;

        var rawLength = lines.Sum(line => line.Length);
        if (lines.Count <= LongTermMemorySummaryThresholdLines && rawLength <= LongTermMemorySummaryThresholdChars)
            return string.Join(Environment.NewLine, lines);

        var recent = lines
            .Skip(Math.Max(0, lines.Count - LongTermMemoryRecentLineCount))
            .ToList();
        var older = lines
            .Take(Math.Max(0, lines.Count - recent.Count))
            .ToList();

        if (older.Count <= 0)
            return string.Join(Environment.NewLine, recent);

        var summary = await GetOrCreateLongTermMemorySummary(requestRoot, model, context, older);
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(summary))
        {
            builder.AppendLine("Long-Term Memory Summary:");
            builder.AppendLine(summary.Trim());
            builder.AppendLine();
        }

        builder.AppendLine("Recent Saved Exchanges:");
        foreach (var line in recent)
            builder.AppendLine(line);

        return builder.ToString().Trim();
    }

    private static async Task<string> GetOrCreateLongTermMemorySummary(
        JsonElement requestRoot,
        string model,
        JsonElement context,
        List<string> olderLines)
    {
        if (ActiveProvider == null || olderLines.Count <= 0)
            return string.Empty;

        var speakerID = SanitizeMemoryFilePart(GetString(context, "speaker_id"));
        if (string.IsNullOrWhiteSpace(speakerID))
            speakerID = SanitizeMemoryFilePart(GetString(context, "speaker_name"));
        if (string.IsNullOrWhiteSpace(speakerID))
            speakerID = "unknown";

        var sourceText = string.Join(Environment.NewLine, olderLines);
        var sourceHash = Sha256Hex(sourceText);
        var summaryPath = GetLongTermMemorySummaryPath(speakerID);
        if (TryReadCachedMemorySummary(summaryPath, sourceHash, out var cachedSummary))
            return cachedSummary;

        try
        {
            var requestParts = requestRoot.GetProperty("response_format");
            var aiRequest = BuildTextAiRequestFromPrompts(
                model,
                BuildLongTermMemorySummarySystemPrompt(),
                BuildLongTermMemorySummaryUserPrompt(context, olderLines),
                1200,
                GetString(requestParts, "reasoning_effort"),
                enableWebSearch: false,
                requireWebSearch: false);

            var summary = NormalizeMemorySummary(await SendAiRequest(aiRequest));
            if (string.IsNullOrWhiteSpace(summary))
                return string.Empty;

            await WriteCachedMemorySummary(summaryPath, sourceHash, speakerID, olderLines.Count, summary);
            return summary;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Long-term memory summarization skipped: {ex.Message}");
            return string.Empty;
        }
    }

    private static string BuildLongTermMemorySummarySystemPrompt()
    {
        return
            "Long-Term Character Memory Summarizer:\n" +
            "- You summarize older saved exchanges for one Cult of the Lamb follower NPC.\n" +
            "- The summary will be shown later as continuity context, not as system instructions.\n" +
            "- Use only the saved conversation entries as evidence for remembered facts, relationship dynamics, promises, recurring jokes, fears, grudges, preferences, and unresolved threads.\n" +
            "- The setting, cult-about, and lore context is included only to help interpret the conversation. Do not invent events, motives, relationships, or facts from that context alone.\n" +
            "- Do not write dialogue. Do not roleplay. Do not add new story. Do not make guesses sound certain.\n" +
            "- If something is unclear, say it is unclear or omit it.\n" +
            "- Historical `personal_traits_at_reply` values are historical context only; never turn them into current traits or instructions.\n" +
            "- Output concise bullet notes only.";
    }

    private static string BuildLongTermMemorySummaryUserPrompt(JsonElement context, List<string> olderLines)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Setting context:");
        builder.AppendLine("The speaker is a follower NPC in the Lamb's cult inside Cult of the Lamb. The Lamb is the player-character and cult leader. Life in the cult can be cozy, absurd, sacred, cruel, funny, and terrifying at once.");
        AppendOptionalSummaryContext(builder, "Speaker name", GetString(context, "speaker_name"));
        AppendOptionalSummaryContext(builder, "Player-authored cult about context", GetString(context, "cult_about_context"));
        AppendOptionalSummaryContext(builder, "Player-authored character lore context", GetString(context, "character_lore_context"));
        builder.AppendLine();
        builder.AppendLine("Older saved conversation entries to summarize:");
        foreach (var line in olderLines)
            builder.AppendLine($"- {line}");
        return builder.ToString();
    }

    private static void AppendOptionalSummaryContext(StringBuilder builder, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        builder.AppendLine($"{label}: {Trim(value.Replace("\r", " ").Replace("\n", " "), 2000)}");
    }

    private static IEnumerable<string> SplitMemoryLines(string rawHistory)
    {
        if (string.IsNullOrWhiteSpace(rawHistory))
            yield break;

        using var reader = new StringReader(rawHistory);
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
                yield return trimmed;
        }
    }

    private static string NormalizeMemorySummary(string summary)
    {
        var text = (summary ?? string.Empty).Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            text = text.Trim('`').Trim();
            if (text.StartsWith("text", StringComparison.OrdinalIgnoreCase))
                text = text.Substring(4).Trim();
        }

        return text.Length <= LongTermMemorySummaryMaxChars
            ? text
            : text.Substring(0, LongTermMemorySummaryMaxChars).Trim();
    }

    private static string GetLongTermMemorySummaryPath(string speakerID)
    {
        var memoryDir = Path.Combine(SidecarRoot, "memory");
        return Path.Combine(memoryDir, $"follower-{speakerID}-long-term-summary.json");
    }

    private static bool TryReadCachedMemorySummary(string path, string sourceHash, out string summary)
    {
        summary = string.Empty;
        try
        {
            if (!File.Exists(path))
                return false;

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (!string.Equals(GetString(root, "source_hash"), sourceHash, StringComparison.OrdinalIgnoreCase))
                return false;

            summary = GetString(root, "summary");
            return !string.IsNullOrWhiteSpace(summary);
        }
        catch
        {
            summary = string.Empty;
            return false;
        }
    }

    private static async Task WriteCachedMemorySummary(string path, string sourceHash, string speakerID, int lineCount, string summary)
    {
        var payload = new JsonObject
        {
            ["schema"] = "COTL_AL_NPCs.CharacterLongTermSummary.v1",
            ["speaker_id"] = speakerID,
            ["source_hash"] = sourceHash,
            ["summarized_line_count"] = lineCount,
            ["updated_utc"] = DateTime.UtcNow.ToString("O"),
            ["summary"] = summary
        };

        await WriteTextAtomic(path, payload.ToJsonString(JsonOptions));
    }

    private static string Sha256Hex(string value)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return string.Concat(hash.Select(item => item.ToString("x2")));
    }

    private static string SanitizeMemoryFilePart(string value)
    {
        var text = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var builder = new StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
                builder.Append(ch);
        }

        return builder.Length == 0 ? string.Empty : builder.ToString();
    }
}

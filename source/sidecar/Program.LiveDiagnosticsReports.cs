using System.Text;

internal static partial class Program
{
    private static string BuildStartHereReport(string liveDir, string sidecarRoot, List<GameEvent> events, GameEvent? latest, Dictionary<string, List<string>> byNpc)
    {
        var builder = Header("Diagnostics Start Here");
        builder.AppendLine("Purpose: fast orientation for the latest game-side AI state without digging through raw logs first.");
        builder.AppendLine();
        builder.AppendLine("Snapshot:");
        builder.AppendLine($"- events_loaded: {events.Count}");
        builder.AppendLine($"- tracked_ai_npcs: {byNpc.Count}");
        builder.AppendLine($"- failure_like_entries: {CollectFailureLines(events, 200).Count}");
        builder.AppendLine($"- ready_file: {DescribeFile(Path.Combine(sidecarRoot, "sidecar-ready.json"))}");
        builder.AppendLine($"- pending_requests: {CountFiles(Path.Combine(sidecarRoot, "requests"), "*.json")}");
        builder.AppendLine($"- responses: {CountFiles(Path.Combine(sidecarRoot, "responses"), "*.json")}");
        builder.AppendLine($"- archived_requests: {CountFiles(Path.Combine(sidecarRoot, "archive"), "*.json")}");
        if (latest != null)
            builder.AppendLine($"- latest_event: {latest.Time}; scope={latest.Scope}; background={latest.Background}; paused={latest.Paused}; special_event={latest.SpecialEvent}");
        builder.AppendLine();
        builder.AppendLine("Where to look next:");
        builder.AppendLine($"- NPC state: {Path.Combine(liveDir, "AI_NPCs.txt")}");
        builder.AppendLine($"- Failures/events: {Path.Combine(liveDir, "FailuresAndEvents.txt")}");
        builder.AppendLine($"- Sidecar traffic: {Path.Combine(liveDir, "SidecarTraffic.txt")}");
        builder.AppendLine($"- Character memory/current events: {Path.Combine(liveDir, "CharacterMemory.txt")}");
        builder.AppendLine($"- Per-NPC dossiers: {Path.Combine(liveDir, "ByNpc")}");
        builder.AppendLine();
        builder.AppendLine("Most recent tracked NPC states:");
        if (byNpc.Count == 0)
            builder.AppendLine("- none");
        foreach (var pair in byNpc.OrderBy(pair => pair.Key).Take(12))
            builder.AppendLine($"- {pair.Key}: {pair.Value.LastOrDefault() ?? "no data"}");
        builder.AppendLine();
        builder.AppendLine("Most recent failure-like entries:");
        var failures = CollectFailureLines(events, 8);
        if (failures.Count == 0)
            builder.AppendLine("- none in recent stream tail");
        foreach (var line in failures)
            builder.AppendLine($"- {Redact(Trim(line, 700))}");
        return builder.ToString();
    }

    private static string BuildRealtimeReport(string liveDir, string sidecarRoot, List<GameEvent> events, GameEvent? latest, Dictionary<string, List<string>> byNpc)
    {
        var builder = Header("Live AI Diagnostic Report");
        builder.AppendLine($"live_dir={liveDir}");
        builder.AppendLine($"sidecar_root={sidecarRoot}");
        builder.AppendLine($"events_loaded={events.Count}");
        if (latest != null)
        {
            builder.AppendLine($"latest_time={latest.Time}; scope={latest.Scope}; background={latest.Background}; paused={latest.Paused}; special_event={latest.SpecialEvent}");
        }
        builder.AppendLine();
        builder.AppendLine("Reports:");
        builder.AppendLine($"- Start here: {Path.Combine(liveDir, "StartHere.txt")}");
        builder.AppendLine($"- AI_NPCs: {Path.Combine(liveDir, "AI_NPCs.txt")}");
        builder.AppendLine($"- Failures/events: {Path.Combine(liveDir, "FailuresAndEvents.txt")}");
        builder.AppendLine($"- Sidecar traffic: {Path.Combine(liveDir, "SidecarTraffic.txt")}");
        builder.AppendLine($"- Character memory/current events: {Path.Combine(liveDir, "CharacterMemory.txt")}");
        builder.AppendLine($"- Per-NPC dossiers: {Path.Combine(liveDir, "ByNpc")}");
        builder.AppendLine();
        builder.AppendLine("Tracked NPCs:");
        foreach (var pair in byNpc.OrderBy(pair => pair.Key))
            builder.AppendLine($"- {pair.Key}: {pair.Value.LastOrDefault() ?? "no data"}");
        return builder.ToString();
    }

    private static string BuildAiNpcReport(Dictionary<string, List<string>> byNpc)
    {
        var builder = Header("AI NPC Focus Report");
        foreach (var pair in byNpc.OrderBy(pair => pair.Key))
        {
            builder.AppendLine($"== NPC {pair.Key} ==");
            foreach (var line in pair.Value.TakeLast(12))
                builder.AppendLine(line);
            builder.AppendLine();
        }
        if (byNpc.Count == 0)
            builder.AppendLine("No tracked AI NPCs in event stream.");
        return builder.ToString();
    }

    private static string BuildFailuresReport(List<GameEvent> events)
    {
        var builder = Header("Failures And Events Report");
        var lines = CollectFailureLines(events, 80);
        if (lines.Count == 0)
            builder.AppendLine("No failure-like stream entries in recent tail.");
        else
            foreach (var line in lines)
                builder.AppendLine(Redact(Trim(line, 1200)));
        return builder.ToString();
    }

    private static string BuildSidecarTrafficReport(string sidecarRoot)
    {
        var builder = Header("Sidecar Traffic Report");
        AppendFileBrief(builder, Path.Combine(sidecarRoot, "sidecar-ready.json"), "ready");
        AppendRecentFiles(builder, Path.Combine(sidecarRoot, "requests"), "*.json", "request", 4);
        AppendRecentFiles(builder, Path.Combine(sidecarRoot, "responses"), "*.json", "response", 4);
        AppendRecentFiles(builder, Path.Combine(sidecarRoot, "archive"), "*.json", "archive", 6);
        return builder.ToString();
    }

    private static string BuildCharacterMemoryReport(string sidecarRoot)
    {
        var builder = Header("Character Memory And Current Events Report");
        var saveRoot = Directory.GetParent(sidecarRoot)?.FullName ?? sidecarRoot;
        AppendFileBrief(builder, Path.Combine(saveRoot, "AiFollowers.json"), "ai-followers");
        AppendFileBrief(builder, Path.Combine(saveRoot, "CurrentEvents.json"), "current-events");
        AppendRecentFiles(builder, Path.Combine(sidecarRoot, "memory"), "*long-term-summary.json", "long-term-summary", 8);
        return builder.ToString();
    }

    private static string BuildFilesIndex(string liveDir, string sidecarRoot)
    {
        var builder = Header("Diagnostic Files Index");
        foreach (var path in new[]
        {
            Path.Combine(liveDir, "GameEventStream.jsonl"),
            Path.Combine(liveDir, "StartHere.txt"),
            Path.Combine(liveDir, "AIRealtimeReport.txt"),
            Path.Combine(liveDir, "AI_NPCs.txt"),
            Path.Combine(liveDir, "FailuresAndEvents.txt"),
            Path.Combine(liveDir, "SidecarTraffic.txt"),
            Path.Combine(liveDir, "CharacterMemory.txt"),
            Path.Combine(liveDir, "ByNpc", "Index.txt"),
            Path.Combine(sidecarRoot, "sidecar-ready.json")
        })
        {
            AppendFileBrief(builder, path, Path.GetFileName(path), includeContent: false);
        }
        return builder.ToString();
    }

    private static List<string> CollectFailureLines(List<GameEvent> events, int maxLines)
    {
        return events
            .SelectMany(item => new[] { item.Diagnostics, item.Followers })
            .Where(IsFailureLine)
            .TakeLast(maxLines)
            .ToList();
    }

    private static string BuildOneNpcReport(string npcKey, List<string> lines, string sidecarRoot)
    {
        var builder = Header($"NPC {npcKey} Diagnostic Dossier");
        builder.AppendLine("== Game Stream ==");
        foreach (var line in lines.TakeLast(24))
            builder.AppendLine(line);
        builder.AppendLine();
        builder.AppendLine("== Sidecar Files Mentioning This NPC Key ==");
        var terms = npcKey.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Concat(new[] { npcKey }).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var matches = GetRecentSidecarFiles(sidecarRoot, 36)
            .Where(file => FileMentionsAny(file.FullName, terms))
            .Take(10)
            .ToList();
        if (matches.Count == 0)
            builder.AppendLine("No recent sidecar files matched this key.");
        foreach (var file in matches)
        {
            builder.AppendLine($"{file.FullName}; changed={file.LastWriteTime:HH:mm:ss}; size={FormatBytes(file.Length)}");
            builder.AppendLine(Indent(ExtractMatchingLines(file.FullName, terms, 1400), "  "));
        }
        return builder.ToString();
    }
}

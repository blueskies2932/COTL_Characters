internal static partial class Program
{
    private static void OrganizeLiveDiagnostics(string sidecarRoot)
    {
        if (DateTime.UtcNow < nextDiagnosticsUtc)
            return;

        nextDiagnosticsUtc = DateTime.UtcNow.AddSeconds(10);

        try
        {
            var saveRoot = Directory.GetParent(sidecarRoot)?.FullName;
            if (string.IsNullOrWhiteSpace(saveRoot))
                return;

            var liveDir = Path.Combine(saveRoot, "LiveDiagnostics");
            var streamPath = Path.Combine(liveDir, "GameEventStream.jsonl");
            if (!File.Exists(streamPath))
                return;

            Directory.CreateDirectory(liveDir);
            var events = ReadTailLines(streamPath, 260);
            WriteDiagnosticsReports(liveDir, sidecarRoot, events);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Live diagnostics organization failed: {ex.Message}");
        }
    }

    private static void WriteDiagnosticsReports(string liveDir, string sidecarRoot, List<GameEvent> events)
    {
        var latest = events.LastOrDefault();
        var byNpc = BuildNpcMap(events);
        WriteTextAtomicSync(Path.Combine(liveDir, "StartHere.txt"), BuildStartHereReport(liveDir, sidecarRoot, events, latest, byNpc));
        WriteTextAtomicSync(Path.Combine(liveDir, "AIRealtimeReport.txt"), BuildRealtimeReport(liveDir, sidecarRoot, events, latest, byNpc));
        WriteTextAtomicSync(Path.Combine(liveDir, "AI_NPCs.txt"), BuildAiNpcReport(byNpc));
        WriteTextAtomicSync(Path.Combine(liveDir, "FailuresAndEvents.txt"), BuildFailuresReport(events));
        WriteTextAtomicSync(Path.Combine(liveDir, "SidecarTraffic.txt"), BuildSidecarTrafficReport(sidecarRoot));
        WriteTextAtomicSync(Path.Combine(liveDir, "CharacterMemory.txt"), BuildCharacterMemoryReport(sidecarRoot));
        WriteTextAtomicSync(Path.Combine(liveDir, "FilesIndex.txt"), BuildFilesIndex(liveDir, sidecarRoot));
        WritePerNpcReports(liveDir, sidecarRoot, byNpc);
    }
}

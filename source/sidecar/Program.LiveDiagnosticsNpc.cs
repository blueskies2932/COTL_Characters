internal static partial class Program
{
    private static void WritePerNpcReports(string liveDir, string sidecarRoot, Dictionary<string, List<string>> byNpc)
    {
        var directory = Path.Combine(liveDir, "ByNpc");
        Directory.CreateDirectory(directory);
        var index = Header("Per-NPC Diagnostic Index");
        foreach (var pair in byNpc.OrderBy(pair => pair.Key))
        {
            var fileName = $"{SanitizeFileName(pair.Key)}.txt";
            var path = Path.Combine(directory, fileName);
            index.AppendLine($"{pair.Key} -> {path}");
            WriteTextAtomicSync(path, BuildOneNpcReport(pair.Key, pair.Value, sidecarRoot));
        }
        if (byNpc.Count == 0)
            index.AppendLine("No tracked AI NPCs in event stream.");
        WriteTextAtomicSync(Path.Combine(directory, "Index.txt"), index.ToString());
    }

    private static Dictionary<string, List<string>> BuildNpcMap(List<GameEvent> events)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in events)
        {
            foreach (var follower in SplitFollowers(item.Followers))
            {
                var key = ExtractNpcKey(follower);
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                if (!map.TryGetValue(key, out var list))
                {
                    list = new List<string>();
                    map[key] = list;
                }
                list.Add($"{item.Time} {follower}");
                if (list.Count > 80)
                    list.RemoveRange(0, list.Count - 80);
            }
        }
        return map;
    }

    private static List<string> SplitFollowers(string followers)
    {
        return (followers ?? string.Empty)
            .Split(new[] { " || " }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .ToList();
    }

    private static string ExtractNpcKey(string followerLine)
    {
        var id = ExtractAfter(followerLine, "id=", " ");
        var name = ExtractAfter(followerLine, "name=", " flags=");
        if (string.IsNullOrWhiteSpace(id))
            return string.Empty;
        return string.IsNullOrWhiteSpace(name) ? id : $"{id}_{name}";
    }

    private static string ExtractAfter(string text, string prefix, string terminator)
    {
        var start = text.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return string.Empty;
        start += prefix.Length;
        var end = text.IndexOf(terminator, start, StringComparison.OrdinalIgnoreCase);
        if (end < 0)
            end = text.Length;
        return text.Substring(start, end - start).Trim();
    }
}

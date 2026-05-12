using System.Text;

internal static partial class Program
{
    private static void AppendRecentFiles(StringBuilder builder, string directory, string pattern, string label, int maxFiles)
    {
        if (!Directory.Exists(directory))
        {
            builder.AppendLine($"{label}_dir missing: {directory}");
            return;
        }
        foreach (var file in new DirectoryInfo(directory).EnumerateFiles(pattern).OrderByDescending(file => file.LastWriteTimeUtc).Take(maxFiles))
            AppendFileBrief(builder, file.FullName, label);
    }

    private static void AppendFileBrief(StringBuilder builder, string path, string label, bool includeContent = true)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
        {
            builder.AppendLine($"{label}: missing {path}");
            return;
        }
        builder.AppendLine($"{label}: {path}; size={FormatBytes(info.Length)}; changed={info.LastWriteTime:HH:mm:ss.fff}");
        if (includeContent && info.Length > 0 && info.Length <= 2L * 1024L * 1024L)
            builder.AppendLine(Indent(Redact(ReadTail(path, 2600)), "  "));
    }

    private static string DescribeFile(string path)
    {
        var info = new FileInfo(path);
        return info.Exists
            ? $"present; size={FormatBytes(info.Length)}; changed={info.LastWriteTime:HH:mm:ss.fff}"
            : "missing";
    }

    private static int CountFiles(string directory, string pattern)
    {
        try
        {
            return Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory, pattern).Count()
                : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static string ReadTail(string path, int maxCharacters)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();
        return text.Length <= maxCharacters ? text : "[tail]\n" + text.Substring(text.Length - maxCharacters);
    }

    private static List<FileInfo> GetRecentSidecarFiles(string sidecarRoot, int maxFiles)
    {
        var files = new List<FileInfo>();
        foreach (var folder in new[] { "requests", "responses", "archive" })
        {
            var directory = Path.Combine(sidecarRoot, folder);
            if (!Directory.Exists(directory))
                continue;
            files.AddRange(new DirectoryInfo(directory).EnumerateFiles("*.json").OrderByDescending(file => file.LastWriteTimeUtc).Take(Math.Max(1, maxFiles / 3)));
        }
        return files.OrderByDescending(file => file.LastWriteTimeUtc).Take(maxFiles).ToList();
    }

    private static bool FileMentionsAny(string path, List<string> terms)
    {
        try
        {
            var text = File.ReadAllText(path);
            return terms.Any(term => text.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
        }
        catch
        {
            return false;
        }
    }

    private static string ExtractMatchingLines(string path, List<string> terms, int maxCharacters)
    {
        try
        {
            var lines = File.ReadAllLines(path);
            var matches = new List<string>();
            for (var index = 0; index < lines.Length; index++)
            {
                if (!terms.Any(term => lines[index].IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0))
                    continue;
                var start = Math.Max(0, index - 4);
                var end = Math.Min(lines.Length - 1, index + 4);
                for (var i = start; i <= end; i++)
                    matches.Add($"{i + 1}: {lines[i]}");
                matches.Add("---");
            }
            var text = string.Join(Environment.NewLine, matches);
            return text.Length <= maxCharacters ? Redact(text) : Redact(text.Substring(0, maxCharacters)) + "\n[matching excerpt truncated]";
        }
        catch (Exception ex)
        {
            return $"unreadable: {ex.Message}";
        }
    }

    private static void WriteTextAtomicSync(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = $"{path}.tmp";
        File.WriteAllText(temp, text);
        if (File.Exists(path))
            File.Delete(path);
        File.Move(temp, path);
    }
}

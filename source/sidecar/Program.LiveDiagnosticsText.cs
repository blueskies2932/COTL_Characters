using System.Text;

internal static partial class Program
{
    private static StringBuilder Header(string title)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"COTL_AL_NPCs {title}");
        builder.AppendLine($"Updated local: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        builder.AppendLine();
        return builder;
    }

    private static bool IsFailureLine(string text)
    {
        return ContainsAny(text, "failed", "failure", "error", "exception", "timeout", "blocked", "refused", "silent failure", "repair", "sidecar", "OpenAI");
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        return !string.IsNullOrWhiteSpace(value) && terms.Any(term => value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string((value ?? string.Empty).Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return cleaned.Length <= 64 ? cleaned : cleaned.Substring(0, 64);
    }

    private static string Indent(string value, string prefix)
    {
        return string.Join(Environment.NewLine, (value ?? string.Empty).Replace("\r", string.Empty).Split('\n').Select(line => prefix + line));
    }

    private static string Redact(string value)
    {
        if (ContainsAny(value, "api_key", "apikey", "secret", "token", "password", "credential", "authorization:", "bearer "))
            return "[redacted credential-like diagnostic text]";
        return value ?? string.Empty;
    }

    private static string FormatBytes(long length)
    {
        if (length >= 1024 * 1024)
            return $"{length / (1024 * 1024.0):0.0}MB";
        if (length >= 1024)
            return $"{length / 1024.0:0.0}KB";
        return $"{length}B";
    }
}

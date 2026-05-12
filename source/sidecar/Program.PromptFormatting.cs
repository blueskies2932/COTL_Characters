using System.Text;

internal static partial class Program
{
    private static void AppendSystemPromptLine(StringBuilder builder, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        builder.AppendLine($"- {label}: {TrimForPrompt(value, 1800)}");
    }

    private static void AppendUntrimmedPromptLine(StringBuilder builder, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        builder.AppendLine($"- {label}: {value}");
    }

    private static void AppendPromptLine(StringBuilder builder, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        builder.AppendLine($"- {label}: {TrimForPrompt(value, 1800)}");
    }

    private static string TrimForPrompt(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed.Substring(0, Math.Max(0, maxLength - 3)) + "...";
    }
}

using System.Text;
using System.Text.Json;

internal static partial class Program
{
    private static string BuildSidecarUserPrompt(JsonElement requestRoot)
    {
        var context = requestRoot.GetProperty("context");
        var builder = new StringBuilder();
        AppendUntrimmedPromptLine(builder, "player_text", GetString(context, "player_text"));

        return builder.ToString().Trim();
    }
}

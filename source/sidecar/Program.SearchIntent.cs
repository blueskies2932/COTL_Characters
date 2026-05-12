using System.Text.Json;

internal static partial class Program
{
    private static bool IsInternetAccessEnabled(JsonElement requestRoot)
    {
        return requestRoot.TryGetProperty("context", out var context)
            && GetBool(context, "internet_access_enabled");
    }

    private static bool IsExplicitSearchRequest(JsonElement requestRoot)
    {
        if (!requestRoot.TryGetProperty("context", out var context))
            return false;

        var playerText = GetString(context, "player_text").ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(playerText))
            return false;

        return playerText.Contains("internet")
            || playerText.Contains("web search")
            || playerText.Contains("search the web")
            || playerText.Contains("search online")
            || playerText.Contains("look online")
            || playerText.Contains("look it up")
            || playerText.Contains("look up ")
            || playerText.Contains("research ")
            || playerText.Contains("google ");
    }
}

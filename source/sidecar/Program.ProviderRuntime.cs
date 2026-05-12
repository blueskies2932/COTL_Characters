using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

internal static partial class Program
{
    private static async Task<string> SendAiRequest(AiRequest request)
    {
        var response = await PostAiRequest(request);
        return response.Text ?? ExtractOutputText(response.RawResponseText ?? string.Empty);
    }

    private static async Task<string> SendAiRequestWithSourceArchive(AiRequest request, string sourcesPath)
    {
        var response = await PostAiRequest(request);
        await WriteInternetSourceArchive(sourcesPath, response.RawResponseText ?? string.Empty);
        return response.Text ?? ExtractOutputText(response.RawResponseText ?? string.Empty);
    }

    private static async Task<AiResponse> PostAiRequest(AiRequest request)
    {
        if (ActiveProvider == null)
            throw new InvalidOperationException("AI provider is not configured.");

        var response = await ActiveProvider.GenerateAsync(request, CancellationToken.None);

        if (!response.Success)
            throw new InvalidOperationException(response.ErrorMessage ?? "AI provider request failed.");

        return response;
    }

    private static async Task<int> TestProviderConnection(AiProviderConfig config, string providerType, bool aiAvailable)
    {
        if (!aiAvailable || ActiveProvider == null)
        {
            Console.WriteLine("AI provider test failed: provider is not configured.");
            return 1;
        }

        var model = config.Model;
        if (string.IsNullOrWhiteSpace(model) && !string.Equals(providerType, "mock", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("AI provider test failed: model is not configured.");
            return 1;
        }

        var response = await ActiveProvider.GenerateAsync(new AiRequest
        {
            SystemPrompt = "You are a connection test responder.",
            Messages = new[]
            {
                new AiMessage
                {
                    Role = "user",
                    Content = "Reply with exactly: provider test ok"
                }
            },
            Model = model,
            MaxTokens = 32,
            Stream = false
        }, CancellationToken.None);

        if (!response.Success)
        {
            Console.WriteLine($"AI provider test failed: {response.ErrorType ?? AiErrorType.Unknown}: {Trim(response.ErrorMessage ?? string.Empty, 500)}");
            return 1;
        }

        var text = (response.Text ?? string.Empty).Trim();
        Console.WriteLine($"AI provider test succeeded: {ActiveProvider.DisplayName}");
        Console.WriteLine($"Native web search: {(ActiveProvider.SupportsNativeWebSearch ? "available" : "not available")}");
        if (!string.IsNullOrWhiteSpace(text))
            Console.WriteLine($"AI provider test response: {Trim(text, 160)}");
        return 0;
    }

    private static string ExtractOutputText(string responseText)
    {
        using var document = JsonDocument.Parse(responseText);
        var root = document.RootElement;
        if (root.TryGetProperty("output_text", out var directOutput))
            return directOutput.GetString() ?? string.Empty;

        if (root.TryGetProperty("choices", out var choices) &&
            choices.ValueKind == JsonValueKind.Array &&
            choices.GetArrayLength() > 0 &&
            choices[0].TryGetProperty("message", out var message) &&
            message.TryGetProperty("content", out var messageContent))
            return messageContent.GetString() ?? string.Empty;

        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
            return string.Empty;

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var contentItem in content.EnumerateArray())
            {
                var type = contentItem.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : string.Empty;
                if (string.Equals(type, "output_text", StringComparison.OrdinalIgnoreCase))
                    return contentItem.TryGetProperty("text", out var textElement) ? textElement.GetString() ?? string.Empty : string.Empty;
            }
        }

        return string.Empty;
    }

    private static async Task WriteInternetSourceArchive(string sourcesPath, string responseText)
    {
        if (string.IsNullOrWhiteSpace(sourcesPath))
            return;

        try
        {
            using var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;
            var sources = new List<JsonObject>();
            var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectUrlSources(root, sources, seenUrls);

            var archive = new JsonObject
            {
                ["schema_version"] = 1,
                ["created_utc"] = DateTime.UtcNow.ToString("O"),
                ["response_id"] = root.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? string.Empty : string.Empty,
                ["source_count"] = sources.Count,
                ["sources"] = new JsonArray(sources.ToArray())
            };

            var directory = Path.GetDirectoryName(sourcesPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var tempPath = $"{sourcesPath}.tmp";
            await File.WriteAllTextAsync(tempPath, archive.ToJsonString(JsonOptions));
            if (File.Exists(sourcesPath))
                File.Delete(sourcesPath);
            File.Move(tempPath, sourcesPath);
        }
        catch
        {
            // Source archives are for player inspection only; never fail the follower response.
        }
    }

    private static void CollectUrlSources(JsonElement element, List<JsonObject> sources, HashSet<string> seenUrls)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            string url = string.Empty;
            string title = string.Empty;
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                    continue;

                if (string.Equals(property.Name, "url", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(property.Name, "uri", StringComparison.OrdinalIgnoreCase))
                    url = property.Value.GetString() ?? string.Empty;
                else if (string.Equals(property.Name, "title", StringComparison.OrdinalIgnoreCase))
                    title = property.Value.GetString() ?? string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(url) && seenUrls.Add(url))
            {
                sources.Add(new JsonObject
                {
                    ["title"] = title,
                    ["url"] = url
                });
            }

            foreach (var property in element.EnumerateObject())
                CollectUrlSources(property.Value, sources, seenUrls);
            return;
        }

        if (element.ValueKind != JsonValueKind.Array)
            return;

        foreach (var item in element.EnumerateArray())
            CollectUrlSources(item, sources, seenUrls);
    }
}


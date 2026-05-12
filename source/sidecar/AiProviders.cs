using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

internal interface IAiProvider
{
    string DisplayName { get; }
    bool SupportsNativeWebSearch { get; }
    Task<AiResponse> GenerateAsync(AiRequest request, CancellationToken cancellationToken);
}

internal sealed class AiRequest
{
    public string? SystemPrompt { get; init; }
    public IReadOnlyList<AiMessage> Messages { get; init; } = Array.Empty<AiMessage>();
    public string? Model { get; init; }
    public double? Temperature { get; init; }
    public int? MaxTokens { get; init; }
    public bool Stream { get; init; }
    public AiResponseFormat? ResponseFormat { get; init; }
    public string? ReasoningEffort { get; init; }
    public bool EnableWebSearch { get; init; }
    public bool RequireWebSearch { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}

internal sealed class AiResponseFormat
{
    public string Name { get; init; } = "response";
    public bool Strict { get; init; }
    public JsonObject Schema { get; init; } = new();
}

internal sealed class AiMessage
{
    public string Role { get; init; } = "user";
    public string Content { get; init; } = string.Empty;
}

internal sealed class AiResponse
{
    public bool Success { get; init; }
    public string? Text { get; init; }
    public string? RawResponseText { get; init; }
    public string? ErrorMessage { get; init; }
    public AiErrorType? ErrorType { get; init; }
    public int? PromptTokens { get; init; }
    public int? CompletionTokens { get; init; }
    public int? TotalTokens { get; init; }
}

internal enum AiErrorType
{
    Unknown,
    MissingApiKey,
    InvalidApiKey,
    ProviderUnavailable,
    ModelNotFound,
    RateLimited,
    Timeout,
    MalformedResponse,
    ContentRejected,
    NetworkError
}

internal sealed class AiProviderSettings
{
    public string ProviderType { get; init; } = "openai";
    public string? ApiKey { get; init; }
    public string? BaseUrl { get; init; }
    public string? EndpointPath { get; init; }
    public string? Model { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(120);
    public double? Temperature { get; init; }
    public int? MaxTokens { get; init; }
    public Dictionary<string, string> Headers { get; init; } = new();
}

internal sealed class AiProviderConfig
{
    public string ProviderType { get; init; } = "openai";
    public string? ApiKeyEnvVar { get; init; }
    public string? ApiKeyFile { get; init; }
    public bool? RequiresApiKey { get; init; }
    public string? BaseUrl { get; init; }
    public string? EndpointPath { get; init; }
    public string? Model { get; init; }
    public int? TimeoutSeconds { get; init; }
    public double? Temperature { get; init; }
    public int? MaxTokens { get; init; }
    public Dictionary<string, string> Headers { get; init; } = new();

    public static AiProviderConfig Load(string root, string? explicitPath)
    {
        foreach (var path in EnumerateConfigCandidates(root, explicitPath))
        {
            try
            {
                if (!File.Exists(path))
                    continue;

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var config = JsonSerializer.Deserialize<AiProviderConfig>(File.ReadAllText(path), options);
                if (config != null)
                    return config;
            }
            catch
            {
                // Keep probing so a broken nearby draft does not stop the sidecar from using defaults.
            }
        }

        return new AiProviderConfig();
    }

    private static IEnumerable<string> EnumerateConfigCandidates(string root, string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
            yield return Path.GetFullPath(explicitPath);

        var current = new DirectoryInfo(root);
        while (current != null)
        {
            yield return Path.Combine(current.FullName, "AiProvider.json");
            yield return Path.Combine(current.FullName, "COTL_AL_NPCs", "AiProvider.json");
            current = current.Parent;
        }
    }
}

internal static class AiProviderFactory
{
    public static IAiProvider Create(AiProviderSettings settings)
    {
        var providerType = (settings.ProviderType ?? string.Empty).Trim().ToLowerInvariant();
        return providerType switch
        {
            "mock" => new MockProvider(),
            "anthropic" or "claude" => new AnthropicProvider(settings),
            "gemini" or "google" or "google-gemini" => new GeminiProvider(settings),
            "openai-compatible" or "openai_compatible" or "openrouter" or "lmstudio" or "lm-studio" or "ollama" =>
                new OpenAICompatibleProvider(settings),
            _ => new OpenAIProvider(settings)
        };
    }
}

internal sealed class OpenAIProvider : IAiProvider
{
    private const string DefaultResponsesUrl = "https://api.openai.com/v1/responses";
    private const string DefaultChatCompletionsUrl = "https://api.openai.com/v1/chat/completions";
    private readonly HttpClient http;
    private readonly string responsesUrl;
    private readonly string chatCompletionsUrl;
    private readonly string endpointPath;

    public OpenAIProvider(AiProviderSettings settings)
    {
        endpointPath = settings.EndpointPath ?? string.Empty;
        responsesUrl = NormalizeUrl(settings.BaseUrl, settings.EndpointPath, DefaultResponsesUrl, "/responses");
        chatCompletionsUrl = NormalizeUrl(settings.BaseUrl, "/chat/completions", DefaultChatCompletionsUrl, "/chat/completions");
        http = new HttpClient { Timeout = settings.Timeout };
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        AddCustomHeaders(http, settings.Headers);
    }

    public string DisplayName => "OpenAI";
    public bool SupportsNativeWebSearch => true;

    public async Task<AiResponse> GenerateAsync(AiRequest request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        if (string.IsNullOrWhiteSpace(model))
            return Fail(AiErrorType.ModelNotFound, "No model was configured for the OpenAI provider.");

        if (ShouldPreferChatCompletions(model, request))
            return await SendChatCompletionsRequest(request, model, cancellationToken);

        var response = await SendResponsesRequest(request, model, cancellationToken);
        if (CanRetryWithChatCompletions(response, request))
        {
            var chatResponse = await SendChatCompletionsRequest(request, model, cancellationToken);
            if (chatResponse.Success)
                return chatResponse;

            return Fail(
                chatResponse.ErrorType ?? response.ErrorType ?? AiErrorType.Unknown,
                $"OpenAI Responses failed: {Trim(response.ErrorMessage ?? string.Empty, 600)} | OpenAI Chat Completions failed: {Trim(chatResponse.ErrorMessage ?? string.Empty, 600)}");
        }

        return response;
    }

    private async Task<AiResponse> SendResponsesRequest(AiRequest request, string model, CancellationToken cancellationToken)
    {
        var input = new JsonArray();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
            input.Add(BuildResponsesInputMessage("system", request.SystemPrompt));
        foreach (var message in request.Messages)
            input.Add(BuildResponsesInputMessage(string.IsNullOrWhiteSpace(message.Role) ? "user" : message.Role, message.Content ?? string.Empty));

        var payload = new JsonObject
        {
            ["model"] = model,
            ["input"] = input,
            ["store"] = false
        };
        if (request.MaxTokens is { } maxTokens)
            payload["max_output_tokens"] = maxTokens;
        if (request.ResponseFormat != null)
        {
            payload["text"] = new JsonObject
            {
                ["format"] = BuildOpenAiResponsesFormat(request.ResponseFormat)
            };
        }

        if (request.EnableWebSearch)
        {
            payload["tools"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "web_search",
                    ["search_context_size"] = request.RequireWebSearch ? "high" : "medium"
                }
            };
            payload["include"] = new JsonArray("web_search_call.action.sources");
            payload["tool_choice"] = request.RequireWebSearch ? "required" : "auto";
        }

        if (SupportsReasoningModel(model) && !string.IsNullOrWhiteSpace(request.ReasoningEffort))
        {
            payload["reasoning"] = new JsonObject
            {
                ["effort"] = NormalizeReasoningEffortForProvider(model, request.ReasoningEffort)
            };
        }

        return await PostJson(http, responsesUrl, payload.ToJsonString(), cancellationToken);
    }

    private async Task<AiResponse> SendChatCompletionsRequest(AiRequest request, string model, CancellationToken cancellationToken)
    {
        var messages = new JsonArray();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(new JsonObject
            {
                ["role"] = "system",
                ["content"] = request.SystemPrompt
            });
        }

        foreach (var message in request.Messages)
        {
            messages.Add(new JsonObject
            {
                ["role"] = string.IsNullOrWhiteSpace(message.Role) ? "user" : message.Role,
                ["content"] = message.Content ?? string.Empty
            });
        }

        var payload = new JsonObject
        {
            ["model"] = model,
            ["messages"] = messages,
            ["stream"] = request.Stream,
            ["store"] = false
        };
        if (request.Temperature is { } temperature)
            payload["temperature"] = temperature;
        if (request.MaxTokens is { } maxTokens)
        {
            if (SupportsReasoningModel(model))
                payload["max_completion_tokens"] = maxTokens;
            else
                payload["max_tokens"] = maxTokens;
        }
        if (request.ResponseFormat != null)
            payload["response_format"] = BuildOpenAiChatResponseFormat(request.ResponseFormat);

        return await PostJson(http, chatCompletionsUrl, payload.ToJsonString(), cancellationToken);
    }

    private bool ShouldPreferChatCompletions(string model, AiRequest request)
    {
        if (request.EnableWebSearch)
            return false;

        if (!string.IsNullOrWhiteSpace(endpointPath) &&
            endpointPath.Contains("chat/completions", StringComparison.OrdinalIgnoreCase))
            return true;

        var normalized = model.Trim().ToLowerInvariant();
        return normalized.StartsWith("gpt-4", StringComparison.Ordinal) ||
               normalized.StartsWith("gpt-3.5", StringComparison.Ordinal);
    }

    private static bool CanRetryWithChatCompletions(AiResponse response, AiRequest request)
    {
        if (response.Success || request.EnableWebSearch)
            return false;

        return response.ErrorType == AiErrorType.ModelNotFound ||
               response.ErrorType == AiErrorType.MalformedResponse ||
               response.ErrorType == AiErrorType.Unknown ||
               ContainsAny(response.ErrorMessage, "unsupported", "not supported", "unknown parameter", "invalid_request_error");
    }

    private static bool ContainsAny(string? text, params string[] needles)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return needles.Any(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static JsonObject BuildResponsesInputMessage(string role, string text)
    {
        return new JsonObject
        {
            ["role"] = role,
            ["content"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "input_text",
                    ["text"] = text
                }
            }
        };
    }

    private static string NormalizeUrl(string? baseUrl, string? endpointPath, string fallback, string defaultPath)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return fallback;

        var trimmedBase = baseUrl.Trim().TrimEnd('/');
        var path = string.IsNullOrWhiteSpace(endpointPath) ? defaultPath : endpointPath.Trim();
        if (Uri.TryCreate(trimmedBase, UriKind.Absolute, out var absolute) &&
            absolute.AbsolutePath.EndsWith(path.TrimStart('/'), StringComparison.OrdinalIgnoreCase))
            return trimmedBase;

        return $"{trimmedBase}/{path.TrimStart('/')}";
    }

    internal static async Task<AiResponse> PostJson(HttpClient http, string url, string requestJson, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
            };
            using var response = await http.SendAsync(request, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Fail(MapError(response.StatusCode), $"{(int)response.StatusCode} {response.ReasonPhrase}: {Trim(responseText, 1200)}");

            return new AiResponse
            {
                Success = true,
                RawResponseText = responseText,
                Text = TryExtractCommonText(responseText)
            };
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return Fail(AiErrorType.Timeout, ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return Fail(AiErrorType.NetworkError, ex.Message);
        }
        catch (Exception ex)
        {
            return Fail(AiErrorType.Unknown, ex.Message);
        }
    }

    internal static AiResponse Fail(AiErrorType errorType, string message)
    {
        return new AiResponse
        {
            Success = false,
            ErrorType = errorType,
            ErrorMessage = message
        };
    }

    internal static AiErrorType MapError(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => AiErrorType.InvalidApiKey,
            HttpStatusCode.NotFound => AiErrorType.ModelNotFound,
            HttpStatusCode.TooManyRequests => AiErrorType.RateLimited,
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => AiErrorType.MalformedResponse,
            HttpStatusCode.ServiceUnavailable or HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout => AiErrorType.ProviderUnavailable,
            _ => AiErrorType.Unknown
        };
    }

    internal static string? TryExtractCommonText(string responseText)
    {
        try
        {
            using var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;
            if (root.TryGetProperty("output_text", out var outputText))
                return outputText.GetString();

            if (root.TryGetProperty("choices", out var choices) &&
                choices.ValueKind == JsonValueKind.Array &&
                choices.GetArrayLength() > 0 &&
                choices[0].TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var content))
                return content.GetString();

            if (root.TryGetProperty("content", out var anthropicContent) &&
                anthropicContent.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in anthropicContent.EnumerateArray())
                {
                    var type = item.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : string.Empty;
                    if (string.Equals(type, "tool_use", StringComparison.OrdinalIgnoreCase) &&
                        item.TryGetProperty("input", out var toolInput))
                        return toolInput.GetRawText();
                    if (string.Equals(type, "text", StringComparison.OrdinalIgnoreCase) &&
                        item.TryGetProperty("text", out var textElement))
                        return textElement.GetString();
                }
            }

            if (root.TryGetProperty("candidates", out var candidates) &&
                candidates.ValueKind == JsonValueKind.Array &&
                candidates.GetArrayLength() > 0 &&
                candidates[0].TryGetProperty("content", out var geminiContent) &&
                geminiContent.TryGetProperty("parts", out var parts) &&
                parts.ValueKind == JsonValueKind.Array)
            {
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var textElement))
                        return textElement.GetString();
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    internal static JsonObject BuildOpenAiResponsesFormat(AiResponseFormat format)
    {
        return new JsonObject
        {
            ["type"] = "json_schema",
            ["name"] = format.Name,
            ["strict"] = format.Strict,
            ["schema"] = NormalizeOpenAiStrictSchema(CloneJsonObject(format.Schema))
        };
    }

    internal static JsonObject BuildOpenAiChatResponseFormat(AiResponseFormat format)
    {
        return new JsonObject
        {
            ["type"] = "json_schema",
            ["json_schema"] = new JsonObject
            {
                ["name"] = format.Name,
                ["strict"] = format.Strict,
                ["schema"] = NormalizeOpenAiStrictSchema(CloneJsonObject(format.Schema))
            }
        };
    }

    internal static JsonObject CloneJsonObject(JsonObject source)
    {
        return JsonNode.Parse(source.ToJsonString()) as JsonObject ?? new JsonObject();
    }

    private static JsonObject NormalizeOpenAiStrictSchema(JsonObject schema)
    {
        NormalizeOpenAiStrictSchemaNode(schema);
        return schema;
    }

    private static void NormalizeOpenAiStrictSchemaNode(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            var type = ReadString(obj, "type");
            var properties = obj["properties"] as JsonObject;
            if (string.Equals(type, "object", StringComparison.OrdinalIgnoreCase))
            {
                if (properties == null)
                {
                    properties = new JsonObject();
                    obj["properties"] = properties;
                }

                obj["additionalProperties"] = false;
                var required = new JsonArray();
                foreach (var property in properties)
                    required.Add(property.Key);
                obj["required"] = required;
            }

            foreach (var property in obj.ToList())
                NormalizeOpenAiStrictSchemaNode(property.Value);
            return;
        }

        if (node is JsonArray array)
        {
            foreach (var item in array)
                NormalizeOpenAiStrictSchemaNode(item);
        }
    }

    private static string ReadString(JsonObject obj, string name)
    {
        if (obj.TryGetPropertyValue(name, out var node) &&
            node is JsonValue value &&
            value.TryGetValue<string>(out var text))
            return text ?? string.Empty;

        return string.Empty;
    }

    internal static bool SupportsReasoningModel(string model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return false;

        return model.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase)
            || model.StartsWith("o", StringComparison.OrdinalIgnoreCase);
    }

    internal static string NormalizeReasoningEffortForProvider(string model, string configuredEffort)
    {
        if (!string.IsNullOrWhiteSpace(model) && model.Contains("-pro", StringComparison.OrdinalIgnoreCase))
            return "high";

        return configuredEffort.Trim().ToLowerInvariant() switch
        {
            "none" or "minimal" or "low" or "medium" or "high" or "xhigh" => configuredEffort.Trim().ToLowerInvariant(),
            _ => "medium"
        };
    }

    internal static void AddCustomHeaders(HttpClient http, Dictionary<string, string> headers)
    {
        foreach (var (name, value) in headers)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
                continue;
            http.DefaultRequestHeaders.Remove(name);
            http.DefaultRequestHeaders.TryAddWithoutValidation(name, value);
        }
    }

    private static string Trim(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        return text[..maxLength] + "...";
    }
}

internal sealed class OpenAICompatibleProvider : IAiProvider
{
    private readonly HttpClient http;
    private readonly string chatCompletionsUrl;
    private readonly AiProviderSettings settings;

    public OpenAICompatibleProvider(AiProviderSettings settings)
    {
        this.settings = settings;
        chatCompletionsUrl = NormalizeChatCompletionsUrl(settings.BaseUrl, settings.EndpointPath);
        http = new HttpClient { Timeout = settings.Timeout };
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        OpenAIProvider.AddCustomHeaders(http, settings.Headers);
    }

    public string DisplayName => "OpenAI-compatible";
    public bool SupportsNativeWebSearch => IsOpenRouterProvider();

    public async Task<AiResponse> GenerateAsync(AiRequest request, CancellationToken cancellationToken)
    {
        var model = request.Model ?? settings.Model;
        if (string.IsNullOrWhiteSpace(model))
            return OpenAIProvider.Fail(AiErrorType.ModelNotFound, "No model was configured for the OpenAI-compatible provider.");

        var messages = new JsonArray();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(new JsonObject
            {
                ["role"] = "system",
                ["content"] = request.SystemPrompt
            });
        }

        foreach (var message in request.Messages)
        {
            messages.Add(new JsonObject
            {
                ["role"] = string.IsNullOrWhiteSpace(message.Role) ? "user" : message.Role,
                ["content"] = message.Content ?? string.Empty
            });
        }

        var payload = new JsonObject
        {
            ["model"] = model,
            ["messages"] = messages,
            ["stream"] = request.Stream
        };
        if ((request.Temperature ?? settings.Temperature) is { } temperature)
            payload["temperature"] = temperature;
        if ((request.MaxTokens ?? settings.MaxTokens) is { } maxTokens)
            payload["max_tokens"] = maxTokens;
        if (request.ResponseFormat != null)
            payload["response_format"] = OpenAIProvider.BuildOpenAiChatResponseFormat(request.ResponseFormat);
        if (IsOpenRouterProvider() && request.EnableWebSearch)
        {
            payload["tools"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "openrouter:web_search",
                    ["parameters"] = new JsonObject
                    {
                        ["search_context_size"] = request.RequireWebSearch ? "high" : "medium"
                    }
                }
            };
        }

        return await OpenAIProvider.PostJson(http, chatCompletionsUrl, payload.ToJsonString(), cancellationToken);
    }

    private bool IsOpenRouterProvider()
    {
        return string.Equals(settings.ProviderType, "openrouter", StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(settings.BaseUrl) &&
                settings.BaseUrl.Contains("openrouter.ai", StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeChatCompletionsUrl(string? baseUrl, string? endpointPath)
    {
        var trimmedBase = string.IsNullOrWhiteSpace(baseUrl)
            ? "http://localhost:1234/v1"
            : baseUrl.Trim().TrimEnd('/');
        var path = string.IsNullOrWhiteSpace(endpointPath) ? "/chat/completions" : endpointPath.Trim();
        return $"{trimmedBase}/{path.TrimStart('/')}";
    }
}

internal sealed class AnthropicProvider : IAiProvider
{
    private const string DefaultAnthropicUrl = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";
    private readonly HttpClient http;
    private readonly string messagesUrl;
    private readonly AiProviderSettings settings;

    public AnthropicProvider(AiProviderSettings settings)
    {
        this.settings = settings;
        messagesUrl = NormalizeAnthropicUrl(settings.BaseUrl, settings.EndpointPath);
        http = new HttpClient { Timeout = settings.Timeout };
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            http.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", settings.ApiKey);
        http.DefaultRequestHeaders.TryAddWithoutValidation("anthropic-version", AnthropicVersion);
        OpenAIProvider.AddCustomHeaders(http, settings.Headers);
    }

    public string DisplayName => "Anthropic Claude";
    public bool SupportsNativeWebSearch => true;

    public async Task<AiResponse> GenerateAsync(AiRequest request, CancellationToken cancellationToken)
    {
        var model = request.Model ?? settings.Model;
        if (string.IsNullOrWhiteSpace(model))
            return OpenAIProvider.Fail(AiErrorType.ModelNotFound, "No model was configured for the Anthropic provider.");

        var systemPrompt = request.SystemPrompt ?? string.Empty;
        var messages = new JsonArray();
        foreach (var message in request.Messages)
        {
            var role = NormalizeAnthropicRole(message.Role);
            if (string.Equals(role, "system", StringComparison.OrdinalIgnoreCase))
            {
                systemPrompt = string.IsNullOrWhiteSpace(systemPrompt)
                    ? message.Content ?? string.Empty
                    : systemPrompt + "\n" + (message.Content ?? string.Empty);
                continue;
            }

            messages.Add(new JsonObject
            {
                ["role"] = role,
                ["content"] = message.Content ?? string.Empty
            });
        }

        if (messages.Count == 0)
        {
            messages.Add(new JsonObject
            {
                ["role"] = "user",
                ["content"] = string.Empty
            });
        }

        var payload = new JsonObject
        {
            ["model"] = model,
            ["max_tokens"] = Math.Max(1, request.MaxTokens ?? settings.MaxTokens ?? 1024),
            ["messages"] = messages
        };
        if (!string.IsNullOrWhiteSpace(systemPrompt))
            payload["system"] = systemPrompt;
        if ((request.Temperature ?? settings.Temperature) is { } temperature)
            payload["temperature"] = temperature;

        var tools = new JsonArray();
        if (request.ResponseFormat != null)
        {
            tools.Add(new JsonObject
            {
                ["name"] = request.ResponseFormat.Name,
                ["description"] = "Return the structured result for this request.",
                ["input_schema"] = OpenAIProvider.CloneJsonObject(request.ResponseFormat.Schema)
            });
            payload["tools"] = tools;
            payload["tool_choice"] = new JsonObject
            {
                ["type"] = "tool",
                ["name"] = request.ResponseFormat.Name
            };
        }
        else if (request.EnableWebSearch)
        {
            tools.Add(new JsonObject
            {
                ["type"] = "web_search_20250305",
                ["name"] = "web_search",
                ["max_uses"] = request.RequireWebSearch ? 5 : 3
            });
            payload["tools"] = tools;
            if (request.RequireWebSearch)
            {
                payload["tool_choice"] = new JsonObject
                {
                    ["type"] = "tool",
                    ["name"] = "web_search"
                };
            }
        }

        return await OpenAIProvider.PostJson(http, messagesUrl, payload.ToJsonString(), cancellationToken);
    }

    private static string NormalizeAnthropicRole(string role)
    {
        if (string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase))
            return "assistant";
        if (string.Equals(role, "system", StringComparison.OrdinalIgnoreCase))
            return "system";
        return "user";
    }

    private static string NormalizeAnthropicUrl(string? baseUrl, string? endpointPath)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return DefaultAnthropicUrl;

        var trimmedBase = baseUrl.Trim().TrimEnd('/');
        if (trimmedBase.EndsWith("/messages", StringComparison.OrdinalIgnoreCase))
            return trimmedBase;

        var path = string.IsNullOrWhiteSpace(endpointPath) ? "/messages" : endpointPath.Trim();
        return $"{trimmedBase}/{path.TrimStart('/')}";
    }
}

internal sealed class GeminiProvider : IAiProvider
{
    private const string DefaultGeminiBaseUrl = "https://generativelanguage.googleapis.com";
    private readonly HttpClient http;
    private readonly AiProviderSettings settings;

    public GeminiProvider(AiProviderSettings settings)
    {
        this.settings = settings;
        http = new HttpClient { Timeout = settings.Timeout };
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            http.DefaultRequestHeaders.TryAddWithoutValidation("x-goog-api-key", settings.ApiKey);
        OpenAIProvider.AddCustomHeaders(http, settings.Headers);
    }

    public string DisplayName => "Google Gemini";
    public bool SupportsNativeWebSearch => true;

    public async Task<AiResponse> GenerateAsync(AiRequest request, CancellationToken cancellationToken)
    {
        var model = request.Model ?? settings.Model;
        if (string.IsNullOrWhiteSpace(model))
            return OpenAIProvider.Fail(AiErrorType.ModelNotFound, "No model was configured for the Gemini provider.");

        var url = BuildGeminiUrl(model);
        var contents = new JsonArray();
        foreach (var message in request.Messages)
        {
            contents.Add(new JsonObject
            {
                ["role"] = NormalizeGeminiRole(message.Role),
                ["parts"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["text"] = message.Content ?? string.Empty
                    }
                }
            });
        }

        if (contents.Count == 0)
        {
            contents.Add(new JsonObject
            {
                ["role"] = "user",
                ["parts"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["text"] = string.Empty
                    }
                }
            });
        }

        var payload = new JsonObject
        {
            ["contents"] = contents
        };
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            payload["systemInstruction"] = new JsonObject
            {
                ["parts"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["text"] = request.SystemPrompt
                    }
                }
            };
        }

        var generationConfig = new JsonObject();
        if ((request.MaxTokens ?? settings.MaxTokens) is { } maxTokens)
            generationConfig["maxOutputTokens"] = maxTokens;
        if ((request.Temperature ?? settings.Temperature) is { } temperature)
            generationConfig["temperature"] = temperature;
        if (request.ResponseFormat != null)
        {
            generationConfig["responseFormat"] = new JsonObject
            {
                ["text"] = new JsonObject
                {
                    ["mimeType"] = "application/json",
                    ["schema"] = ConvertJsonSchemaForGemini(request.ResponseFormat.Schema)
                }
            };
        }
        if (generationConfig.Count > 0)
            payload["generationConfig"] = generationConfig;
        if (request.EnableWebSearch)
        {
            payload["tools"] = new JsonArray
            {
                new JsonObject
                {
                    ["google_search"] = new JsonObject()
                }
            };
        }

        return await OpenAIProvider.PostJson(http, url, payload.ToJsonString(), cancellationToken);
    }

    private string BuildGeminiUrl(string model)
    {
        var baseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl)
            ? DefaultGeminiBaseUrl
            : settings.BaseUrl.Trim().TrimEnd('/');
        var endpoint = string.IsNullOrWhiteSpace(settings.EndpointPath)
            ? $"/v1beta/models/{model}:generateContent"
            : settings.EndpointPath.Trim().Replace("{model}", model, StringComparison.OrdinalIgnoreCase);
        return $"{baseUrl}/{endpoint.TrimStart('/')}";
    }

    private static string NormalizeGeminiRole(string role)
    {
        return string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase)
            ? "model"
            : "user";
    }

    private static JsonNode ConvertJsonSchemaForGemini(JsonNode node)
    {
        var clone = JsonNode.Parse(node.ToJsonString());
        if (clone == null)
            return new JsonObject();

        NormalizeGeminiSchemaTypes(clone);
        return clone;
    }

    private static void NormalizeGeminiSchemaTypes(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            if (obj.TryGetPropertyValue("type", out var typeNode) &&
                typeNode is JsonValue typeValue &&
                typeValue.TryGetValue<string>(out var type))
                obj["type"] = type.ToUpperInvariant();

            foreach (var property in obj.ToList())
            {
                if (property.Value != null)
                    NormalizeGeminiSchemaTypes(property.Value);
            }
            return;
        }

        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item != null)
                    NormalizeGeminiSchemaTypes(item);
            }
        }
    }
}

internal sealed class MockProvider : IAiProvider
{
    public string DisplayName => "Mock";
    public bool SupportsNativeWebSearch => false;

    public Task<AiResponse> GenerateAsync(AiRequest request, CancellationToken cancellationToken)
    {
        const string text = "AI provider connection test successful.";
        return Task.FromResult(new AiResponse
        {
            Success = true,
            Text = text,
            RawResponseText = "{\"output_text\":\"AI provider connection test successful.\"}"
        });
    }
}

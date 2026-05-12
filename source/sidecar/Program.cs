using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

internal static partial class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static IAiProvider? ActiveProvider;
    private static DateTime nextDiagnosticsUtc = DateTime.MinValue;
    private static string SidecarRoot = string.Empty;

    private static async Task<int> Main(string[] args)
    {
        var root = GetArg(args, "--root");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("Usage: CotlAiNpcSidecar --root <.../BepInEx/config/COTL_AL_NPCs/Saves/<save>/Sidecar> [--once] [--parent-pid <pid>]");
            return 2;
        }

        root = Path.GetFullPath(root);
        SidecarRoot = root;
        var once = args.Any(arg => string.Equals(arg, "--once", StringComparison.OrdinalIgnoreCase));
        var parentPid = ParseInt(GetArg(args, "--parent-pid"), 0);
        var requestDir = Path.Combine(root, "requests");
        var responseDir = Path.Combine(root, "responses");
        var archiveDir = Path.Combine(root, "archive");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(requestDir);
        Directory.CreateDirectory(responseDir);
        Directory.CreateDirectory(archiveDir);

        var providerConfig = AiProviderConfig.Load(root, GetArg(args, "--provider-config"));
        var providerType = GetArg(args, "--provider") ?? providerConfig.ProviderType;
        var apiKey = ResolveProviderApiKey(root, GetArg(args, "--key-file"), providerConfig, providerType);
        var requiresApiKey = ProviderRequiresApiKey(providerType, providerConfig);
        var aiAvailable = !requiresApiKey || !string.IsNullOrWhiteSpace(apiKey);
        if (!aiAvailable)
        {
            Console.WriteLine("The configured AI provider requires an API key, but no key was found. Sidecar will still organize live diagnostics.");
        }
        else
        {
            var timeoutSeconds = ParseInt(GetArg(args, "--timeout"), providerConfig.TimeoutSeconds ?? 120);
            var timeout = TimeSpan.FromSeconds(Math.Max(10, timeoutSeconds));
            ActiveProvider = AiProviderFactory.Create(new AiProviderSettings
            {
                ProviderType = providerType,
                ApiKey = apiKey,
                BaseUrl = GetArg(args, "--base-url") ?? providerConfig.BaseUrl,
                EndpointPath = GetArg(args, "--endpoint") ?? providerConfig.EndpointPath,
                Model = GetArg(args, "--model") ?? providerConfig.Model,
                Timeout = timeout,
                Temperature = providerConfig.Temperature,
                MaxTokens = providerConfig.MaxTokens,
                Headers = providerConfig.Headers
            });
        }

        if (args.Any(arg => string.Equals(arg, "--test-provider", StringComparison.OrdinalIgnoreCase)))
            return await TestProviderConnection(providerConfig, providerType, aiAvailable);

        Console.WriteLine($"COTL AI NPC sidecar running at {root}");
        if (ActiveProvider != null)
            Console.WriteLine($"AI provider active: {ActiveProvider.DisplayName}");
        while (true)
        {
            if (ParentProcessExited(parentPid))
            {
                Console.WriteLine($"Parent game process {parentPid} is no longer running; sidecar exiting.");
                return 0;
            }

            WriteReady(root, parentPid, aiAvailable);
            if (aiAvailable)
                await ProcessRequests(requestDir, responseDir, archiveDir);
            OrganizeLiveDiagnostics(root);

            if (once)
                return 0;

            await Task.Delay(500);
        }
    }

    private static async Task ProcessRequests(string requestDir, string responseDir, string archiveDir)
    {
        foreach (var requestPath in Directory.GetFiles(requestDir, "*.request.json").OrderBy(File.GetCreationTimeUtc))
        {
            var requestID = Path.GetFileName(requestPath).Replace(".request.json", string.Empty, StringComparison.OrdinalIgnoreCase);
            var responsePath = Path.Combine(responseDir, $"{requestID}.response.json");
            if (File.Exists(responsePath))
            {
                Archive(requestPath, archiveDir);
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(await File.ReadAllTextAsync(requestPath));
                var root = document.RootElement;
                var models = root.TryGetProperty("model_candidates", out var modelArray)
                    ? modelArray.EnumerateArray().Select(item => item.GetString()).Where(value => !string.IsNullOrWhiteSpace(value)).ToList()
                    : new List<string?>();
                var errors = new List<string>();
                foreach (var model in models)
                {
                    if (string.IsNullOrWhiteSpace(model))
                        continue;

                    try
                    {
                        var (outputText, compiler) = await SendDecisionRequestForModel(root, model);
                        if (string.IsNullOrWhiteSpace(outputText))
                        {
                            errors.Add($"{model}: empty output_text");
                            continue;
                        }

                        await WriteResponse(responsePath, true, $"Sidecar AI provider chose response with model={model}; compiler={compiler}.", outputText, model, errors);
                        Archive(requestPath, archiveDir);
                        return;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{model}: {ex.Message}");
                    }
                }

                await WriteResponse(responsePath, false, $"Sidecar failed all model candidates: {string.Join(" | ", errors)}", string.Empty, string.Empty, errors);
                Archive(requestPath, archiveDir);
            }
            catch (Exception ex)
            {
                await WriteResponse(responsePath, false, $"Sidecar request processing failed: {ex.Message}", string.Empty, string.Empty, new[] { ex.ToString() });
                Archive(requestPath, archiveDir);
            }
        }
    }

    private static async Task<(string OutputText, string Compiler)> SendDecisionRequestForModel(JsonElement requestRoot, string model)
    {
        if (IsReceiptReplyRequest(requestRoot))
        {
            var aiRequest = BuildReceiptReplyAiRequest(requestRoot, model);
            return (await SendAiRequest(aiRequest), "receipt_reply_v1");
        }

        if (IsCharacterModeRequest(requestRoot))
        {
            var outputText = await SendDirectSpeakReply(requestRoot, model);
            return (outputText, "character_mode_direct_speak_v1");
        }

        return (string.Empty, "unsupported_character_only_request");
    }

    private static string? GetArg(string[] args, string name)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                return args[index + 1];
        }

        return null;
    }

    private static int ParseInt(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static string Trim(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Length <= maxLength
            ? value
            : value.Substring(0, Math.Max(0, maxLength - 3)) + "...";
    }
}




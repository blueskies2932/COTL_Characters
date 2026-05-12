using BepInEx;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace COTL_AL_NPCs
{
    internal sealed class FollowerAiProviderSetupDraft
    {
        public string ProviderType = "openai";
        public string ApiKeyEnvVar = "OPENAI_API_KEY";
        public string BaseUrl = string.Empty;
        public string EndpointPath = "/responses";
        public string Model = string.Empty;
        public bool RequiresApiKey = true;
        public int TimeoutSeconds = 120;
    }

    internal static class FollowerAiProviderSetup
    {
        private const string ProviderConfigFileName = "AiProvider.json";
        private const string ProviderKeyFileName = "AiProviderKey.txt";
        private const string StartHereFileName = "START_HERE_AI_PROVIDER_SETUP.txt";
        private const string SetupCommandFileName = "Setup_AI_Provider.cmd";
        private const string SetupScriptFileName = "Setup_AI_Provider.ps1";
        private const string ResetCommandFileName = "Reset_AI_Provider.cmd";
        private const string ResetScriptFileName = "Reset_AI_Provider.ps1";
        private const string LastProviderSetupTestFileName = "LAST_PROVIDER_SETUP_TEST.txt";

        internal static string ConfigDirectory => Path.Combine(Paths.ConfigPath, "COTL_AL_NPCs");
        internal static string ProviderConfigPath => Path.Combine(ConfigDirectory, ProviderConfigFileName);
        internal static string ProviderKeyPath => Path.Combine(ConfigDirectory, ProviderKeyFileName);
        internal static string StartHerePath => Path.Combine(ConfigDirectory, StartHereFileName);
        internal static string SetupCommandPath => Path.Combine(ConfigDirectory, SetupCommandFileName);
        internal static string SetupScriptPath => Path.Combine(ConfigDirectory, SetupScriptFileName);
        internal static string ResetCommandPath => Path.Combine(ConfigDirectory, ResetCommandFileName);
        internal static string ResetScriptPath => Path.Combine(ConfigDirectory, ResetScriptFileName);
        internal static string LastProviderSetupTestPath => Path.Combine(ConfigDirectory, LastProviderSetupTestFileName);

        internal static FollowerAiProviderSetupDraft GetDraft()
        {
            try
            {
                var config = ReadConfig();
                return new FollowerAiProviderSetupDraft
                {
                    ProviderType = config["providerType"]?.Value<string>()?.Trim() ?? "openai",
                    ApiKeyEnvVar = config["apiKeyEnvVar"]?.Value<string>()?.Trim() ?? "OPENAI_API_KEY",
                    BaseUrl = config["baseUrl"]?.Value<string>()?.Trim() ?? string.Empty,
                    EndpointPath = config["endpointPath"]?.Value<string>()?.Trim() ?? "/responses",
                    Model = config["model"]?.Value<string>()?.Trim() ?? string.Empty,
                    RequiresApiKey = config["requiresApiKey"]?.Value<bool?>() ?? true,
                    TimeoutSeconds = Math.Max(10, config["timeoutSeconds"]?.Value<int?>() ?? 120)
                };
            }
            catch
            {
                return new FollowerAiProviderSetupDraft();
            }
        }

        internal static void EnsureFirstRunFiles()
        {
            try
            {
                Directory.CreateDirectory(ConfigDirectory);

                if (!File.Exists(ProviderConfigPath))
                    WriteDefaultProviderConfig();

                if (!File.Exists(ProviderKeyPath))
                    File.WriteAllText(ProviderKeyPath, string.Empty);

                if (!File.Exists(StartHerePath))
                    WriteStartHere();
                if (!File.Exists(SetupCommandPath))
                    WriteSetupCommand();
                if (!File.Exists(SetupScriptPath))
                    WriteSetupScript();
                if (!File.Exists(ResetCommandPath))
                    WriteResetCommand();
                if (!File.Exists(ResetScriptPath))
                    WriteResetScript();
            }
            catch (Exception ex)
            {
                AICharacterPlugin.Log?.LogWarning($"AI provider setup files could not be prepared: {ex.Message}");
            }
        }

        internal static bool IsConfigured()
        {
            try
            {
                var config = ReadConfig();
                if (config["setupComplete"]?.Value<bool?>() != true)
                    return false;

                var providerType = config["providerType"]?.Value<string>()?.Trim() ?? string.Empty;
                if (!string.Equals(providerType, "mock", StringComparison.OrdinalIgnoreCase) &&
                    config["validatedProviderModel"]?.Value<bool?>() != true)
                {
                    return false;
                }

                var requiresKey = config["requiresApiKey"]?.Value<bool?>() ?? true;
                if (!requiresKey)
                    return true;

                var envVar = config["apiKeyEnvVar"]?.Value<string>()?.Trim();
                if (!string.IsNullOrWhiteSpace(envVar) &&
                    !string.IsNullOrWhiteSpace(ReadEnvironmentVariable(envVar)))
                    return true;

                var keyFile = ResolveConfiguredKeyFile(config);
                if (!string.IsNullOrWhiteSpace(ReadFirstNonCommentLine(keyFile)))
                    return true;

                return false;
            }
            catch
            {
                return false;
            }
        }

        internal static string GetConfiguredModel()
        {
            try
            {
                return ReadConfig()["model"]?.Value<string>()?.Trim() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        internal static bool SaveSetup(
            FollowerAiProviderSetupDraft draft,
            string apiKey,
            bool saveKeyFile,
            bool saveEnvironmentVariable,
            out string message)
        {
            message = string.Empty;
            EnsureFirstRunFiles();

            var providerType = (draft?.ProviderType ?? string.Empty).Trim();
            var model = (draft?.Model ?? string.Empty).Trim();
            var envVar = (draft?.ApiKeyEnvVar ?? string.Empty).Trim();
            var baseUrl = (draft?.BaseUrl ?? string.Empty).Trim();
            var endpointPath = (draft?.EndpointPath ?? string.Empty).Trim();
            var key = (apiKey ?? string.Empty).Trim();
            var requiresKey = draft?.RequiresApiKey ?? true;
            var timeout = Math.Max(10, draft?.TimeoutSeconds ?? 120);

            if (string.IsNullOrWhiteSpace(providerType))
            {
                message = "Choose an AI provider.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(model))
            {
                message = "Enter a model name available to your provider/API key.";
                return false;
            }

            if (requiresKey)
            {
                var hasKeyInput = !string.IsNullOrWhiteSpace(key);
                if (hasKeyInput && !saveKeyFile && !(saveEnvironmentVariable && !string.IsNullOrWhiteSpace(envVar)))
                {
                    message = "Choose a place to save the pasted key, or use an existing environment variable/key file.";
                    return false;
                }

                var hasExistingEnv = !string.IsNullOrWhiteSpace(envVar) &&
                                     !string.IsNullOrWhiteSpace(ReadEnvironmentVariable(envVar));
                var hasExistingKeyFile = !string.IsNullOrWhiteSpace(ReadFirstNonCommentLine(ProviderKeyPath));

                if (!hasKeyInput && !hasExistingEnv && !hasExistingKeyFile)
                {
                    message = "Paste an API key, or set the provider's environment variable before saving.";
                    return false;
                }
            }

            try
            {
                Directory.CreateDirectory(ConfigDirectory);

                if (saveEnvironmentVariable && !string.IsNullOrWhiteSpace(envVar) && !string.IsNullOrWhiteSpace(key))
                    Environment.SetEnvironmentVariable(envVar, key, EnvironmentVariableTarget.User);

                if (saveKeyFile && !string.IsNullOrWhiteSpace(key))
                    File.WriteAllText(ProviderKeyPath, key);
                else if (!File.Exists(ProviderKeyPath))
                    File.WriteAllText(ProviderKeyPath, string.Empty);

                var json = new JObject
                {
                    ["providerType"] = providerType,
                    ["apiKeyEnvVar"] = envVar,
                    ["apiKeyFile"] = saveKeyFile ? ProviderKeyFileName : string.Empty,
                    ["requiresApiKey"] = requiresKey,
                    ["setupComplete"] = true,
                    ["validatedProviderModel"] = !string.Equals(providerType, "mock", StringComparison.OrdinalIgnoreCase),
                    ["baseUrl"] = baseUrl,
                    ["endpointPath"] = endpointPath,
                    ["model"] = model,
                    ["timeoutSeconds"] = timeout,
                    ["temperature"] = null,
                    ["maxTokens"] = null,
                    ["headers"] = new JObject()
                };

                File.WriteAllText(ProviderConfigPath, json.ToString());
                var savedConfig = ReadConfig();
                var configured = IsConfigured();
                message = configured
                    ? "AI provider setup saved and verified."
                    : $"AI provider setup was written, but is not complete yet: {DescribeConfiguredState(savedConfig)}";
                return configured;
            }
            catch (Exception ex)
            {
                message = $"Provider setup could not be saved: {ex.Message}";
                return false;
            }
        }

        internal static void ResetSetupFiles()
        {
            try
            {
                if (File.Exists(ProviderConfigPath))
                    File.Delete(ProviderConfigPath);
                if (File.Exists(ProviderKeyPath))
                    File.Delete(ProviderKeyPath);
                WriteDefaultProviderConfig();
                File.WriteAllText(ProviderKeyPath, string.Empty);
            }
            catch (Exception ex)
            {
                AICharacterPlugin.Log?.LogWarning($"AI provider setup reset failed: {ex.Message}");
            }
        }

        internal static bool HasUsableProviderKey(FollowerAiProviderSetupDraft draft, string pastedKey)
        {
            if (draft?.RequiresApiKey != true)
                return true;

            if (!string.IsNullOrWhiteSpace((pastedKey ?? string.Empty).Trim()))
                return true;

            var envVar = (draft.ApiKeyEnvVar ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(envVar) &&
                !string.IsNullOrWhiteSpace(ReadEnvironmentVariable(envVar)))
                return true;

            return !string.IsNullOrWhiteSpace(ReadFirstNonCommentLine(ProviderKeyPath));
        }

        internal static void WriteLastProviderSetupTest(FollowerAiProviderSetupDraft draft, bool success, string message, bool usedPastedKey)
        {
            try
            {
                Directory.CreateDirectory(ConfigDirectory);
                var provider = (draft?.ProviderType ?? string.Empty).Trim();
                var model = (draft?.Model ?? string.Empty).Trim();
                var endpoint = (draft?.EndpointPath ?? string.Empty).Trim();
                var baseUrl = (draft?.BaseUrl ?? string.Empty).Trim();
                var envVar = (draft?.ApiKeyEnvVar ?? string.Empty).Trim();
                var requiresKey = draft?.RequiresApiKey ?? true;
                var hasEnvironmentKey = !string.IsNullOrWhiteSpace(envVar) &&
                                        !string.IsNullOrWhiteSpace(ReadEnvironmentVariable(envVar));
                var hasLocalKeyFile = !string.IsNullOrWhiteSpace(ReadFirstNonCommentLine(ProviderKeyPath));

                var builder = new StringBuilder();
                builder.AppendLine("COTL AI NPC - Last AI Provider Setup Test");
                builder.AppendLine("========================================");
                builder.AppendLine($"Local time: {DateTime.Now:O}");
                builder.AppendLine($"UTC time:   {DateTime.UtcNow:O}");
                builder.AppendLine($"Result:     {(success ? "SUCCESS" : "FAILED")}");
                builder.AppendLine($"Provider:   {provider}");
                builder.AppendLine($"Model:      {model}");
                builder.AppendLine($"Base URL:   {baseUrl}");
                builder.AppendLine($"Endpoint:   {endpoint}");
                builder.AppendLine($"Needs key:  {requiresKey}");
                builder.AppendLine($"Pasted key used for this test: {(usedPastedKey ? "yes" : "no")}");
                builder.AppendLine($"Env var:    {envVar}");
                builder.AppendLine($"Env key:    {(hasEnvironmentKey ? "present" : "not present")}");
                builder.AppendLine($"Key file:   {(hasLocalKeyFile ? "present" : "not present")}");
                builder.AppendLine();
                builder.AppendLine("Provider response:");
                builder.AppendLine(message ?? string.Empty);

                File.WriteAllText(LastProviderSetupTestPath, builder.ToString());
            }
            catch (Exception ex)
            {
                AICharacterPlugin.Log?.LogWarning($"Could not write AI provider setup test receipt: {ex.Message}");
            }
        }

        internal static string GetStatusLine()
        {
            try
            {
                var config = ReadConfig();
                var provider = config["providerType"]?.Value<string>()?.Trim();
                var model = config["model"]?.Value<string>()?.Trim();
                var setupComplete = config["setupComplete"]?.Value<bool?>() == true;
                if (string.IsNullOrWhiteSpace(provider))
                    provider = "not selected";
                if (string.IsNullOrWhiteSpace(model))
                    model = "not selected";

                if (!setupComplete)
                    return $"AI provider setup has not been completed: {provider} / {model}";

                if (!string.Equals(provider, "mock", StringComparison.OrdinalIgnoreCase) &&
                    config["validatedProviderModel"]?.Value<bool?>() != true)
                {
                    return $"AI provider setup needs model validation: {provider} / {model}";
                }

                return IsConfigured()
                    ? $"AI provider configured: {provider} / {model}"
                    : $"AI provider needs setup: {provider} / {model}";
            }
            catch
            {
                return "AI provider needs setup.";
            }
        }

        private static string DescribeConfiguredState(JObject config)
        {
            try
            {
                var provider = config["providerType"]?.Value<string>()?.Trim() ?? "not selected";
                var model = config["model"]?.Value<string>()?.Trim() ?? "not selected";
                var setupComplete = config["setupComplete"]?.Value<bool?>() == true;
                var validated = config["validatedProviderModel"]?.Value<bool?>() == true;
                var requiresKey = config["requiresApiKey"]?.Value<bool?>() ?? true;
                var envVar = config["apiKeyEnvVar"]?.Value<string>()?.Trim() ?? string.Empty;
                var hasEnvironmentKey = !string.IsNullOrWhiteSpace(envVar) &&
                                        !string.IsNullOrWhiteSpace(ReadEnvironmentVariable(envVar));
                var keyFile = ResolveConfiguredKeyFile(config);
                var hasKeyFile = !string.IsNullOrWhiteSpace(ReadFirstNonCommentLine(keyFile));
                return $"provider={provider}, model={model}, setupComplete={setupComplete}, validatedModel={validated}, requiresKey={requiresKey}, envKey={(hasEnvironmentKey ? "present" : "missing")}, keyFile={(hasKeyFile ? "present" : "missing")}";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        internal static void OpenSetupTool()
        {
            EnsureFirstRunFiles();
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = SetupCommandPath,
                    UseShellExecute = true,
                    WorkingDirectory = ConfigDirectory
                });
            }
            catch (Exception ex)
            {
                AICharacterPlugin.Log?.LogWarning($"AI provider setup tool could not be opened: {ex.Message}");
            }
        }

        private static JObject ReadConfig()
        {
            EnsureFirstRunFiles();
            return JObject.Parse(File.ReadAllText(ProviderConfigPath));
        }

        private static string ResolveConfiguredKeyFile(JObject config)
        {
            var configured = config["apiKeyFile"]?.Value<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(configured))
                return ProviderKeyPath;

            return Path.IsPathRooted(configured)
                ? configured
                : Path.GetFullPath(Path.Combine(ConfigDirectory, configured));
        }

        private static string ReadFirstNonCommentLine(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return string.Empty;

            foreach (var line in File.ReadLines(path))
            {
                var trimmed = line?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(trimmed) && !trimmed.StartsWith("#"))
                    return trimmed;
            }

            return string.Empty;
        }

        internal static string[] FetchAvailableModels(FollowerAiProviderSetupDraft draft, string apiKey, out string message)
        {
            message = string.Empty;
            var providerType = (draft?.ProviderType ?? string.Empty).Trim().ToLowerInvariant();
            var key = (apiKey ?? string.Empty).Trim();
            var envVar = (draft?.ApiKeyEnvVar ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(envVar))
                key = ReadEnvironmentVariable(envVar);

            try
            {
                var request = CreateModelListRequest(draft, providerType, key);
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    var json = JObject.Parse(reader.ReadToEnd());
                    var models = ParseModelIds(providerType, json);
                    if (models.Length == 0)
                    {
                        message = "The provider responded, but no usable text-generation models were found.";
                        return models;
                    }

                    message = $"Found {models.Length} model(s). Use Find, Test & Save Setup to test likely models automatically, or choose one manually and test it.";
                    return models;
                }
            }
            catch (WebException ex)
            {
                message = DescribeWebException(ex);
                return new string[0];
            }
            catch (Exception ex)
            {
                message = $"Could not fetch models: {ex.Message}";
                return new string[0];
            }
        }

        private static HttpWebRequest CreateModelListRequest(FollowerAiProviderSetupDraft draft, string providerType, string apiKey)
        {
            var baseUrl = (draft?.BaseUrl ?? string.Empty).Trim().TrimEnd('/');
            string url;

            switch (providerType)
            {
                case "openai":
                    url = string.IsNullOrWhiteSpace(baseUrl)
                        ? "https://api.openai.com/v1/models"
                        : $"{baseUrl}/models";
                    break;
                case "openrouter":
                    url = string.IsNullOrWhiteSpace(baseUrl)
                        ? "https://openrouter.ai/api/v1/models"
                        : $"{baseUrl}/models";
                    break;
                case "anthropic":
                case "claude":
                    url = string.IsNullOrWhiteSpace(baseUrl)
                        ? "https://api.anthropic.com/v1/models"
                        : $"{baseUrl}/models";
                    break;
                case "gemini":
                case "google":
                case "google-gemini":
                    url = string.IsNullOrWhiteSpace(baseUrl)
                        ? "https://generativelanguage.googleapis.com/v1beta/models"
                        : $"{baseUrl}/v1beta/models";
                    break;
                default:
                    url = string.IsNullOrWhiteSpace(baseUrl)
                        ? "https://api.openai.com/v1/models"
                        : $"{baseUrl}/models";
                    break;
            }

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Timeout = Math.Max(10000, (draft?.TimeoutSeconds ?? 120) * 1000);
            request.ReadWriteTimeout = request.Timeout;
            request.Accept = "application/json";

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                if (providerType == "anthropic" || providerType == "claude")
                {
                    request.Headers["x-api-key"] = apiKey;
                    request.Headers["anthropic-version"] = "2023-06-01";
                }
                else if (providerType == "gemini" || providerType == "google" || providerType == "google-gemini")
                {
                    request.Headers["x-goog-api-key"] = apiKey;
                }
                else
                {
                    request.Headers[HttpRequestHeader.Authorization] = $"Bearer {apiKey}";
                }
            }

            return request;
        }

        private static string[] ParseModelIds(string providerType, JObject json)
        {
            JToken listToken;
            if (providerType == "gemini" || providerType == "google" || providerType == "google-gemini")
                listToken = json["models"];
            else
                listToken = json["data"];

            if (listToken == null || listToken.Type != JTokenType.Array)
                return new string[0];

            var models = listToken
                .OfType<JObject>()
                .Where(model => SupportsTextGeneration(providerType, model))
                .Select(model => ExtractModelId(providerType, model))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return models;
        }

        private static bool SupportsTextGeneration(string providerType, JObject model)
        {
            if (providerType == "gemini" || providerType == "google" || providerType == "google-gemini")
            {
                var methods = model["supportedGenerationMethods"] as JArray;
                return methods == null ||
                       methods.Any(method => string.Equals(method?.Value<string>(), "generateContent", StringComparison.OrdinalIgnoreCase));
            }

            var architecture = model["architecture"] as JObject;
            var outputModalities = architecture?["output_modalities"] as JArray;
            if (outputModalities != null && outputModalities.Count > 0)
                return outputModalities.Any(value => string.Equals(value?.Value<string>(), "text", StringComparison.OrdinalIgnoreCase));

            return true;
        }

        private static string ExtractModelId(string providerType, JObject model)
        {
            var id = providerType == "gemini" || providerType == "google" || providerType == "google-gemini"
                ? model["name"]?.Value<string>()
                : model["id"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(id))
                return string.Empty;

            id = id.Trim();
            return id.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
                ? id.Substring("models/".Length)
                : id;
        }

        private static string DescribeWebException(WebException ex)
        {
            var status = ex.Response is HttpWebResponse response
                ? $"{(int)response.StatusCode} {response.StatusCode}"
                : ex.Status.ToString();

            return $"Could not fetch models from provider: {status}. Check provider, key, endpoint, and network access.";
        }

        private static string ReadEnvironmentVariable(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            try
            {
                var processValue = Environment.GetEnvironmentVariable(name)?.Trim();
                if (!string.IsNullOrWhiteSpace(processValue))
                    return processValue;
            }
            catch
            {
                // Keep checking user-level configuration.
            }

            try
            {
                return Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User)?.Trim() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void WriteDefaultProviderConfig()
        {
            var json = new JObject
            {
                ["providerType"] = "openai",
                ["apiKeyEnvVar"] = "OPENAI_API_KEY",
                ["apiKeyFile"] = ProviderKeyFileName,
                ["requiresApiKey"] = true,
                ["setupComplete"] = false,
                ["validatedProviderModel"] = false,
                ["baseUrl"] = string.Empty,
                ["endpointPath"] = "/responses",
                ["model"] = string.Empty,
                ["timeoutSeconds"] = 120,
                ["temperature"] = null,
                ["maxTokens"] = null,
                ["headers"] = new JObject()
            };

            File.WriteAllText(ProviderConfigPath, json.ToString());
        }

        private static void WriteStartHere()
        {
            var text =
                "COTL AI NPC - AI Provider Setup" + Environment.NewLine +
                "================================" + Environment.NewLine +
                Environment.NewLine +
                "Launch the game and use the in-game AI Provider Setup panel." + Environment.NewLine +
                "For most players: choose the provider, paste the provider key once, then click Find, Test & Save Setup." + Environment.NewLine +
                "The in-game setup prompt stays visible until the mod has tested and saved a provider/model that can answer NPC conversations." + Environment.NewLine +
                "Double-click Reset_AI_Provider.cmd later if you want to clear provider setup and start over." + Environment.NewLine +
                "Setup_AI_Provider.cmd is an advanced file setup helper. The in-game setup is preferred because it can test the exact model path." + Environment.NewLine +
                Environment.NewLine +
                "The setup tool writes:" + Environment.NewLine +
                "- AiProvider.json      provider/model/endpoint settings" + Environment.NewLine +
                "- AiProviderKey.txt    optional local API key file" + Environment.NewLine +
                Environment.NewLine +
                "Do not share AiProviderKey.txt or any config file containing an API key." + Environment.NewLine;

            File.WriteAllText(StartHerePath, text);
        }

        private static void WriteSetupCommand()
        {
            var text =
                "@echo off" + Environment.NewLine +
                "cd /d \"%~dp0\"" + Environment.NewLine +
                "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"%~dp0Setup_AI_Provider.ps1\"" + Environment.NewLine;

            File.WriteAllText(SetupCommandPath, text);
        }

        private static void WriteResetCommand()
        {
            var text =
                "@echo off" + Environment.NewLine +
                "cd /d \"%~dp0\"" + Environment.NewLine +
                "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"%~dp0Reset_AI_Provider.ps1\"" + Environment.NewLine;

            File.WriteAllText(ResetCommandPath, text);
        }

        private static void WriteResetScript()
        {
            var script = @"
Add-Type -AssemblyName System.Windows.Forms

$configRoot = $PSScriptRoot
$configPath = Join-Path $configRoot 'AiProvider.json'
$keyPath = Join-Path $configRoot 'AiProviderKey.txt'

$message = 'This will clear the local AI provider setup and local key file for this mod profile. Launch the game afterward and use the in-game setup panel to find and save a working provider/model. Continue?'
$choice = [System.Windows.Forms.MessageBox]::Show($message, 'Reset COTL AI Provider Setup', 'YesNo', 'Warning')
if ($choice -ne 'Yes') { exit 0 }

if (Test-Path -LiteralPath $configPath) { Remove-Item -LiteralPath $configPath -Force }
if (Test-Path -LiteralPath $keyPath) { Remove-Item -LiteralPath $keyPath -Force }

[System.Windows.Forms.MessageBox]::Show('AI provider setup was reset. Launch the game and use the in-game setup panel to create fresh setup files.', 'Reset Complete') | Out-Null
";

            File.WriteAllText(ResetScriptPath, script.TrimStart());
        }

        private static void WriteSetupScript()
        {
            var script = @"
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$configRoot = $PSScriptRoot
$configPath = Join-Path $configRoot 'AiProvider.json'
$keyPath = Join-Path $configRoot 'AiProviderKey.txt'

function New-DefaultConfig {
    [ordered]@{
        providerType = 'openai'
        apiKeyEnvVar = 'OPENAI_API_KEY'
        apiKeyFile = $keyPath
        requiresApiKey = $true
        setupComplete = $false
        validatedProviderModel = $false
        baseUrl = ''
        endpointPath = '/responses'
        model = ''
        timeoutSeconds = 120
        temperature = $null
        maxTokens = $null
        headers = @{}
    }
}

function Read-Config {
    if (Test-Path -LiteralPath $configPath) {
        try { return Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json } catch {}
    }
    return [pscustomobject](New-DefaultConfig)
}

function Set-Preset([string]$preset) {
    switch ($preset) {
        'OpenAI' {
            $providerType.Text = 'openai'; $envVar.Text = 'OPENAI_API_KEY'; $requiresKey.Checked = $true
            $baseUrl.Text = ''; $endpoint.Text = '/responses'; $model.Text = ''
        }
        'OpenRouter' {
            $providerType.Text = 'openai-compatible'; $envVar.Text = 'OPENROUTER_API_KEY'; $requiresKey.Checked = $true
            $baseUrl.Text = 'https://openrouter.ai/api/v1'; $endpoint.Text = '/chat/completions'; $model.Text = ''
        }
        'LM Studio' {
            $providerType.Text = 'openai-compatible'; $envVar.Text = ''; $requiresKey.Checked = $false
            $baseUrl.Text = 'http://localhost:1234/v1'; $endpoint.Text = '/chat/completions'; $model.Text = ''
        }
        'Ollama Compatible' {
            $providerType.Text = 'openai-compatible'; $envVar.Text = ''; $requiresKey.Checked = $false
            $baseUrl.Text = 'http://localhost:11434/v1'; $endpoint.Text = '/chat/completions'; $model.Text = ''
        }
        'Anthropic Claude' {
            $providerType.Text = 'anthropic'; $envVar.Text = 'ANTHROPIC_API_KEY'; $requiresKey.Checked = $true
            $baseUrl.Text = 'https://api.anthropic.com/v1'; $endpoint.Text = '/messages'; $model.Text = ''
        }
        'Google Gemini' {
            $providerType.Text = 'gemini'; $envVar.Text = 'GEMINI_API_KEY'; $requiresKey.Checked = $true
            $baseUrl.Text = 'https://generativelanguage.googleapis.com'; $endpoint.Text = '/v1beta/models/{model}:generateContent'; $model.Text = ''
        }
        'Mock' {
            $providerType.Text = 'mock'; $envVar.Text = ''; $requiresKey.Checked = $false
            $baseUrl.Text = ''; $endpoint.Text = ''; $model.Text = 'mock'
        }
    }
}

function Save-Config {
    New-Item -ItemType Directory -Force -Path $configRoot | Out-Null

    if ($saveEnvVar.Checked -and -not [string]::IsNullOrWhiteSpace($envVar.Text) -and -not [string]::IsNullOrWhiteSpace($apiKey.Text)) {
        [Environment]::SetEnvironmentVariable($envVar.Text.Trim(), $apiKey.Text.Trim(), 'User')
    }

    if ($saveKeyFile.Checked) {
        Set-Content -LiteralPath $keyPath -Value $apiKey.Text.Trim() -NoNewline
    }

    if ([string]::IsNullOrWhiteSpace($model.Text)) {
        $status.Text = 'Enter a model name available to your provider/API key before saving.'
        return
    }

    if ($requiresKey.Checked -and [string]::IsNullOrWhiteSpace($apiKey.Text) -and [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($envVar.Text.Trim()))) {
        $status.Text = 'Paste an API key, or set the provider environment variable before saving.'
        return
    }

    $config = [ordered]@{
        providerType = $providerType.Text.Trim()
        apiKeyEnvVar = $envVar.Text.Trim()
        apiKeyFile = $keyPath
        requiresApiKey = [bool]$requiresKey.Checked
        setupComplete = $true
        validatedProviderModel = $false
        baseUrl = $baseUrl.Text.Trim()
        endpointPath = $endpoint.Text.Trim()
        model = $model.Text.Trim()
        timeoutSeconds = [int]$timeout.Value
        temperature = $null
        maxTokens = $null
        headers = @{}
    }

    $config | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $configPath -Encoding UTF8
    $status.Text = 'Saved. Relaunch the game if the AI sidecar was already running.'
}

function Reset-ProviderSetup {
    $message = 'This will clear the local AI provider setup and local key file for this mod profile. Continue?'
    $choice = [System.Windows.Forms.MessageBox]::Show($message, 'Reset COTL AI Provider Setup', 'YesNo', 'Warning')
    if ($choice -ne 'Yes') { return }
    if (Test-Path -LiteralPath $configPath) { Remove-Item -LiteralPath $configPath -Force }
    if (Test-Path -LiteralPath $keyPath) { Remove-Item -LiteralPath $keyPath -Force }
    $apiKey.Text = ''
    $config = [pscustomobject](New-DefaultConfig)
    $providerType.Text = $config.providerType
    $model.Text = $config.model
    $baseUrl.Text = $config.baseUrl
    $endpoint.Text = $config.endpointPath
    $envVar.Text = $config.apiKeyEnvVar
    $requiresKey.Checked = [bool]$config.requiresApiKey
    $saveKeyFile.Checked = $true
    $status.Text = 'AI provider setup was reset. Choose a provider and click Save to complete setup again.'
}

$config = Read-Config
$form = New-Object System.Windows.Forms.Form
$form.Text = 'COTL AI Provider Setup'
$form.StartPosition = 'CenterScreen'
$form.Size = New-Object System.Drawing.Size(760, 640)
$form.MinimumSize = New-Object System.Drawing.Size(720, 620)
$form.Font = New-Object System.Drawing.Font('Segoe UI', 10)

$y = 18
function Add-Label($text, $x, $y) {
    $label = New-Object System.Windows.Forms.Label
    $label.Text = $text
    $label.Location = New-Object System.Drawing.Point($x, $y)
    $label.Size = New-Object System.Drawing.Size(180, 24)
    $form.Controls.Add($label)
}
function Add-TextBox($x, $y, $w) {
    $box = New-Object System.Windows.Forms.TextBox
    $box.Location = New-Object System.Drawing.Point($x, $y)
    $box.Size = New-Object System.Drawing.Size($w, 28)
    $form.Controls.Add($box)
    return $box
}

Add-Label 'Preset' 18 $y
$preset = New-Object System.Windows.Forms.ComboBox
$preset.Location = New-Object System.Drawing.Point(210, $y)
$preset.Size = New-Object System.Drawing.Size(500, 28)
$preset.DropDownStyle = 'DropDownList'
[void]$preset.Items.AddRange(@('OpenAI', 'OpenRouter', 'LM Studio', 'Ollama Compatible', 'Anthropic Claude', 'Google Gemini', 'Custom', 'Mock'))
$preset.SelectedItem = 'OpenAI'
$form.Controls.Add($preset)
$y += 42

Add-Label 'Provider type' 18 $y; $providerType = Add-TextBox 210 $y 500; $y += 42
Add-Label 'Model' 18 $y; $model = Add-TextBox 210 $y 500; $y += 42
Add-Label 'Base URL' 18 $y; $baseUrl = Add-TextBox 210 $y 500; $y += 42
Add-Label 'Endpoint path' 18 $y; $endpoint = Add-TextBox 210 $y 500; $y += 42
Add-Label 'API key env var' 18 $y; $envVar = Add-TextBox 210 $y 500; $y += 42

$requiresKey = New-Object System.Windows.Forms.CheckBox
$requiresKey.Text = 'Provider requires an API key'
$requiresKey.Location = New-Object System.Drawing.Point(210, $y)
$requiresKey.Size = New-Object System.Drawing.Size(360, 28)
$form.Controls.Add($requiresKey)
$y += 38

$saveEnvVar = New-Object System.Windows.Forms.CheckBox
$saveEnvVar.Text = 'Save pasted key to Windows user environment variable'
$saveEnvVar.Location = New-Object System.Drawing.Point(210, $y)
$saveEnvVar.Size = New-Object System.Drawing.Size(470, 28)
$form.Controls.Add($saveEnvVar)
$y += 38

$saveKeyFile = New-Object System.Windows.Forms.CheckBox
$saveKeyFile.Text = 'Save pasted key to local AiProviderKey.txt'
$saveKeyFile.Location = New-Object System.Drawing.Point(210, $y)
$saveKeyFile.Size = New-Object System.Drawing.Size(420, 28)
$saveKeyFile.Checked = $true
$form.Controls.Add($saveKeyFile)
$y += 38

Add-Label 'API key' 18 $y
$apiKey = Add-TextBox 210 $y 500
$apiKey.UseSystemPasswordChar = $true
$y += 42

Add-Label 'Timeout seconds' 18 $y
$timeout = New-Object System.Windows.Forms.NumericUpDown
$timeout.Location = New-Object System.Drawing.Point(210, $y)
$timeout.Size = New-Object System.Drawing.Size(120, 28)
$timeout.Minimum = 10
$timeout.Maximum = 600
$timeout.Value = 120
$form.Controls.Add($timeout)
$y += 50

$save = New-Object System.Windows.Forms.Button
$save.Text = 'Save'
$save.Location = New-Object System.Drawing.Point(210, $y)
$save.Size = New-Object System.Drawing.Size(130, 36)
$form.Controls.Add($save)

$reset = New-Object System.Windows.Forms.Button
$reset.Text = 'Reset'
$reset.Location = New-Object System.Drawing.Point(350, $y)
$reset.Size = New-Object System.Drawing.Size(130, 36)
$form.Controls.Add($reset)

$close = New-Object System.Windows.Forms.Button
$close.Text = 'Close'
$close.Location = New-Object System.Drawing.Point(500, $y)
$close.Size = New-Object System.Drawing.Size(130, 36)
$form.Controls.Add($close)
$y += 50

$status = New-Object System.Windows.Forms.TextBox
$status.Location = New-Object System.Drawing.Point(18, $y)
$status.Size = New-Object System.Drawing.Size(700, 80)
$status.Multiline = $true
$status.ReadOnly = $true
$status.Text = 'Choose a provider, paste a key if needed, then click Save.'
$form.Controls.Add($status)

$providerType.Text = $config.providerType
$model.Text = $config.model
$baseUrl.Text = $config.baseUrl
$endpoint.Text = $config.endpointPath
$envVar.Text = $config.apiKeyEnvVar
$requiresKey.Checked = [bool]$config.requiresApiKey
if ($config.timeoutSeconds) { $timeout.Value = [decimal]$config.timeoutSeconds }
if ($config.apiKeyFile) { $saveKeyFile.Checked = $true }

$preset.Add_SelectedIndexChanged({ Set-Preset $preset.SelectedItem })
$save.Add_Click({ Save-Config })
$reset.Add_Click({ Reset-ProviderSetup })
$close.Add_Click({ $form.Close() })

[void]$form.ShowDialog()
";

            File.WriteAllText(SetupScriptPath, script.TrimStart());
        }
    }
}

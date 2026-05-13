internal static partial class Program
{
    private static string ResolveProviderApiKey(string root, string? explicitKeyFile, AiProviderConfig config, string providerType)
    {
        var envName = !string.IsNullOrWhiteSpace(config.ApiKeyEnvVar)
            ? config.ApiKeyEnvVar
            : DefaultProviderApiKeyEnvVar(providerType);
        var env = ReadEnvironmentVariable(envName);
        if (!string.IsNullOrWhiteSpace(env))
            return env.Trim();

        foreach (var path in EnumerateKeyCandidates(root, explicitKeyFile, config.ApiKeyFile))
        {
            try
            {
                if (!File.Exists(path))
                    continue;

                var line = File.ReadLines(path)
                    .Select(value => value.Trim())
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value) && !value.StartsWith("#"));
                if (!string.IsNullOrWhiteSpace(line))
                    return line;
            }
            catch
            {
                // Keep probing.
            }
        }

        return string.Empty;
    }

    private static IEnumerable<string> EnumerateKeyCandidates(string root, string? explicitKeyFile, string? configuredKeyFile)
    {
        if (!string.IsNullOrWhiteSpace(explicitKeyFile))
            yield return Path.GetFullPath(explicitKeyFile);

        if (!string.IsNullOrWhiteSpace(configuredKeyFile))
            yield return Path.GetFullPath(configuredKeyFile);
    }

    private static bool ProviderRequiresApiKey(string providerType, AiProviderConfig config)
    {
        if (config.RequiresApiKey.HasValue)
            return config.RequiresApiKey.Value;

        var normalized = (providerType ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is "" or "openai" or "openrouter" or "anthropic" or "claude" or "gemini";
    }

    private static string DefaultProviderApiKeyEnvVar(string providerType)
    {
        var normalized = (providerType ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "openrouter" => "OPENROUTER_API_KEY",
            "anthropic" or "claude" => "ANTHROPIC_API_KEY",
            "gemini" or "google" => "GEMINI_API_KEY",
            "openai-compatible" or "openai_compatible" => "AI_PROVIDER_API_KEY",
            _ => "OPENAI_API_KEY"
        };
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
}

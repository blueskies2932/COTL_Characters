using BepInEx;
using BepInEx.Configuration;
using System;

namespace COTL_AL_NPCs
{
    public partial class AICharacterPlugin
    {
        internal static string GetOpenAIApiKey()
        {
            var envKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (!string.IsNullOrWhiteSpace(envKey))
                return envKey.Trim();

            var configKey = OpenAIApiKey?.Value?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(configKey))
                return configKey;

            return string.Empty;
        }

        internal static string GetOpenAIConfigurationSource() => FollowerAiProviderSetup.GetStatusLine();

        private void BindConfig()
        {
            OpenAIEnabled = Config.Bind("OpenAI", "Enabled", true, "Allow AI follower decisions through the sidecar AI runtime.");
            OpenAIApiKey = Config.Bind("AI Provider", "LegacyApiKey", string.Empty, "Deprecated. Use the in-game AI Provider Setup panel instead.");
            OpenAIModel = Config.Bind("AI Provider", "LegacyModel", string.Empty, "Deprecated. Use the in-game AI Provider Setup panel instead.");
            OpenAIModelFallbacks = Config.Bind("AI Provider", "ModelFallbacks", string.Empty, "Optional comma-separated model IDs to try if the configured provider model is unavailable.");
            OpenAIReasoningEffort = Config.Bind("OpenAI", "ReasoningEffort", "medium", "Reasoning effort for GPT-5-class models: none, minimal, low, medium, high, or xhigh. Pro models are sent as high.");
            OpenAITimeoutSeconds = Config.Bind("OpenAI", "TimeoutSeconds", 90, "HTTP timeout for OpenAI follower decisions.");
            OpenAIInternetAccessEnabled = Config.Bind("AI Provider", "InternetAccessEnabled", false, "Allow direct character replies to use internet-backed search tools when supported by the selected AI provider.");
            VerboseLogging = Config.Bind("Performance", "VerboseLogging", false, "Write detailed AI scheduler trace lines to the BepInEx log. Keep this off during normal play.");
            FollowerFactsDumpRequest = Config.Bind("Diagnostics", "FollowerFactsDumpRequest", false, "One-shot request: write CurrentFollowerFactsReport.txt from the live follower facts provider, then reset this setting to false.");
            LiveDiagnosticsEnabled = Config.Bind("Diagnostics", "LiveReportEnabled", true, "Write a live AI diagnostic report file under the active save scope so issues can be inspected outside the game.");
            LiveDiagnosticsIntervalSeconds = Config.Bind("Diagnostics", "LiveReportIntervalSeconds", 10f, "Seconds between lightweight live AI diagnostic stream appends. Focused reports are compiled by the sidecar outside Unity. Lower values are for short test runs only.");
            RosterCacheSeconds = Config.Bind("Performance", "RosterCacheSeconds", 2f, "Seconds to cache reflected follower roster summaries. Higher is faster but slightly less live.");
            SnapshotCacheSeconds = Config.Bind("Performance", "SnapshotCacheSeconds", 0.5f, "Seconds to cache reflected follower runtime snapshots.");
            SidecarEnabled = Config.Bind("AI Sidecar", "Enabled", true, "Allow an external COTL_AL_NPCs sidecar process to handle AI provider requests outside the game when it is running and ready.");
            SidecarAutoStartEnabled = Config.Bind("AI Sidecar", "AutoStart", true, "Start the COTL_AL_NPCs sidecar automatically when the game/mod starts, and stop the owned sidecar process when the game closes.");
            SidecarExecutablePath = Config.Bind("AI Sidecar", "ExecutablePath", string.Empty, "Optional path to CotlAiNpcSidecar.exe, CotlAiNpcSidecar.dll, or a folder containing it. Leave blank for auto-discovery.");
            SidecarDotnetPath = Config.Bind("AI Sidecar", "DotnetPath", "dotnet", "dotnet executable used when the sidecar is launched from CotlAiNpcSidecar.dll instead of CotlAiNpcSidecar.exe.");
            SidecarSnapshotIntervalSeconds = Config.Bind("AI Sidecar", "SnapshotIntervalSeconds", 12f, "Seconds between compact live-state snapshot exports for the sidecar. Higher is smoother but less fresh.");
            SidecarDecisionTimeoutSeconds = Config.Bind("AI Sidecar", "DecisionTimeoutSeconds", 90, "Seconds an off-thread AI request waits for a ready sidecar response before failing.");
            SidecarReadyStaleSeconds = Config.Bind("AI Sidecar", "ReadyStaleSeconds", 20, "Maximum age in seconds for the sidecar ready heartbeat before the game ignores sidecar decision routing.");
            ConversationWindowX = Config.Bind("AI Conversation Window", "X", -1f, "Saved screen X position for the AI follower conversation window. Negative means auto-center.");
            ConversationWindowY = Config.Bind("AI Conversation Window", "Y", -1f, "Saved screen Y position for the AI follower conversation window. Negative means auto-center.");
            ConversationWindowWidth = Config.Bind("AI Conversation Window", "Width", 0f, "Saved width for the AI follower conversation window. 0 means automatic.");
            ConversationWindowHeight = Config.Bind("AI Conversation Window", "Height", 0f, "Saved height for the AI follower conversation window. 0 means automatic.");
            ConversationFontSize = Config.Bind("AI Conversation Window", "FontSize", 52, "Font size used by the AI follower conversation window.");
            ConversationWindowOpacity = Config.Bind("AI Conversation Window", "WindowOpacity", 0.72f, "Opacity for the AI follower conversation window background. Text remains fully opaque.");
        }

    }
}

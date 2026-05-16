using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Lamb.UI;

namespace COTL_AL_NPCs
{
    [BepInPlugin("io.github.blueskies2932.COTL_Characters", "COTL Characters", "0.1.6")]
    [BepInDependency("io.github.xhayper.COTL_API")]
    public partial class AICharacterPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static bool NextFollowerIsNPC = false;
        internal static FollowerAiMode NextFollowerMode = FollowerAiMode.Vanilla;
        internal static AICharacterPlugin Instance;
        internal static readonly bool IsProductBuild = DetectProductBuild();
        internal static ConfigEntry<bool> OpenAIEnabled;
        internal static ConfigEntry<string> OpenAIApiKey;
        internal static ConfigEntry<string> OpenAIModel;
        internal static ConfigEntry<string> OpenAIModelFallbacks;
        internal static ConfigEntry<string> OpenAIReasoningEffort;
        internal static ConfigEntry<int> OpenAITimeoutSeconds;
        internal static ConfigEntry<bool> OpenAIInternetAccessEnabled;
        internal static ConfigEntry<bool> VerboseLogging;
        internal static ConfigEntry<bool> FollowerFactsDumpRequest = null;
        internal static ConfigEntry<bool> LiveDiagnosticsEnabled = null;
        internal static ConfigEntry<float> LiveDiagnosticsIntervalSeconds = null;
        internal static ConfigEntry<float> RosterCacheSeconds;
        internal static ConfigEntry<float> SnapshotCacheSeconds;
        internal static ConfigEntry<bool> SidecarEnabled;
        internal static ConfigEntry<bool> SidecarAutoStartEnabled;
        internal static ConfigEntry<string> SidecarExecutablePath;
        internal static ConfigEntry<string> SidecarDotnetPath;
        internal static ConfigEntry<float> SidecarSnapshotIntervalSeconds;
        internal static ConfigEntry<int> SidecarDecisionTimeoutSeconds;
        internal static ConfigEntry<int> SidecarReadyStaleSeconds;
        internal static ConfigEntry<float> ConversationWindowX;
        internal static ConfigEntry<float> ConversationWindowY;
        internal static ConfigEntry<float> ConversationWindowWidth;
        internal static ConfigEntry<float> ConversationWindowHeight;
        internal static ConfigEntry<int> ConversationFontSize;
        internal static ConfigEntry<float> ConversationWindowOpacity;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            BindConfig();
            FollowerAiProviderSetup.EnsureFirstRunFiles();
            Log.LogInfo("COTL AI NPC Character plugin loaded.");
            Log.LogInfo($"AI decision layer enabled={OpenAIEnabled.Value} route_available={OpenAiFollowerDecisionClient.CanAcceptRequests} sidecar_enabled={FollowerAiSidecarBridge.IsEnabledForDecisionRequests()} source={GetOpenAIConfigurationSource()}");
            FollowerAiSaveScope.Initialize();
            FollowerAIManager.LoadNPCStatuses();
            Log.LogInfo($"FollowerRecruit type: {typeof(FollowerRecruit).FullName}");
            Log.LogInfo($"UIFollowerIndoctrinationMenuController type: {typeof(UIFollowerIndoctrinationMenuController).FullName}");
            var harmony = new Harmony("io.github.blueskies2932.COTL_Characters");
            harmony.PatchAll();

            foreach (var method in harmony.GetPatchedMethods())
            {
                if (method.Name.Contains("DoRecruit") || method.Name.Contains("CompleteCallBack") || method.Name.Contains("Show"))
                {
                    Log.LogInfo($"Patched method: {method.DeclaringType.FullName}.{method.Name}");
                }
            }
        }

        private void Update()
        {
            FollowerAiSaveScope.Update();
            FollowerAiSidecarBridge.Update();
            FollowerAiConversationOverlay.Update();
            FollowerAiGameState.UpdatePauseLogging();
            FollowerAiLiveDiagnostics.Update();
            FollowerAiFollowerFacts.Update();
            FollowerAiTournamentOverlay.Update();
            FollowerAiInvocations.Update();
            FollowerAiInternetAccessOverlay.Update();
            if (!FollowerAiGameState.ShouldRunBackgroundBrainWork())
            {
                FollowerAIManager.UpdateDeferredSaves();
                return;
            }

            FollowerAiCurrentEventObservation.Update();
            FollowerAIManager.UpdateDeferredSaves();
        }

        private void OnGUI()
        {
            FollowerAiCultAboutOverlay.OnGUI();
            FollowerAiTournamentOverlay.OnGUI();
            FollowerAiInvocations.OnGUI();
            FollowerAiInternetAccessOverlay.OnGUI();
            FollowerAiConversationOverlay.OnGUI();
            FollowerAiProviderSetupOverlay.OnGUI();
        }

        private void OnApplicationQuit()
        {
            FollowerAiSidecarBridge.Shutdown();
        }

        private void OnDestroy()
        {
            FollowerAiSidecarBridge.Shutdown();
        }

        public static void SetNextFollowerNPCStatus(bool value)
        {
            NextFollowerIsNPC = value;
            NextFollowerMode = value ? FollowerAiMode.Character : FollowerAiMode.Vanilla;
            Log.LogInfo($"NPC Character toggle set to {value} for next recruited follower.");
        }

        public static void SetNextFollowerMode(FollowerAiMode mode)
        {
            NextFollowerMode = mode;
            NextFollowerIsNPC = mode != FollowerAiMode.Vanilla;
            Log.LogInfo($"NPC mode selector set to {mode} for next recruited follower.");
        }

        internal static bool IsVerboseLoggingEnabled()
        {
            return VerboseLogging != null && VerboseLogging.Value;
        }

        internal static void LogInfoVerbose(string message)
        {
            if (IsVerboseLoggingEnabled())
                Log?.LogInfo(message);
        }

        private static bool DetectProductBuild()
        {
#if PRODUCT_BUILD
            return true;
#else
            return false;
#endif
        }
    }
}

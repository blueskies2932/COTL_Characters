using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;

namespace COTL_AL_NPCs
{
    internal static class FollowerAiProviderSetupOverlay
    {
        private static readonly string[] PresetNames =
        {
            "OpenAI",
            "OpenRouter",
            "LM Studio",
            "Ollama",
            "Anthropic",
            "Gemini",
            "Mock",
            "Custom"
        };

        private static Rect windowRect = new Rect(0f, 0f, 1180f, 780f);
        private static Vector2 scroll;
        private static bool initialized;
        private static int selectedPreset;
        private static FollowerAiProviderSetupDraft draft = new FollowerAiProviderSetupDraft();
        private static string apiKey = string.Empty;
        private static bool saveKeyFile = true;
        private static bool saveEnvironmentVariable;
        private static string status = "Most players: choose a provider, paste the provider key once, then click Find, Test & Save Setup.";
        private static string[] fetchedModels = new string[0];
        private static int selectedFetchedModel = -1;
        private static Vector2 modelScroll;
        private static bool fetchingModels;
        private static readonly object modelFetchLock = new object();
        private static bool hasPendingModelFetchResult;
        private static string[] pendingFetchedModels = new string[0];
        private static string pendingFetchStatus = string.Empty;
        private static bool testingModel;
        private static bool hasPendingModelTestResult;
        private static bool pendingModelTestSuccess;
        private static string pendingModelTestStatus = string.Empty;
        private static string pendingModelTestModel = string.Empty;
        private static bool saveAfterPendingModelTest;
        private static string validatedSetupSignature = string.Empty;
        private static bool autoFindingModel;
        private static bool hasPendingAutoFindResult;
        private static bool pendingAutoFindSuccess;
        private static string pendingAutoFindStatus = string.Empty;
        private static string pendingAutoFindModel = string.Empty;
        private static string pendingAutoFindApiKey = string.Empty;
        private static string[] pendingAutoFindModels = new string[0];
        private static bool hasPendingAutoFindProgress;
        private static string pendingAutoFindProgress = string.Empty;
        private static string lastValidatedApiKey = string.Empty;
        private static bool setupSavedAwaitingSubmit;

        internal static void OnGUI()
        {
            if (AICharacterPlugin.OpenAIEnabled == null || !AICharacterPlugin.OpenAIEnabled.Value)
                return;

            if (FollowerAiProviderSetup.IsConfigured() && !setupSavedAwaitingSubmit)
            {
                initialized = false;
                return;
            }

            EnsureInitialized();
            ApplyPendingModelFetchResult();
            ApplyPendingModelTestResult();
            ApplyPendingAutoFindProgress();
            ApplyPendingAutoFindResult();

            var width = Mathf.Min(1180f, Screen.width * 0.9f);
            var height = Mathf.Min(820f, Screen.height * 0.88f);
            if (windowRect.x <= 0f && windowRect.y <= 0f)
                windowRect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            else
                windowRect = new Rect(
                    Mathf.Clamp(windowRect.x, 0f, Mathf.Max(0f, Screen.width - width)),
                    Mathf.Clamp(windowRect.y, 0f, Mathf.Max(0f, Screen.height - height)),
                    width,
                    height);

            windowRect = GUI.Window(982104, windowRect, DrawWindow, "AI Provider Setup Required");
        }

        private static void EnsureInitialized()
        {
            if (initialized)
                return;

            draft = FollowerAiProviderSetup.GetDraft();
            selectedPreset = FindPresetIndex(draft.ProviderType);
            apiKey = string.Empty;
            saveKeyFile = true;
            saveEnvironmentVariable = false;
            fetchedModels = new string[0];
            selectedFetchedModel = -1;
            initialized = true;
        }

        private static int FindPresetIndex(string providerType)
        {
            var normalized = (providerType ?? string.Empty).Trim().ToLowerInvariant();
            if (normalized == "openai")
                return 0;
            if (normalized == "openrouter")
                return 1;
            if (normalized == "openai-compatible")
                return 7;
            if (normalized == "anthropic")
                return 4;
            if (normalized == "gemini")
                return 5;
            if (normalized == "mock")
                return 6;
            return 0;
        }

        private static void ApplyPreset(int index)
        {
            selectedPreset = index;
            ClearFetchedModels();
            saveKeyFile = true;
            saveEnvironmentVariable = false;
            switch (index)
            {
                case 0:
                    draft.ProviderType = "openai";
                    draft.ApiKeyEnvVar = "OPENAI_API_KEY";
                    draft.BaseUrl = string.Empty;
                    draft.EndpointPath = "/responses";
                    draft.RequiresApiKey = true;
                    draft.Model = string.Empty;
                    break;
                case 1:
                    draft.ProviderType = "openrouter";
                    draft.ApiKeyEnvVar = "OPENROUTER_API_KEY";
                    draft.BaseUrl = "https://openrouter.ai/api/v1";
                    draft.EndpointPath = "/chat/completions";
                    draft.RequiresApiKey = true;
                    draft.Model = string.Empty;
                    break;
                case 2:
                    draft.ProviderType = "openai-compatible";
                    draft.ApiKeyEnvVar = string.Empty;
                    draft.BaseUrl = "http://localhost:1234/v1";
                    draft.EndpointPath = "/chat/completions";
                    draft.RequiresApiKey = false;
                    draft.Model = string.Empty;
                    break;
                case 3:
                    draft.ProviderType = "openai-compatible";
                    draft.ApiKeyEnvVar = string.Empty;
                    draft.BaseUrl = "http://localhost:11434/v1";
                    draft.EndpointPath = "/chat/completions";
                    draft.RequiresApiKey = false;
                    draft.Model = string.Empty;
                    break;
                case 4:
                    draft.ProviderType = "anthropic";
                    draft.ApiKeyEnvVar = "ANTHROPIC_API_KEY";
                    draft.BaseUrl = "https://api.anthropic.com/v1";
                    draft.EndpointPath = "/messages";
                    draft.RequiresApiKey = true;
                    draft.Model = string.Empty;
                    break;
                case 5:
                    draft.ProviderType = "gemini";
                    draft.ApiKeyEnvVar = "GEMINI_API_KEY";
                    draft.BaseUrl = "https://generativelanguage.googleapis.com";
                    draft.EndpointPath = "/v1beta/models/{model}:generateContent";
                    draft.RequiresApiKey = true;
                    draft.Model = string.Empty;
                    break;
                case 6:
                    draft.ProviderType = "mock";
                    draft.ApiKeyEnvVar = string.Empty;
                    draft.BaseUrl = string.Empty;
                    draft.EndpointPath = string.Empty;
                    draft.RequiresApiKey = false;
                    draft.Model = "mock";
                    break;
                case 7:
                    draft.ProviderType = "openai-compatible";
                    draft.ApiKeyEnvVar = "AI_PROVIDER_API_KEY";
                    draft.BaseUrl = string.Empty;
                    draft.EndpointPath = "/chat/completions";
                    draft.RequiresApiKey = true;
                    draft.Model = string.Empty;
                    break;
            }
        }

        private static void DrawWindow(int id)
        {
            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                wordWrap = true
            };
            labelStyle.normal.textColor = Color.white;

            var titleStyle = new GUIStyle(labelStyle)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold
            };

            var fieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 24
            };

            var buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 25,
                fontStyle = FontStyle.Bold
            };

            var statusStyle = BuildStatusStyle();

            GUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            GUILayout.Space(10f);
            GUILayout.Label("Set up the AI provider this mod will use.", titleStyle);
            GUILayout.Label("Most players only need three steps: choose the provider, paste the key from that provider's account dashboard once, then click Find, Test & Save Setup. The mod will ask the provider for model names, test likely chat models through the real NPC reply adapter, and save the first one that works.", labelStyle);
            GUILayout.Space(10f);

            scroll = GUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));

            GUILayout.Label("Provider", labelStyle);
            var newPreset = GUILayout.SelectionGrid(selectedPreset, PresetNames, 4, buttonStyle, GUILayout.Height(92f));
            if (newPreset != selectedPreset)
                ApplyPreset(newPreset);

            GUILayout.Space(14f);
            if (selectedPreset == 7)
            {
                GUILayout.Label("Custom provider details", labelStyle);
                DrawTextField("Provider type", ref draft.ProviderType, fieldStyle, labelStyle);
                DrawTextField("Base URL", ref draft.BaseUrl, fieldStyle, labelStyle);
                DrawTextField("Endpoint path", ref draft.EndpointPath, fieldStyle, labelStyle);
                DrawTextField("API key environment variable name", ref draft.ApiKeyEnvVar, fieldStyle, labelStyle);
                draft.RequiresApiKey = GUILayout.Toggle(draft.RequiresApiKey, "Custom provider requires an API key", labelStyle, GUILayout.Height(36f));
            }
            else
            {
                GUILayout.Label(BuildProviderSummary(), labelStyle);
            }

            GUILayout.Space(8f);
            if (draft.RequiresApiKey)
            {
                GUILayout.Space(10f);
                GUILayout.Label("API key", labelStyle);
                var newApiKey = GUILayout.PasswordField(apiKey ?? string.Empty, '*', fieldStyle, GUILayout.Height(42f));
                if (!string.Equals(newApiKey, apiKey, StringComparison.Ordinal))
                {
                    apiKey = newApiKey ?? string.Empty;
                    lastValidatedApiKey = string.Empty;
                    validatedSetupSignature = string.Empty;
                }
                GUILayout.Label("Paste the key here once. After a model test succeeds, the mod saves it to this Thunderstore profile's local key file. Keys are never shown to the AI model.", labelStyle);
            }
            else
            {
                GUILayout.Label("This provider preset does not require an API key.", labelStyle);
            }

            GUILayout.Space(10f);
            DrawTextField("Model", ref draft.Model, fieldStyle, labelStyle);
            GUILayout.Label("Advanced/manual field. If you do not know what works, use Find, Test & Save Setup instead of guessing.", labelStyle);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(autoFindingModel ? "Finding..." : "Find, Test & Save Setup", buttonStyle, GUILayout.Width(390f), GUILayout.Height(56f)))
                StartFindAndSaveWorkingModel();
            if (GUILayout.Button(fetchingModels ? "Fetching..." : "Fetch Models", buttonStyle, GUILayout.Width(190f), GUILayout.Height(56f)))
                StartFetchModels(false);
            if (GUILayout.Button(testingModel ? "Testing..." : "Test Selected Model", buttonStyle, GUILayout.Width(270f), GUILayout.Height(56f)))
                StartTestSelectedModel(saveWhenPassed: false);
            if (fetchedModels.Length > 0)
                GUILayout.Label($"{fetchedModels.Length} available model(s)", labelStyle, GUILayout.Height(56f));
            GUILayout.EndHorizontal();

            DrawFetchedModelPicker(buttonStyle, labelStyle);

            GUILayout.Space(10f);
            GUILayout.Label("Timeout seconds", labelStyle);
            var timeoutText = GUILayout.TextField(draft.TimeoutSeconds.ToString(), fieldStyle, GUILayout.Width(160f), GUILayout.Height(42f));
            if (int.TryParse(timeoutText, out var timeout))
                draft.TimeoutSeconds = Mathf.Clamp(timeout, 10, 600);

            GUILayout.EndScrollView();

            GUILayout.Space(10f);
            GUILayout.Box(status, statusStyle, GUILayout.ExpandWidth(true), GUILayout.Height(86f));
            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            var submitLabel = setupSavedAwaitingSubmit ? "Submit" : "Find, Test & Save Setup";
            if (GUILayout.Button(submitLabel, buttonStyle, GUILayout.Height(58f)))
                Save();
            if (GUILayout.Button("Reset", buttonStyle, GUILayout.Width(160f), GUILayout.Height(58f)))
                Reset();
            if (GUILayout.Button("Advanced File Setup", buttonStyle, GUILayout.Width(260f), GUILayout.Height(58f)))
                FollowerAiProviderSetup.OpenSetupTool();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 36f));
        }

        private static GUIStyle BuildStatusStyle()
        {
            var style = new GUIStyle(GUI.skin.box)
            {
                fontSize = 23,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(16, 16, 8, 8)
            };
            style.normal.textColor = GetStatusColor();
            return style;
        }

        private static Color GetStatusColor()
        {
            var normalized = (status ?? string.Empty).ToLowerInvariant();
            if (normalized.Contains("ready to save") ||
                normalized.Contains("saved") ||
                normalized.Contains("working model") ||
                normalized.Contains("test passed"))
                return new Color(0.45f, 1f, 0.45f);

            if (normalized.Contains("failed") ||
                normalized.Contains("could not") ||
                normalized.Contains("not saved") ||
                normalized.Contains("rejected") ||
                normalized.Contains("timed out") ||
                normalized.Contains("none worked"))
                return new Color(1f, 0.55f, 0.45f);

            if (normalized.Contains("testing") ||
                normalized.Contains("fetching") ||
                normalized.Contains("finding") ||
                normalized.Contains("wait"))
                return new Color(1f, 0.9f, 0.45f);

            return Color.white;
        }

        private static string BuildProviderSummary()
        {
            var providerName = selectedPreset >= 0 && selectedPreset < PresetNames.Length
                ? PresetNames[selectedPreset]
                : draft.ProviderType;

            var modelHint = string.IsNullOrWhiteSpace(draft.Model)
                ? "No model selected yet."
                : $"Selected model: {draft.Model}.";

            return $"{providerName} provider selected. {modelHint} Use Advanced File Setup only if you need custom endpoints or environment-variable setup.";
        }

        private static void DrawTextField(string label, ref string value, GUIStyle fieldStyle, GUIStyle labelStyle)
        {
            GUILayout.Label(label, labelStyle);
            value = GUILayout.TextField(value ?? string.Empty, fieldStyle, GUILayout.Height(42f));
        }

        private static void Save()
        {
            if (setupSavedAwaitingSubmit)
            {
                setupSavedAwaitingSubmit = false;
                initialized = false;
                return;
            }

            if (testingModel || fetchingModels || autoFindingModel)
            {
                status = "Wait for the current provider/model check to finish before saving.";
                return;
            }

            var saveApiKey = GetApiKeyForSave();
            if (RequiresModelTest() && !string.Equals(validatedSetupSignature, BuildValidationSignature(saveApiKey), StringComparison.Ordinal))
            {
                if (!CanContactSelectedProvider(out var contactMessage))
                {
                    status = contactMessage;
                    return;
                }

                status = "Finding, testing, and saving a model that actually works with this provider key...";
                StartFindAndSaveWorkingModel();
                return;
            }

            if (FollowerAiProviderSetup.SaveSetup(draft, saveApiKey, saveKeyFile, saveEnvironmentVariable, out var message))
            {
                status = $"SAVED: {message} Click Submit to close this setup screen.";
                apiKey = string.Empty;
                lastValidatedApiKey = string.Empty;
                setupSavedAwaitingSubmit = true;
                FollowerAiSidecarBridge.RestartForProviderSetupChange();
                return;
            }

            status = message;
        }

        private static bool RequiresModelTest()
        {
            return !string.Equals(draft.ProviderType, "mock", StringComparison.OrdinalIgnoreCase);
        }

        private static void StartFetchModels(bool quiet)
        {
            if (fetchingModels)
                return;

            if (!HasPastedSetupKeyForProvider(out var keyMessage))
            {
                status = keyMessage;
                return;
            }

            fetchingModels = true;
            if (!quiet)
                status = "Fetching models from the selected provider...";

            var requestDraft = CloneDraftForProviderTest(draft, clampTimeoutForSetup: false);
            var requestApiKey = GetPastedSetupKey();
            var fetchDraft = CloneDraftForProviderTest(requestDraft, clampTimeoutForSetup: false);
            fetchDraft.ApiKeyEnvVar = string.Empty;
            requestDraft = fetchDraft;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                var models = FollowerAiProviderSetup.FetchAvailableModels(requestDraft, requestApiKey, out var message);
                models = RankModelCandidates(requestDraft.ProviderType, requestDraft.Model, models).ToArray();
                lock (modelFetchLock)
                {
                    pendingFetchedModels = models ?? new string[0];
                    pendingFetchStatus = message;
                    hasPendingModelFetchResult = true;
                }
            });
        }

        private static void StartFindAndSaveWorkingModel()
        {
            if (autoFindingModel || testingModel || fetchingModels)
                return;

            if (!CanContactSelectedProvider(out var message))
            {
                status = message;
                return;
            }

            if (string.Equals(draft.ProviderType, "mock", StringComparison.OrdinalIgnoreCase))
            {
                draft.Model = "mock";
                validatedSetupSignature = BuildValidationSignature(GetApiKeyForSave());
                Save();
                return;
            }

            autoFindingModel = true;
            status = "Finding a working model. The mod will fetch model names when possible, then test likely chat models for you...";

            var requestDraft = CloneDraftForProviderTest(draft, clampTimeoutForSetup: true);
            requestDraft.ApiKeyEnvVar = string.Empty;
            var requestApiKey = GetPastedSetupKey();

            ThreadPool.QueueUserWorkItem(_ =>
            {
                var testedModels = new List<string>();
                try
                {
                    var fetched = FollowerAiProviderSetup.FetchAvailableModels(requestDraft, requestApiKey, out var fetchMessage);
                    var candidates = BuildAutoFindCandidates(requestDraft.ProviderType, requestDraft.Model, fetched).ToArray();
                    if (candidates.Length == 0)
                    {
                        CompleteAutoFind(false, string.IsNullOrWhiteSpace(fetchMessage)
                            ? "No model candidates were found. Type a model manually, or check that the provider/local server is running."
                            : $"{fetchMessage} Type a model manually, or check that the provider/local server is running.", string.Empty, string.Empty, fetched);
                        return;
                    }

                    var limit = candidates.Length;
                    for (var index = 0; index < limit; index++)
                    {
                        var candidate = candidates[index];
                        testedModels.Add(candidate);
                        ReportAutoFindProgress($"Testing model {index + 1} of {limit}: {candidate}");

                        var testDraft = CloneDraftForProviderTest(requestDraft, clampTimeoutForSetup: true);
                        testDraft.Model = candidate;
                        var success = FollowerAiSidecarBridge.TryTestProviderConfiguration(testDraft, requestApiKey, out var testMessage);
                        if (!success)
                            continue;

                        CompleteAutoFind(true, $"Found a working model: {candidate}", candidate, requestApiKey, candidates);
                        return;
                    }

                    CompleteAutoFind(
                        false,
                        $"The mod tested {testedModels.Count} likely model(s), but none worked. Use Fetch Models, pick one manually, then Test Selected Model. Last fetch result: {fetchMessage}",
                        string.Empty,
                        string.Empty,
                        candidates);
                }
                catch (Exception ex)
                {
                    CompleteAutoFind(false, $"Model discovery failed: {ex.Message}", string.Empty, string.Empty, testedModels.ToArray());
                }
            });
        }

        private static void StartTestSelectedModel(bool saveWhenPassed)
        {
            if (testingModel)
                return;

            if (string.IsNullOrWhiteSpace(draft.Model))
            {
                status = "Choose or enter a model before testing.";
                return;
            }

            testingModel = true;
            status = "Testing this provider/model with the same adapter path the mod will use...";

            var requestDraft = CloneDraftForProviderTest(draft, clampTimeoutForSetup: false);
            requestDraft.ApiKeyEnvVar = string.Empty;
            var requestApiKey = GetPastedSetupKey();
            var signature = BuildValidationSignature(requestApiKey);
            var testedModel = draft.Model ?? string.Empty;
            saveAfterPendingModelTest = saveWhenPassed;
            pendingModelTestModel = testedModel;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                var success = FollowerAiSidecarBridge.TryTestProviderConfiguration(requestDraft, requestApiKey, out var message);
                lock (modelFetchLock)
                {
                    pendingModelTestSuccess = success;
                    pendingModelTestStatus = message;
                    if (success)
                    {
                        validatedSetupSignature = signature;
                        lastValidatedApiKey = requestApiKey;
                    }
                    else
                    {
                        validatedSetupSignature = string.Empty;
                        lastValidatedApiKey = string.Empty;
                    }
                    hasPendingModelTestResult = true;
                }
            });
        }

        private static void ApplyPendingModelTestResult()
        {
            bool success;
            string message;
            string model;
            bool saveAfterSuccess;
            lock (modelFetchLock)
            {
                if (!hasPendingModelTestResult)
                    return;

                success = pendingModelTestSuccess;
                message = pendingModelTestStatus ?? string.Empty;
                model = pendingModelTestModel ?? string.Empty;
                saveAfterSuccess = saveAfterPendingModelTest;
                pendingModelTestSuccess = false;
                pendingModelTestStatus = string.Empty;
                pendingModelTestModel = string.Empty;
                saveAfterPendingModelTest = false;
                hasPendingModelTestResult = false;
            }

            testingModel = false;
            if (success && saveAfterSuccess)
            {
                status = $"Test passed for {model}. Saving setup now...";
                Save();
                return;
            }

            status = success
                ? $"READY TO SAVE: test passed for {model}. Click Find, Test & Save Setup to save it. {message}"
                : $"TEST FAILED: {message}";
        }

        private static void ApplyPendingAutoFindProgress()
        {
            string message;
            lock (modelFetchLock)
            {
                if (!hasPendingAutoFindProgress)
                    return;

                message = pendingAutoFindProgress ?? string.Empty;
                pendingAutoFindProgress = string.Empty;
                hasPendingAutoFindProgress = false;
            }

            if (!string.IsNullOrWhiteSpace(message))
                status = message;
        }

        private static void ApplyPendingAutoFindResult()
        {
            bool success;
            string message;
            string model;
            string testedApiKey;
            string[] models;
            lock (modelFetchLock)
            {
                if (!hasPendingAutoFindResult)
                    return;

                success = pendingAutoFindSuccess;
                message = pendingAutoFindStatus ?? string.Empty;
                model = pendingAutoFindModel ?? string.Empty;
                testedApiKey = pendingAutoFindApiKey ?? string.Empty;
                models = pendingAutoFindModels ?? new string[0];
                pendingAutoFindSuccess = false;
                pendingAutoFindStatus = string.Empty;
                pendingAutoFindModel = string.Empty;
                pendingAutoFindApiKey = string.Empty;
                pendingAutoFindModels = new string[0];
                hasPendingAutoFindResult = false;
            }

            autoFindingModel = false;
            fetchedModels = models;
            selectedFetchedModel = -1;
            if (!string.IsNullOrWhiteSpace(model))
            {
                draft.Model = model;
                selectedFetchedModel = FindFetchedModelIndex(model);
            }

            if (success)
            {
                lastValidatedApiKey = testedApiKey;
                validatedSetupSignature = BuildValidationSignature(testedApiKey);
                if (FollowerAiProviderSetup.SaveSetup(draft, GetApiKeyForSave(testedApiKey), saveKeyFile, saveEnvironmentVariable, out var saveMessage))
                {
                    status = $"SAVED: {message}. {saveMessage} Click Submit to close this setup screen.";
                    apiKey = string.Empty;
                    lastValidatedApiKey = string.Empty;
                    setupSavedAwaitingSubmit = true;
                    FollowerAiSidecarBridge.RestartForProviderSetupChange();
                    return;
                }

                status = $"{message} Found, but could not save: {saveMessage}";
                return;
            }

            status = message;
        }

        private static void ApplyPendingModelFetchResult()
        {
            string[] models;
            string message;
            lock (modelFetchLock)
            {
                if (!hasPendingModelFetchResult)
                    return;

                models = pendingFetchedModels ?? new string[0];
                message = pendingFetchStatus ?? string.Empty;
                pendingFetchedModels = new string[0];
                pendingFetchStatus = string.Empty;
                hasPendingModelFetchResult = false;
            }

            fetchedModels = models;
            selectedFetchedModel = FindFetchedModelIndex(draft.Model);
            status = fetchedModels.Length > 0
                ? $"{message} No model has been selected yet. Use Find, Test & Save Setup to test and save a working model, or click a model below for manual testing."
                : message;
            fetchingModels = false;
        }

        private static int FindFetchedModelIndex(string model)
        {
            if (string.IsNullOrWhiteSpace(model) || fetchedModels == null)
                return -1;

            for (var i = 0; i < fetchedModels.Length; i++)
            {
                if (string.Equals(fetchedModels[i], model, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static void DrawFetchedModelPicker(GUIStyle buttonStyle, GUIStyle labelStyle)
        {
            if (fetchedModels == null || fetchedModels.Length == 0)
                return;

            GUILayout.Label("Available models", labelStyle);
            var pickerHeight = Mathf.Min(280f, fetchedModels.Length * 48f + 8f);
            modelScroll = GUILayout.BeginScrollView(modelScroll, GUILayout.Height(pickerHeight));
            var newSelection = selectedFetchedModel;
            for (var i = 0; i < fetchedModels.Length; i++)
            {
                var prefix = i == selectedFetchedModel ? "> " : string.Empty;
                if (GUILayout.Button($"{prefix}{fetchedModels[i]}", buttonStyle, GUILayout.Height(46f)))
                    newSelection = i;
            }
            GUILayout.EndScrollView();

            if (newSelection != selectedFetchedModel && newSelection >= 0 && newSelection < fetchedModels.Length)
            {
                selectedFetchedModel = newSelection;
                draft.Model = fetchedModels[newSelection];
                validatedSetupSignature = string.Empty;
                lastValidatedApiKey = string.Empty;
                status = $"Selected model: {draft.Model}";
            }
        }

        private static void Reset()
        {
            FollowerAiProviderSetup.ResetSetupFiles();
            ClearFetchedModels();
            setupSavedAwaitingSubmit = false;
            initialized = false;
            status = "AI provider setup was reset. Choose a provider, enter a model/key if needed, then Save.";
        }

        private static void ClearFetchedModels()
        {
            fetchedModels = new string[0];
            selectedFetchedModel = -1;
            modelScroll = Vector2.zero;
            validatedSetupSignature = string.Empty;
            lastValidatedApiKey = string.Empty;
            testingModel = false;
            pendingModelTestModel = string.Empty;
            saveAfterPendingModelTest = false;
            autoFindingModel = false;
            lock (modelFetchLock)
            {
                pendingFetchedModels = new string[0];
                pendingFetchStatus = string.Empty;
                hasPendingModelFetchResult = false;
                pendingModelTestSuccess = false;
                pendingModelTestStatus = string.Empty;
                pendingModelTestModel = string.Empty;
                saveAfterPendingModelTest = false;
                hasPendingModelTestResult = false;
                pendingAutoFindSuccess = false;
                pendingAutoFindStatus = string.Empty;
                pendingAutoFindModel = string.Empty;
                pendingAutoFindApiKey = string.Empty;
                pendingAutoFindModels = new string[0];
                hasPendingAutoFindResult = false;
                pendingAutoFindProgress = string.Empty;
                hasPendingAutoFindProgress = false;
            }
        }

        private static bool CanContactSelectedProvider(out string message)
        {
            message = string.Empty;
            if (!HasPastedSetupKeyForProvider(out var keyMessage))
            {
                message = keyMessage;
                return false;
            }

            if (string.Equals(draft.ProviderType, "mock", StringComparison.OrdinalIgnoreCase))
            {
                draft.Model = "mock";
                validatedSetupSignature = BuildValidationSignature(GetApiKeyForSave());
                return true;
            }

            return true;
        }

        private static FollowerAiProviderSetupDraft CloneDraftForProviderTest(FollowerAiProviderSetupDraft source, bool clampTimeoutForSetup)
        {
            return new FollowerAiProviderSetupDraft
            {
                ProviderType = source.ProviderType,
                ApiKeyEnvVar = source.ApiKeyEnvVar,
                BaseUrl = source.BaseUrl,
                EndpointPath = source.EndpointPath,
                Model = source.Model,
                RequiresApiKey = source.RequiresApiKey,
                TimeoutSeconds = clampTimeoutForSetup
                    ? Math.Max(10, Math.Min(30, source.TimeoutSeconds))
                    : source.TimeoutSeconds
            };
        }

        private static IEnumerable<string> BuildAutoFindCandidates(string providerType, string preferredModel, string[] fetched)
        {
            var candidates = new List<string>();
            var fetchedSet = fetched ?? Array.Empty<string>();
            if (!string.IsNullOrWhiteSpace(preferredModel) &&
                !fetchedSet.Contains(preferredModel, StringComparer.OrdinalIgnoreCase))
            {
                candidates.Add(preferredModel.Trim());
            }

            candidates.AddRange(RankModelCandidates(providerType, preferredModel, fetchedSet));
            candidates.AddRange(GetFallbackCandidates(providerType));

            return candidates
                .Where(model => !string.IsNullOrWhiteSpace(model))
                .Select(model => model.Trim())
                .Where(IsLikelyChatModel)
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> RankModelCandidates(string providerType, string preferredModel, IEnumerable<string> models)
        {
            var normalizedProvider = (providerType ?? string.Empty).Trim().ToLowerInvariant();
            return (models ?? Array.Empty<string>())
                .Where(model => !string.IsNullOrWhiteSpace(model))
                .Select(model => model.Trim())
                .Where(IsLikelyChatModel)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(model => ScoreModelCandidate(normalizedProvider, preferredModel, model))
                .ThenBy(model => model, StringComparer.OrdinalIgnoreCase);
        }

        private static int ScoreModelCandidate(string providerType, string preferredModel, string model)
        {
            var id = (model ?? string.Empty).Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(preferredModel) &&
                string.Equals(id, preferredModel.Trim(), StringComparison.OrdinalIgnoreCase))
                return -1000;

            var score = 500;
            if (id.Contains("mini") || id.Contains("flash") || id.Contains("haiku") || id.Contains("lite") || id.Contains("small"))
                score -= 180;
            if (id.Contains("gpt-4o-mini") || id.Contains("gpt-4.1-mini") || id.Contains("gpt-5") && id.Contains("mini"))
                score -= 140;
            if (id.Contains("gpt-4o") || id.Contains("gpt-4.1"))
                score -= 90;
            if (id.StartsWith("gpt-", StringComparison.Ordinal))
                score -= 70;
            if (id.Contains("claude") || id.Contains("gemini"))
                score -= 55;
            if (id.Contains("turbo") || id.Contains("fast"))
                score -= 35;
            if (id.Contains("preview") || id.Contains("experimental") || id.Contains("beta"))
                score += 45;
            if (id.Contains("pro") || id.Contains("opus") || id.Contains("large"))
                score += 55;
            if (id.StartsWith("o", StringComparison.Ordinal))
                score += 85;
            if (providerType == "openrouter" && id.Contains(":free"))
                score -= 25;
            return score;
        }

        private static bool IsLikelyChatModel(string model)
        {
            if (string.IsNullOrWhiteSpace(model))
                return false;

            var id = model.Trim().ToLowerInvariant();
            var blocked = new[]
            {
                "embedding",
                "embed",
                "whisper",
                "tts",
                "audio",
                "image",
                "dall-e",
                "moderation",
                "realtime",
                "transcribe",
                "speech",
                "rerank",
                "vision-only"
            };
            return !blocked.Any(id.Contains);
        }

        private static IEnumerable<string> GetFallbackCandidates(string providerType)
        {
            var normalized = (providerType ?? string.Empty).Trim().ToLowerInvariant();
            if (normalized == "openai")
            {
                yield return "gpt-4o-mini";
                yield return "gpt-4.1-mini";
                yield return "gpt-4o";
                yield return "gpt-4.1";
                yield return "gpt-3.5-turbo";
                yield break;
            }

            if (normalized == "openrouter")
            {
                yield return "openai/gpt-4o-mini";
                yield return "anthropic/claude-3.5-haiku";
                yield return "google/gemini-flash-1.5";
                yield break;
            }

            if (normalized == "anthropic" || normalized == "claude")
            {
                yield return "claude-3-5-haiku-latest";
                yield return "claude-3-5-sonnet-latest";
                yield break;
            }

            if (normalized == "gemini" || normalized == "google" || normalized == "google-gemini")
            {
                yield return "gemini-2.0-flash";
                yield return "gemini-1.5-flash";
                yield break;
            }
        }

        private static void ReportAutoFindProgress(string message)
        {
            lock (modelFetchLock)
            {
                pendingAutoFindProgress = message ?? string.Empty;
                hasPendingAutoFindProgress = true;
            }
        }

        private static void CompleteAutoFind(bool success, string message, string model, string testedApiKey, IEnumerable<string> models)
        {
            lock (modelFetchLock)
            {
                pendingAutoFindSuccess = success;
                pendingAutoFindStatus = message ?? string.Empty;
                pendingAutoFindModel = model ?? string.Empty;
                pendingAutoFindApiKey = testedApiKey ?? string.Empty;
                pendingAutoFindModels = (models ?? Array.Empty<string>()).ToArray();
                hasPendingAutoFindResult = true;
            }
        }

        private static string GetApiKeyForSave(string preferredKey = null)
        {
            if (!string.IsNullOrWhiteSpace(preferredKey))
                return preferredKey;

            var pastedKey = GetPastedSetupKey();
            if (!string.IsNullOrWhiteSpace(pastedKey))
                return pastedKey;

            return lastValidatedApiKey ?? string.Empty;
        }

        private static string GetPastedSetupKey()
        {
            return (apiKey ?? string.Empty).Trim();
        }

        private static bool HasPastedSetupKeyForProvider(out string message)
        {
            message = string.Empty;
            if (draft?.RequiresApiKey != true)
                return true;

            if (!string.IsNullOrWhiteSpace(GetPastedSetupKey()))
                return true;

            message = "Paste the provider API key into the API key field first. The in-game setup does not use environment-variable fallback.";
            return false;
        }

        private static string BuildValidationSignature(string keyForValidation)
        {
            var value =
                $"{draft.ProviderType}\n{draft.ApiKeyEnvVar}\n{draft.BaseUrl}\n{draft.EndpointPath}\n{draft.Model}\n{draft.RequiresApiKey}\n{draft.TimeoutSeconds}\n{HashForSignature(keyForValidation)}";
            return HashForSignature(value);
        }

        private static string HashForSignature(string value)
        {
            try
            {
                using (var sha = SHA256.Create())
                {
                    var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                    return Convert.ToBase64String(bytes);
                }
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace COTL_AL_NPCs
{
    internal static partial class FollowerAiConversationOverlay
    {
        private const float NativeInteractionCloseGraceSeconds = 0.75f;
        private const string InvocationReactionSystemMessage = "A terrible force has just passed through you like black lightning, using you for nefarious purposes beyond your comprehension or control. You do not know what happened, but you know it happened to you. React in character.";

        private static bool isOpen;
        private static int speakerID = -1;
        private static Rect windowRect;
        private static Rect lastSavedWindowRect;
        private static bool windowRectInitialized;
        private static string playerText = string.Empty;
        private static string transcript = string.Empty;
        private static Vector2 transcriptScroll;
        private static float transcriptCopiedUntilRealtime;
        private static float openedAtRealtime;
        private static float nextWindowConfigSaveAtRealtime;
        private static float nextControllerScrollAtRealtime;
        private static bool resizingWindow;
        private static Vector2 resizeStartMouseScreen;
        private static Vector2 resizeStartSize;
        private static PendingOpenAiRequest pendingOpenAiRequest;

        private static GUIStyle windowStyle;
        private static GUIStyle transcriptStyle;
        private static GUIStyle commandLabelStyle;
        private static GUIStyle textAreaStyle;
        private static GUIStyle buttonStyle;
        private static GUIStyle iconButtonStyle;
        private static GUIStyle resizeHandleStyle;
        private static int styleFontSize = -1;
        private static float styleWindowOpacity = -1f;
        private static Texture2D windowBackgroundTexture;
        private static Texture2D copyIconTexture;
        private static readonly HashSet<string> unavailableAxisNames = new HashSet<string>();
        private static readonly object pendingLock = new object();

        private sealed class PendingOpenAiRequest
        {
            public int SpeakerID;
            public string PlayerText;
            public OpenAiFollowerDecisionContext Context;
            public bool IsInvocationReply;
            public string InvocationReceipt = string.Empty;
            public bool Complete;
            public bool Success;
            public OpenAiFollowerDecision Decision;
            public string Message;
            public bool ProgressReady;
            public bool ProgressShown;
            public string ProgressMessage;
        }

        internal static void Open(int followerID)
        {
            if (isOpen && speakerID == followerID)
                return;

            EnsureWindowRectInitialized();
            speakerID = followerID;
            isOpen = true;
            FollowerAiOverlayInputBlocker.Show("conversation");
            openedAtRealtime = Time.realtimeSinceStartup;
            characterLoreExpanded = false;
            characterLoreDraft = FollowerAiCharacterModeSettings.Get(followerID).LoreText ?? string.Empty;
            characterLoreDirty = false;
            characterLoreSavedMessageUntilRealtime = 0f;
            characterLoreScroll = Vector2.zero;
            playerText = string.Empty;
            transcript = string.Empty;
            transcriptScroll = Vector2.zero;
            AICharacterPlugin.Log.LogInfo($"AI conversation opened for follower {followerID}.");
        }

        internal static void NotifyFollowerInteractionClosed(int followerID)
        {
            if (speakerID < 0 || followerID != speakerID)
                return;

            if (ShouldKeepOpenForNativeMenuSettling($"follower interaction close for follower {followerID}"))
                return;

            CloseTextWindowForNativeInteraction();
        }

        internal static void NotifyNativeInteractionMenuClosed()
        {
            if (speakerID < 0)
                return;

            if (ShouldKeepOpenForNativeMenuSettling("native interaction wheel hide"))
                return;

            CloseTextWindowForNativeInteraction();
        }

        internal static void Update()
        {
            ShowPendingOpenAiProgress();
            CompletePendingOpenAiRequest();
            HandleContinuousScrollInput();
        }

        internal static bool IsBlockingAutonomy(int followerID)
        {
            return speakerID == followerID && (isOpen || HasAnyPendingOpenAiRequest());
        }

        internal static void ResetForSaveScopeChange()
        {
            lock (pendingLock)
                pendingOpenAiRequest = null;

            isOpen = false;
            FollowerAiOverlayInputBlocker.Hide("conversation");
            speakerID = -1;
            playerText = string.Empty;
            transcript = string.Empty;
            transcriptScroll = Vector2.zero;
        }

        internal static void ClearLiveConversationForFollower(int followerID, string source)
        {
            if (speakerID != followerID)
                return;

            AICharacterPlugin.LogInfoVerbose($"Clearing live conversation state for follower {followerID}: {source}");
            ResetForSaveScopeChange();
        }

        internal static void OnGUI()
        {
            if (!isOpen)
                return;

            EnsureWindowRectInitialized();
            EnsureStyles();
            GUI.depth = -1000;
            windowRect = GUI.Window(192041, windowRect, DrawWindow, "AI Follower", windowStyle);
            windowRect = ClampToScreen(windowRect);
            if (FollowerAIManager.GetMode(speakerID) == FollowerAiMode.Character)
            {
                DrawCharacterLoreLeafTab();
                if (characterLoreExpanded)
                    GUI.Window(192042, GetCharacterLoreLeafRect(), DrawCharacterLoreLeaf, "Lore", windowStyle);
            }
            SaveWindowRectIfChanged();
            FollowerAiOverlayInputBlocker.ConsumeImGuiPointerEvents();
        }

        private static void DrawWindow(int windowID)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            HandleKeyboardSendShortcut();
            HandleKeyboardScrollShortcut();
            HandleKeyboardCloseShortcut();

            var fontSize = GetFontSize();
            DrawCharacterAwarenessSettings();
            var textAreaHeight = Mathf.Clamp(fontSize * 3.6f, 82f, windowRect.height * 0.32f);
            var buttonHeight = Mathf.Clamp(fontSize * 1.65f, 44f, 78f);
            var awarenessHeight = GetCharacterAwarenessHeight();
            var transcriptHeight = Mathf.Max(80f, windowRect.height - textAreaHeight - buttonHeight - awarenessHeight - 104f);
            transcriptScroll = GUILayout.BeginScrollView(transcriptScroll, GUILayout.Height(transcriptHeight), GUILayout.ExpandWidth(true));
            GUILayout.Label(transcript, transcriptStyle, GUILayout.ExpandWidth(true));
            GUILayout.EndScrollView();

            GUILayout.Space(10f);
            GUILayout.Label("Command", commandLabelStyle);
            GUI.SetNextControlName("AICommandText");
            playerText = GUILayout.TextArea(playerText, textAreaStyle, GUILayout.Height(textAreaHeight), GUILayout.ExpandWidth(true));
            if (string.IsNullOrEmpty(GUI.GetNameOfFocusedControl()))
                GUI.FocusControl("AICommandText");

            GUILayout.BeginHorizontal();
            GUI.enabled = !HasPendingOpenAiRequest();
            if (GUILayout.Button(OpenAiFollowerDecisionClient.CanAcceptRequests ? "Send to AI" : "Send", buttonStyle, GUILayout.Height(buttonHeight)))
                Send();
            GUI.enabled = true;
            GUI.enabled = !string.IsNullOrWhiteSpace(transcript);
            if (GUILayout.Button(new GUIContent(copyIconTexture, "Copy transcript"), iconButtonStyle, GUILayout.Width(Mathf.Max(78f, fontSize * 1.55f)), GUILayout.Height(buttonHeight)))
                CopyTranscriptToClipboard();
            GUI.enabled = true;
            if (GUILayout.Button("Close", buttonStyle, GUILayout.Width(Mathf.Max(150f, fontSize * 3.1f)), GUILayout.Height(buttonHeight)))
                CloseTextWindowForNativeInteraction();
            if (Time.realtimeSinceStartup < transcriptCopiedUntilRealtime)
                GUILayout.Label("Copied.", commandLabelStyle, GUILayout.Width(Mathf.Max(110f, fontSize * 2.7f)));
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            HandleResizeGrip();
            if (!resizingWindow)
                GUI.DragWindow(new Rect(0f, 0f, windowRect.width - GetResizeGripSize(), Math.Max(34f, GetFontSize() * 1.2f)));
        }

        private static void Send()
        {
            try
            {
                var submittedText = playerText?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(submittedText))
                    return;

                if (FollowerAiInvocations.TryHandleSubmittedCode(speakerID, submittedText, out var invocationReceipt, out _))
                {
                    AppendTranscript("Lamb: *forbidden words of power thundered in an old tongue*");
                    StartOpenAiInvocationReply(invocationReceipt);
                    return;
                }

                var priorTranscript = transcript;
                AppendTranscript($"Lamb: {submittedText}");

                if (OpenAiFollowerDecisionClient.CanAcceptRequests)
                {
                    StartOpenAiDecision(submittedText, priorTranscript);
                    return;
                }

                AppendTranscript($"{GetSpeakerName()}: {BuildNoAiReplyTranscript()}");
                playerText = string.Empty;
                ScrollTranscriptToLatest();
            }
            catch (Exception ex)
            {
                FollowerAiDiagnostics.Record("conversation send exception", ex.ToString(), speakerID, -1, null, playerText);
                AppendTranscript($"{GetSpeakerName()}: {BuildNoAiReplyTranscript()}");
                AICharacterPlugin.Log.LogError($"AI command failed: {ex}");
            }
        }

        private static void StartOpenAiDecision(string submittedText, string priorTranscript)
        {
            if (HasPendingOpenAiRequest())
                return;

            var context = OpenAiFollowerDecisionClient.CreateContext(speakerID, submittedText);
            if (FollowerAIManager.GetMode(speakerID) == FollowerAiMode.Character)
            {
                context.CharacterModeEnabled = true;
                context.CharacterAwareness = FollowerAiCharacterModeSettings.Get(speakerID);
                context.ReplyLength = context.CharacterAwareness.ReplyLength;
                context.ActiveConversationTranscript = BuildActiveConversationTranscriptForModel(priorTranscript);
                if (context.CharacterAwareness.LongTermConversationHistory)
                    context.CharacterModeConversationHistory = BuildCharacterModeConversationHistoryForModel();
            }
            else
            {
                context.ActiveConversationTranscript = BuildActiveConversationTranscriptForModel(priorTranscript);
            }

            QueueOpenAiRequest(new PendingOpenAiRequest
            {
                SpeakerID = speakerID,
                PlayerText = submittedText,
                Context = context,
                Complete = false
            });
        }

        private static void StartOpenAiInvocationReply(string receipt)
        {
            if (!OpenAiFollowerDecisionClient.CanAcceptRequests)
            {
                AppendTranscript(receipt);
                playerText = string.Empty;
                ScrollTranscriptToLatest();
                return;
            }

            var context = OpenAiFollowerDecisionClient.CreateInvocationReplyContext(
                speakerID,
                receipt,
                InvocationReactionSystemMessage);

            QueueOpenAiRequest(new PendingOpenAiRequest
            {
                SpeakerID = speakerID,
                PlayerText = receipt,
                Context = context,
                IsInvocationReply = true,
                InvocationReceipt = receipt,
                Complete = false
            });
        }

        private static void QueueOpenAiRequest(PendingOpenAiRequest request)
        {
            lock (pendingLock)
                pendingOpenAiRequest = request;

            playerText = string.Empty;
            ScrollTranscriptToLatest();

            ThreadPool.QueueUserWorkItem(_ =>
            {
                var success = OpenAiFollowerDecisionClient.TryDecide(request.Context, out var decision, out var message, progress =>
                {
                    lock (pendingLock)
                    {
                        if (pendingOpenAiRequest == request && !request.ProgressShown)
                        {
                            request.ProgressMessage = progress;
                            request.ProgressReady = true;
                        }
                    }
                });

                lock (pendingLock)
                {
                    request.Success = success;
                    request.Decision = decision;
                    request.Message = message;
                    request.Complete = true;
                }
            });
        }

        private static void ShowPendingOpenAiProgress()
        {
            string progress = null;
            lock (pendingLock)
            {
                if (pendingOpenAiRequest == null ||
                    pendingOpenAiRequest.Complete ||
                    !pendingOpenAiRequest.ProgressReady ||
                    pendingOpenAiRequest.ProgressShown ||
                    string.IsNullOrWhiteSpace(pendingOpenAiRequest.ProgressMessage))
                    return;

                progress = pendingOpenAiRequest.ProgressMessage;
                pendingOpenAiRequest.ProgressShown = true;
            }

            AppendTranscript(progress);
            ScrollTranscriptToLatest();
        }

        private static void CompletePendingOpenAiRequest()
        {
            PendingOpenAiRequest request = null;
            lock (pendingLock)
            {
                if (pendingOpenAiRequest == null || !pendingOpenAiRequest.Complete)
                    return;

                request = pendingOpenAiRequest;
                pendingOpenAiRequest = null;
            }

            if (request.Success)
            {
                var reply = request.Decision?.Reply ?? string.Empty;
                if (string.IsNullOrWhiteSpace(reply))
                    reply = BuildNoAiReplyTranscript();

                AppendTranscript($"{GetSpeakerName(request.SpeakerID)}: {reply.Trim()}");
                if (request.IsInvocationReply && !string.IsNullOrWhiteSpace(request.InvocationReceipt))
                    AppendTranscript(request.InvocationReceipt);

                RecordCharacterModeMemory(request, reply);
                AICharacterPlugin.Log.LogInfo($"AI conversation reply generated: {request.Message}");
            }
            else
            {
                FollowerAiDiagnostics.Record("AI reply failed", request.Message, request.SpeakerID, -1, null, request.PlayerText);
                AppendTranscript($"{GetSpeakerName(request.SpeakerID)}: {BuildNoAiReplyTranscript(request.Message)}");
                AICharacterPlugin.Log.LogWarning($"AI reply failed: {request.Message}");
            }

            ScrollTranscriptToLatest();
        }

        private static string BuildActiveConversationTranscriptForModel(string rawTranscript)
        {
            if (string.IsNullOrWhiteSpace(rawTranscript))
                return string.Empty;

            var entries = rawTranscript
                .Split(new[] { Environment.NewLine + Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                .Select(entry => entry.Trim())
                .Where(entry => !string.IsNullOrWhiteSpace(entry))
                .ToList();
            if (entries.Count == 0)
                return string.Empty;

            return string.Join(Environment.NewLine, entries.Skip(Math.Max(0, entries.Count - 12)));
        }

        private static string BuildCharacterModeConversationHistoryForModel()
        {
            var saved = FollowerAIManager.GetSavedConversationHistory(speakerID)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Where(line => line.TrimStart().StartsWith("Character memory:", StringComparison.Ordinal))
                .ToList();
            saved = saved.Skip(Math.Max(0, saved.Count - 80)).ToList();
            return saved.Count == 0 ? string.Empty : string.Join(Environment.NewLine, saved);
        }

        private static void RecordCharacterModeMemory(PendingOpenAiRequest request, string reply)
        {
            if (request == null ||
                request.Context == null ||
                !request.Context.CharacterModeEnabled ||
                string.IsNullOrWhiteSpace(reply))
            {
                return;
            }

            var line = $"Character memory: Lamb said \"{OneLineMemoryText(request.PlayerText)}\"; you replied \"{OneLineMemoryText(reply)}\"";
            if (request.Context.CharacterAwareness?.PersonalTraits == true)
            {
                var traits = BuildPersonalTraitsSnapshotForMemory(request.SpeakerID);
                if (!string.IsNullOrWhiteSpace(traits))
                    line += $"; personal_traits_at_reply=[{traits}]";
            }

            FollowerAIManager.AddConversationLine(request.SpeakerID, line);
        }

        private static string BuildPersonalTraitsSnapshotForMemory(int followerID)
        {
            var fact = FollowerAiFollowerFacts.GetCurrentFollowers().FirstOrDefault(item => item.ID == followerID);
            if (fact?.Traits == null || fact.Traits.Count == 0)
                return "none";

            return string.Join(", ", fact.Traits.Select(trait =>
            {
                var title = string.IsNullOrWhiteSpace(trait?.Title) ? trait?.Name : trait.Title;
                return string.IsNullOrWhiteSpace(title) ? "unknown" : OneLineMemoryText(title);
            }));
        }

        private static string OneLineMemoryText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var text = value.Replace("\r", " ").Replace("\n", " ").Trim();
            while (text.Contains("  "))
                text = text.Replace("  ", " ");
            text = text.Replace("\"", "'");
            return text.Length <= 500 ? text : text.Substring(0, 500).Trim();
        }

        private static void AppendTranscript(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            transcript = string.IsNullOrWhiteSpace(transcript)
                ? message
                : $"{transcript}{Environment.NewLine}{Environment.NewLine}{message}";
            ScrollTranscriptToLatest();
        }

        private static void CopyTranscriptToClipboard()
        {
            if (string.IsNullOrWhiteSpace(transcript))
                return;

            GUIUtility.systemCopyBuffer = BuildShareableTranscript();
            transcriptCopiedUntilRealtime = Time.realtimeSinceStartup + 1.8f;
        }

        private static string BuildShareableTranscript()
        {
            var name = GetSpeakerName();
            if (string.IsNullOrWhiteSpace(name))
                name = "Follower";

            return $"Conversation with {name}{Environment.NewLine}{DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}{Environment.NewLine}{transcript.Trim()}";
        }

        private static string GetSpeakerName()
        {
            return GetSpeakerName(speakerID);
        }

        private static string GetSpeakerName(int followerID)
        {
            try
            {
                var fact = FollowerAiFollowerFacts.GetCurrentFollowers().FirstOrDefault(follower => follower.ID == followerID);
                return string.IsNullOrWhiteSpace(fact?.Name) ? "The follower" : fact.Name;
            }
            catch
            {
                return "The follower";
            }
        }

        private static string BuildNoAiReplyTranscript(string detail = null)
        {
            var normalized = (detail ?? string.Empty).ToLowerInvariant();
            if (normalized.Contains("model_not_found") ||
                normalized.Contains("does not have access to model") ||
                normalized.Contains("no model was configured"))
            {
                return "[AI provider setup issue: the selected model is not available to this provider key/project. Open AI Provider Setup and use Find, Test & Save Setup.]";
            }

            if (normalized.Contains("401") ||
                normalized.Contains("403") ||
                normalized.Contains("unauthorized") ||
                normalized.Contains("forbidden") ||
                normalized.Contains("invalidapikey") ||
                normalized.Contains("invalid api key"))
            {
                return "[AI provider setup issue: the provider rejected the key or model access. Check the provider key, then use Find, Test & Save Setup.]";
            }

            if (normalized.Contains("timeout") ||
                normalized.Contains("did not answer"))
            {
                return "[AI provider timeout: the AI runtime did not answer in time. Try again, or check the provider setup and sidecar diagnostics.]";
            }

            return "[AI route unavailable: no AI reply was generated.]";
        }

        private static bool HasPendingOpenAiRequest()
        {
            lock (pendingLock)
                return pendingOpenAiRequest != null && !pendingOpenAiRequest.Complete;
        }

        private static bool HasAnyPendingOpenAiRequest()
        {
            lock (pendingLock)
                return pendingOpenAiRequest != null;
        }

        private static void HandleKeyboardSendShortcut()
        {
            var ev = Event.current;
            if (ev == null || ev.type != EventType.KeyDown)
                return;

            if (ev.keyCode != KeyCode.Return && ev.keyCode != KeyCode.KeypadEnter)
                return;

            if (!ev.control && !ev.command)
                return;

            if (HasPendingOpenAiRequest() || string.IsNullOrWhiteSpace(playerText))
                return;

            Send();
            ev.Use();
        }

        private static void HandleKeyboardScrollShortcut()
        {
            var ev = Event.current;
            if (ev == null || ev.type != EventType.KeyDown)
                return;

            if (ev.keyCode == KeyCode.UpArrow)
            {
                ScrollTranscript(-GetScrollStep(), true);
                ev.Use();
            }
            else if (ev.keyCode == KeyCode.DownArrow)
            {
                ScrollTranscript(GetScrollStep(), true);
                ev.Use();
            }
            else if (ev.keyCode == KeyCode.PageUp)
            {
                ScrollTranscript(-GetPageScrollStep(), true);
                ev.Use();
            }
            else if (ev.keyCode == KeyCode.PageDown)
            {
                ScrollTranscript(GetPageScrollStep(), true);
                ev.Use();
            }
        }

        private static void HandleKeyboardCloseShortcut()
        {
            var ev = Event.current;
            if (ev == null || ev.type != EventType.KeyDown)
                return;

            if (ev.keyCode != KeyCode.Escape)
                return;

            CloseTextWindowForNativeInteraction();
            ev.Use();
        }

        private static void HandleContinuousScrollInput()
        {
            if (!isOpen)
                return;

            var direction = 0f;
            if (Input.GetKey(KeyCode.UpArrow))
                direction -= 1f;
            if (Input.GetKey(KeyCode.DownArrow))
                direction += 1f;

            direction -= TryGetAxis("Vertical") + TryGetAxis("DPadY") + TryGetAxis("DPadVertical") + TryGetAxis("DPad Y") + TryGetAxis("ControllerVertical");
            if (Mathf.Abs(direction) < 0.25f || Time.realtimeSinceStartup < nextControllerScrollAtRealtime)
                return;

            nextControllerScrollAtRealtime = Time.realtimeSinceStartup + 0.05f;
            ScrollTranscript(Mathf.Sign(direction) * GetScrollStep(), false);
        }

        private static float TryGetAxis(string axisName)
        {
            if (unavailableAxisNames.Contains(axisName))
                return 0f;

            try
            {
                return Input.GetAxisRaw(axisName);
            }
            catch
            {
                unavailableAxisNames.Add(axisName);
                return 0f;
            }
        }

        private static void ScrollTranscript(float delta, bool immediate)
        {
            if (Math.Abs(delta) < 0.01f)
                return;

            transcriptScroll.y = Mathf.Max(0f, transcriptScroll.y + delta);
            if (immediate && Event.current != null)
                GUI.changed = true;
        }

        private static void ScrollTranscriptToLatest()
        {
            transcriptScroll.y = 999999f;
            if (Event.current != null)
                GUI.changed = true;
        }

        private static void EnsureWindowRectInitialized()
        {
            if (windowRectInitialized)
                return;

            var width = AICharacterPlugin.ConversationWindowWidth?.Value ?? 0f;
            var height = AICharacterPlugin.ConversationWindowHeight?.Value ?? 0f;
            if (width <= 0f)
                width = Mathf.Clamp(Screen.width * 0.78f, 760f, Mathf.Max(760f, Screen.width - 80f));
            if (height <= 0f)
                height = Mathf.Clamp(Screen.height * 0.76f, 520f, Mathf.Max(520f, Screen.height - 80f));

            var x = AICharacterPlugin.ConversationWindowX?.Value ?? -1f;
            var y = AICharacterPlugin.ConversationWindowY?.Value ?? -1f;
            if (x < 0f)
                x = (Screen.width - width) * 0.5f;
            if (y < 0f)
                y = (Screen.height - height) * 0.5f;

            windowRect = ClampToScreen(new Rect(x, y, width, height));
            lastSavedWindowRect = windowRect;
            windowRectInitialized = true;
        }

        private static void EnsureStyles()
        {
            var fontSize = GetFontSize();
            var opacity = GetWindowOpacity();
            if (windowStyle != null && styleFontSize == fontSize && Math.Abs(styleWindowOpacity - opacity) < 0.01f)
                return;

            styleFontSize = fontSize;
            styleWindowOpacity = opacity;
            windowBackgroundTexture = FollowerAiOverlayGui.SolidTexture(new Color(0.055f, 0.043f, 0.034f, opacity));

            windowStyle = new GUIStyle(GUI.skin.window)
            {
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(22, 22, 38, 22)
            };
            windowStyle.normal.textColor = new Color(0.95f, 0.89f, 0.74f);
            FollowerAiOverlayGui.ApplyBackground(windowStyle, windowBackgroundTexture);

            transcriptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                wordWrap = true,
                richText = false,
                padding = new RectOffset(8, 8, 6, 6),
                alignment = TextAnchor.UpperLeft
            };
            transcriptStyle.normal.textColor = new Color(0.98f, 0.94f, 0.82f);

            commandLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Math.Max(16, fontSize - 2),
                fontStyle = FontStyle.Bold
            };
            commandLabelStyle.normal.textColor = new Color(0.95f, 0.89f, 0.74f);

            textAreaStyle = new GUIStyle(GUI.skin.textArea)
            {
                fontSize = fontSize,
                wordWrap = true,
                padding = new RectOffset(10, 10, 8, 8)
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Math.Max(24, fontSize - 3),
                fontStyle = FontStyle.Bold
            };

            iconButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Math.Max(26, fontSize - 2),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(4, 4, 2, 2)
            };
            copyIconTexture = CreateCopyIconTexture(64, new Color(0.98f, 0.94f, 0.82f, 1f));

            resizeHandleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Math.Max(24, fontSize - 4),
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            resizeHandleStyle.normal.textColor = new Color(0.95f, 0.89f, 0.74f, 0.9f);

            EnsureCharacterAwarenessStyles(fontSize);
        }

        private static int GetFontSize()
        {
            var fontSize = FollowerAiOverlayGui.GetScaledFontSize(16, 76);
            if (windowRect.width > 0f && windowRect.height > 0f)
            {
                var windowScale = Mathf.Min(windowRect.width / 1180f, windowRect.height / 780f);
                fontSize = Mathf.RoundToInt(fontSize * Mathf.Clamp(windowScale, 0.55f, 1f));
            }

            return Mathf.Clamp(fontSize, 16, 76);
        }

        private static float GetWindowOpacity()
        {
            return Mathf.Clamp(AICharacterPlugin.ConversationWindowOpacity?.Value ?? 0.72f, 0.35f, 0.95f);
        }

        private static float GetScrollStep() => Mathf.Max(36f, GetFontSize() * 1.4f);

        private static float GetPageScrollStep() => Mathf.Max(120f, windowRect.height * 0.55f);

        private static float GetResizeGripSize() => Mathf.Max(40f, GetFontSize() * 0.85f);

        private static Rect ClampToScreen(Rect rect)
        {
            var minWidth = Mathf.Min(Screen.width - 20f, 640f);
            var minHeight = Mathf.Min(Screen.height - 20f, 440f);
            rect.width = Mathf.Clamp(rect.width, minWidth, Mathf.Max(minWidth, Screen.width - 20f));
            rect.height = Mathf.Clamp(rect.height, minHeight, Mathf.Max(minHeight, Screen.height - 20f));
            rect.x = Mathf.Clamp(rect.x, 0f, Mathf.Max(0f, Screen.width - rect.width));
            rect.y = Mathf.Clamp(rect.y, 0f, Mathf.Max(0f, Screen.height - rect.height));
            return rect;
        }

        private static void HandleResizeGrip()
        {
            var size = GetResizeGripSize();
            var gripRect = new Rect(windowRect.width - size, windowRect.height - size, size, size);
            GUI.Label(gripRect, "///", resizeHandleStyle);

            var ev = Event.current;
            if (ev == null)
                return;

            if (ev.type == EventType.MouseDown && ev.button == 0 && gripRect.Contains(ev.mousePosition))
            {
                resizingWindow = true;
                resizeStartMouseScreen = GUIUtility.GUIToScreenPoint(ev.mousePosition);
                resizeStartSize = new Vector2(windowRect.width, windowRect.height);
                ev.Use();
            }
            else if (ev.type == EventType.MouseDrag && resizingWindow)
            {
                var current = GUIUtility.GUIToScreenPoint(ev.mousePosition);
                var delta = current - resizeStartMouseScreen;
                windowRect.width = resizeStartSize.x + delta.x;
                windowRect.height = resizeStartSize.y + delta.y;
                ev.Use();
            }
            else if (ev.rawType == EventType.MouseUp && resizingWindow)
            {
                resizingWindow = false;
                SaveWindowRectNow();
                ev.Use();
            }
        }

        private static void SaveWindowRectIfChanged()
        {
            if (!windowRectInitialized || !WindowRectChanged() || Time.realtimeSinceStartup < nextWindowConfigSaveAtRealtime)
                return;

            SaveWindowRectNow();
        }

        private static bool WindowRectChanged()
        {
            return Math.Abs(windowRect.x - lastSavedWindowRect.x) > 0.5f ||
                   Math.Abs(windowRect.y - lastSavedWindowRect.y) > 0.5f ||
                   Math.Abs(windowRect.width - lastSavedWindowRect.width) > 0.5f ||
                   Math.Abs(windowRect.height - lastSavedWindowRect.height) > 0.5f;
        }

        private static void SaveWindowRectNow()
        {
            if (AICharacterPlugin.ConversationWindowX == null)
                return;

            AICharacterPlugin.ConversationWindowX.Value = windowRect.x;
            AICharacterPlugin.ConversationWindowY.Value = windowRect.y;
            AICharacterPlugin.ConversationWindowWidth.Value = windowRect.width;
            AICharacterPlugin.ConversationWindowHeight.Value = windowRect.height;
            AICharacterPlugin.Instance?.Config.Save();
            lastSavedWindowRect = windowRect;
            nextWindowConfigSaveAtRealtime = Time.realtimeSinceStartup + 0.5f;
        }

        private static void CloseTextWindowForNativeInteraction()
        {
            if (windowRectInitialized && WindowRectChanged())
                SaveWindowRectNow();

            isOpen = false;
            FollowerAiOverlayInputBlocker.Hide("conversation");
            speakerID = HasAnyPendingOpenAiRequest() ? speakerID : -1;
            playerText = string.Empty;
        }

        private static bool ShouldKeepOpenForNativeMenuSettling(string source)
        {
            if (!isOpen)
                return false;

            var elapsed = Time.realtimeSinceStartup - openedAtRealtime;
            if (elapsed >= NativeInteractionCloseGraceSeconds)
                return false;

            AICharacterPlugin.LogInfoVerbose($"AI conversation kept open after early {source}; speaker={speakerID} elapsed={elapsed:0.00}s.");
            return true;
        }

        private static Texture2D CreateCopyIconTexture(int size, Color color)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var clear = new Color(0f, 0f, 0f, 0f);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                texture.SetPixel(x, y, clear);

            DrawRect(texture, 22, 14, 28, 34, color);
            DrawRect(texture, 14, 24, 28, 30, color);
            DrawRect(texture, 26, 18, 20, 4, clear);
            DrawRect(texture, 26, 22, 4, 22, clear);
            DrawRect(texture, 18, 28, 20, 4, clear);
            DrawRect(texture, 18, 32, 4, 18, clear);
            texture.Apply();
            return texture;
        }

        private static void DrawRect(Texture2D texture, int x, int y, int width, int height, Color color)
        {
            for (var yy = y; yy < y + height; yy++)
            for (var xx = x; xx < x + width; xx++)
            {
                if (xx >= 0 && yy >= 0 && xx < texture.width && yy < texture.height)
                    texture.SetPixel(xx, yy, color);
            }
        }
    }
}

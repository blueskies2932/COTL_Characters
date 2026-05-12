using UnityEngine;

namespace COTL_AL_NPCs
{
    internal static class FollowerAiInternetAccessOverlay
    {
        private const int WindowID = 192094;
        private const string InputBlockerOwner = "internet_access";

        private static bool visible;
        private static Rect windowRect;
        private static bool windowRectInitialized;
        private static int lastScreenWidth;
        private static int lastScreenHeight;

        private static int styleFontSize = -1;
        private static GUIStyle windowStyle;
        private static GUIStyle bannerStyle;
        private static GUIStyle titleStyle;
        private static GUIStyle labelStyle;
        private static GUIStyle buttonStyle;
        private static GUIStyle statusStyle;

        private static string status = "Internet access settings ready.";
        private static float statusUntilRealtime;

        internal static bool IsEnabled =>
            AICharacterPlugin.OpenAIInternetAccessEnabled != null &&
            AICharacterPlugin.OpenAIInternetAccessEnabled.Value;

        internal static void Update()
        {
            if (visible && !IsPauseScreenOpen())
            {
                SetVisible(false);
                return;
            }

            if (!Input.GetKeyDown(KeyCode.F9) || !IsPauseScreenOpen())
                return;

            SetVisible(!visible);
            SetStatus(visible ? "Internet access panel opened." : "Internet access panel closed.");
        }

        internal static void OnGUI()
        {
            if (!visible || !IsPauseScreenOpen())
                return;

            EnsureStyles();
            EnsureWindowRect();
            GUI.depth = -979;
            windowRect = GUI.ModalWindow(WindowID, windowRect, DrawWindow, string.Empty, windowStyle);
            FollowerAiOverlayInputBlocker.ConsumeImGuiPointerEvents();
        }

        private static void DrawWindow(int id)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            GUILayout.BeginVertical(bannerStyle, GUILayout.Height(Mathf.Max(92f, GetFontSize() * 2.2f)));
            GUILayout.FlexibleSpace();
            GUILayout.Label("<color=#ffffff>Internet</color> <color=#ffd800>Access</color>", titleStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            GUILayout.Space(18f);

            GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.Label("Direct-speak internet access", labelStyle);
            GUILayout.Space(8f);

            var enabled = IsEnabled;
            var nextLabel = enabled ? "Internet Access: ON" : "Internet Access: OFF";
            if (GUILayout.Button(nextLabel, buttonStyle, GUILayout.Height(Mathf.Max(74f, GetFontSize() * 1.6f)), GUILayout.ExpandWidth(true)))
                SetEnabled(!enabled);

            GUILayout.Space(14f);
            GUILayout.Label("This setting is for direct character replies only.", labelStyle);
            GUILayout.Label("When enabled, direct speech can use web search. Search sources are archived outside the in-game reply.", labelStyle);
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Close", buttonStyle, GUILayout.Height(Mathf.Max(58f, GetFontSize() * 1.25f))))
                SetVisible(false);

            GUILayout.FlexibleSpace();
            var statusText = Time.realtimeSinceStartup < statusUntilRealtime ? status : "F9 toggles this panel while paused.";
            GUILayout.Label(statusText, statusStyle, GUILayout.Width(Mathf.Max(360f, windowRect.width * 0.45f)));
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, windowRect.width, Mathf.Max(96f, GetFontSize() * 2.4f)));
        }

        private static void SetVisible(bool nextVisible)
        {
            visible = nextVisible;
            if (visible)
            {
                CenterWindow();
                FollowerAiOverlayInputBlocker.Show(InputBlockerOwner);
            }
            else
            {
                FollowerAiOverlayInputBlocker.Hide(InputBlockerOwner);
            }
        }

        private static void SetEnabled(bool enabled)
        {
            if (AICharacterPlugin.OpenAIInternetAccessEnabled == null)
                return;

            AICharacterPlugin.OpenAIInternetAccessEnabled.Value = enabled;
            AICharacterPlugin.Instance?.Config.Save();
            SetStatus(enabled ? "Internet access enabled." : "Internet access disabled.");
        }

        private static bool IsPauseScreenOpen()
        {
            return FollowerAiGameState.IsSimulationPaused();
        }

        private static void EnsureStyles()
        {
            var fontSize = GetFontSize();
            if (windowStyle != null && styleFontSize == fontSize)
                return;

            styleFontSize = fontSize;

            windowStyle = new GUIStyle(GUI.skin.window)
            {
                fontSize = 1,
                padding = new RectOffset(18, 18, 18, 18)
            };
            FollowerAiOverlayGui.ApplyBackground(windowStyle, FollowerAiOverlayGui.SolidTexture(new Color(0.015f, 0.015f, 0.012f, 0.97f)));

            bannerStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(28, 28, 8, 8),
                margin = new RectOffset(0, 0, 0, 0)
            };
            FollowerAiOverlayGui.ApplyBackground(bannerStyle, FollowerAiOverlayGui.SolidTexture(new Color(0f, 0f, 0f, 0.98f)));

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(42, fontSize + 12),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = true,
                wordWrap = false,
                normal = { textColor = Color.white }
            };

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = fontSize,
                wordWrap = false
            };

            statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(22, Mathf.RoundToInt(fontSize * 0.72f)),
                alignment = TextAnchor.MiddleRight,
                wordWrap = true,
                normal = { textColor = new Color(0.9f, 0.9f, 0.84f, 1f) }
            };
        }

        private static void EnsureWindowRect()
        {
            if (!windowRectInitialized || lastScreenWidth != Screen.width || lastScreenHeight != Screen.height)
                CenterWindow();

            windowRect = FollowerAiOverlayGui.ClampToScreen(windowRect, 720f, 420f, 12f);
        }

        private static void CenterWindow()
        {
            var targetWidth = Mathf.Clamp(Screen.width * 0.72f, 760f, Screen.width - 80f);
            var targetHeight = Mathf.Clamp(Screen.height * 0.52f, 440f, Screen.height - 80f);
            windowRect = new Rect(
                Mathf.Max(40f, (Screen.width - targetWidth) * 0.5f),
                Mathf.Max(40f, (Screen.height - targetHeight) * 0.5f),
                targetWidth,
                targetHeight);
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            windowRectInitialized = true;
        }

        private static int GetFontSize()
        {
            return Mathf.Clamp(AICharacterPlugin.ConversationFontSize?.Value ?? 52, 28, 72);
        }

        private static void SetStatus(string message)
        {
            status = message ?? string.Empty;
            statusUntilRealtime = Time.realtimeSinceStartup + 3f;
        }
    }
}

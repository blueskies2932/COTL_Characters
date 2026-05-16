using UnityEngine;

namespace COTL_AL_NPCs
{
    internal static class FollowerAiCultAboutOverlay
    {
        private const int WindowID = 192071;
        private const string InputBlockerOwner = "cult_about";

        private static bool visible;
        private static Rect windowRect;
        private static bool windowRectInitialized;
        private static string draftText = string.Empty;
        private static Vector2 scroll;

        private static int styleFontSize = -1;
        private static GUIStyle windowStyle;
        private static GUIStyle textAreaStyle;
        private static GUIStyle labelStyle;
        private static GUIStyle buttonStyle;

        internal static void Show()
        {
            visible = true;
            CenterWindow();
            draftText = FollowerAiCultAbout.Get();
            FollowerAiOverlayInputBlocker.Show(InputBlockerOwner);
            AICharacterPlugin.LogInfoVerbose("AI cult about overlay opened.");
        }

        internal static void Hide()
        {
            visible = false;
            FollowerAiOverlayInputBlocker.Hide(InputBlockerOwner);
            AICharacterPlugin.LogInfoVerbose("AI cult about overlay hidden.");
        }

        internal static void OnGUI()
        {
            if (!visible)
                return;

            HandleKeyboardCloseShortcut();
            EnsureStyles();
            EnsureWindowRect();
            GUI.depth = -990;
            windowRect = GUI.ModalWindow(WindowID, windowRect, DrawWindow, "AI Cult About", windowStyle);
            FollowerAiOverlayInputBlocker.ConsumeImGuiPointerEvents();
        }

        private static void DrawWindow(int windowID)
        {
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
            var textAreaHeight = GetAboutTextAreaHeight();
            draftText = GUILayout.TextArea(
                draftText ?? string.Empty,
                textAreaStyle,
                GUILayout.MinHeight(textAreaHeight),
                GUILayout.ExpandWidth(true));
            GUILayout.EndScrollView();

            GUILayout.Space(12f);
            GUILayout.BeginHorizontal();
            var buttonHeight = Mathf.Max(58f, GetFontSize() * 1.25f);
            if (GUILayout.Button("Save About", buttonStyle, GUILayout.Height(buttonHeight)))
                FollowerAiCultAbout.Save(draftText);
            if (GUILayout.Button("Reload", buttonStyle, GUILayout.Height(buttonHeight)))
                draftText = FollowerAiCultAbout.Get();
            if (GUILayout.Button("Close", buttonStyle, GUILayout.Height(buttonHeight)))
                Hide();
            GUILayout.EndHorizontal();

            GUI.DragWindow(new Rect(0, 0, 10000, Mathf.Max(42f, GetFontSize() * 1.2f)));
        }

        private static void EnsureStyles()
        {
            var fontSize = GetFontSize();
            if (windowStyle != null && styleFontSize == fontSize)
                return;

            styleFontSize = fontSize;

            windowStyle = new GUIStyle(GUI.skin.window)
            {
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(22, 22, 38, 22)
            };
            FollowerAiOverlayGui.ApplyBackground(windowStyle, FollowerAiOverlayGui.SolidTexture(new Color(0.015f, 0.015f, 0.012f, 0.97f)));

            textAreaStyle = new GUIStyle(GUI.skin.textArea)
            {
                fontSize = fontSize,
                wordWrap = true,
                padding = new RectOffset(12, 12, 10, 10),
                normal = { textColor = Color.white },
                focused = { textColor = Color.white },
                hover = { textColor = Color.white },
                active = { textColor = Color.white }
            };
            FollowerAiOverlayGui.ApplyBackground(textAreaStyle, FollowerAiOverlayGui.SolidTexture(new Color(0.02f, 0.02f, 0.018f, 0.94f)));

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(24, Mathf.RoundToInt(fontSize * 0.72f)),
                wordWrap = true,
                normal = { textColor = new Color(0.96f, 0.93f, 0.84f, 1f) }
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.Max(24, Mathf.RoundToInt(fontSize * 0.82f)),
                fontStyle = FontStyle.Bold
            };
        }

        private static void EnsureWindowRect()
        {
            if (!windowRectInitialized)
                CenterWindow();

            windowRect = FollowerAiOverlayGui.ClampToScreen(windowRect, 820f, 620f, 20f);
        }

        private static void CenterWindow()
        {
            var targetWidth = Mathf.Clamp(Screen.width * 0.74f, 860f, Screen.width - 80f);
            var targetHeight = Mathf.Clamp(Screen.height * 0.72f, 640f, Screen.height - 80f);
            windowRect = new Rect(
                Mathf.Max(40f, (Screen.width - targetWidth) * 0.5f),
                Mathf.Max(40f, (Screen.height - targetHeight) * 0.5f),
                targetWidth,
                targetHeight);
            windowRectInitialized = true;
        }

        private static void HandleKeyboardCloseShortcut()
        {
            var ev = Event.current;
            if (ev == null || ev.type != EventType.KeyDown || ev.keyCode != KeyCode.Escape)
                return;

            Hide();
            ev.Use();
        }

        private static int GetFontSize()
        {
            return FollowerAiOverlayGui.GetScaledFontSize(22, 72);
        }

        private static float GetAboutTextAreaHeight()
        {
            var fontSize = GetFontSize();
            var lineCount = 1;
            if (!string.IsNullOrEmpty(draftText))
                lineCount = Mathf.Max(1, draftText.Split('\n').Length);

            return Mathf.Max(320f, windowRect.height * 0.72f, (lineCount + 4) * fontSize * 1.45f);
        }
    }
}

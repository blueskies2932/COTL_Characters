using UnityEngine;

namespace COTL_AL_NPCs
{
    internal static partial class FollowerAiInvocations
    {
        private static bool visible;
        private static Rect windowRect;
        private static bool windowRectInitialized;
        private static int lastScreenWidth;
        private static int lastScreenHeight;
        private static Vector2 scroll;
        private static int styleFontSize = -1;
        private static GUIStyle windowStyle;
        private static GUIStyle bannerStyle;
        private static GUIStyle headerStyle;
        private static GUIStyle subHeaderStyle;
        private static GUIStyle labelStyle;
        private static GUIStyle rowStyle;
        private static GUIStyle textFieldStyle;
        private static GUIStyle buttonStyle;
        private static GUIStyle statusStyle;
        private static Texture2D windowBackground;
        private static Texture2D bannerBackground;
        private static Texture2D rowBackground;
        private static Texture2D fieldBackground;
        private static string status = "Invocations ready.";
        private static float statusUntilRealtime;

        internal static void Update()
        {
            if (visible && !IsPauseScreenOpen())
            {
                Save();
                visible = false;
                FollowerAiOverlayInputBlocker.Hide(InputBlockerOwner);
                return;
            }

            if (Input.GetKeyDown(KeyCode.F7))
            {
                if (!IsPauseScreenOpen())
                    return;

                EnsureLoaded();
                visible = !visible;
                if (visible)
                {
                    CenterWindow();
                    FollowerAiOverlayInputBlocker.Show(InputBlockerOwner);
                }
                else
                {
                    Save();
                    FollowerAiOverlayInputBlocker.Hide(InputBlockerOwner);
                }
                SetStatus(visible ? "Invocations menu opened." : "Invocations menu closed.");
            }
        }

        internal static void OnGUI()
        {
            if (!visible || !IsPauseScreenOpen())
                return;

            EnsureLoaded();
            EnsureStyles();
            EnsureWindowRect();
            GUI.depth = -980;
            windowRect = GUI.Window(WindowID, windowRect, DrawWindow, string.Empty, windowStyle);
            FollowerAiOverlayInputBlocker.ConsumeImGuiPointerEvents();
        }

        private static void DrawWindow(int windowID)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.BeginVertical(bannerStyle, GUILayout.Height(Mathf.Max(94f, GetFontSize() * 2.35f)));
            GUILayout.FlexibleSpace();
            GUILayout.Label("<color=#ffffff>Invocations</color> <color=#ffd800>Menu</color>", headerStyle);
            GUILayout.Label("Set exact codes for mod-side invocations.", subHeaderStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            GUILayout.Space(14f);

            scroll = GUILayout.BeginScrollView(scroll, false, true, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            lock (Sync)
            {
                foreach (var entry in state.Invocations)
                {
                    GUILayout.BeginVertical(rowStyle, GUILayout.ExpandWidth(true));
                    GUILayout.Label(entry.Name, labelStyle);
                    GUI.SetNextControlName($"InvocationCode_{entry.Id}");
                    entry.Code = GUILayout.TextField(entry.Code ?? string.Empty, textFieldStyle, GUILayout.Height(Mathf.Max(64f, GetFontSize() * 1.65f)), GUILayout.ExpandWidth(true));
                    GUILayout.EndVertical();
                    GUILayout.Space(12f);
                }
            }
            GUILayout.EndScrollView();
            GUILayout.Space(12f);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save", buttonStyle, GUILayout.Height(Mathf.Max(58f, GetFontSize() * 1.35f))))
            {
                Save();
                SetStatus("Invocations saved.");
            }

            if (GUILayout.Button("Reload", buttonStyle, GUILayout.Height(Mathf.Max(58f, GetFontSize() * 1.35f))))
            {
                lock (Sync)
                    state = null;
                EnsureLoaded();
                SetStatus("Invocations reloaded.");
            }

            if (GUILayout.Button("Close", buttonStyle, GUILayout.Height(Mathf.Max(58f, GetFontSize() * 1.35f))))
            {
                Save();
                visible = false;
                FollowerAiOverlayInputBlocker.Hide(InputBlockerOwner);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);

            var statusText = Time.realtimeSinceStartup < statusUntilRealtime ? status : "F7 toggles this menu while paused.";
            GUILayout.Label(statusText, statusStyle);
            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0f, 0f, windowRect.width, Mathf.Max(96f, GetFontSize() * 2.4f)));
        }

        private static void EnsureStyles()
        {
            var fontSize = GetFontSize();
            if (windowStyle != null && labelStyle != null && styleFontSize == fontSize)
                return;

            styleFontSize = fontSize;
            windowBackground = FollowerAiOverlayGui.SolidTexture(new Color(0.015f, 0.015f, 0.012f, 0.97f));
            bannerBackground = FollowerAiOverlayGui.SolidTexture(new Color(0f, 0f, 0f, 0.98f));
            rowBackground = FollowerAiOverlayGui.SolidTexture(new Color(0.025f, 0.025f, 0.022f, 0.94f));
            fieldBackground = FollowerAiOverlayGui.SolidTexture(new Color(0.11f, 0.12f, 0.115f, 0.98f));
            windowStyle = new GUIStyle(GUI.skin.window)
            {
                fontSize = 1,
                padding = new RectOffset(18, 18, 18, 18)
            };
            FollowerAiOverlayGui.ApplyBackground(windowStyle, windowBackground);
            bannerStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(28, 28, 8, 8),
                margin = new RectOffset(0, 0, 0, 0)
            };
            FollowerAiOverlayGui.ApplyBackground(bannerStyle, bannerBackground);
            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(42, fontSize + 12),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = true,
                normal = { textColor = Color.white },
                wordWrap = false
            };
            subHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(22, Mathf.RoundToInt(fontSize * 0.7f)),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.9f, 0.9f, 0.84f, 1f) },
                wordWrap = false
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                normal = { textColor = Color.white },
                wordWrap = true
            };
            rowStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(18, 18, 16, 18),
                margin = new RectOffset(0, 8, 0, 0)
            };
            FollowerAiOverlayGui.ApplyBackground(rowStyle, rowBackground);
            textFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = fontSize,
                padding = new RectOffset(14, 14, 10, 10),
                normal = { textColor = Color.white },
                focused = { textColor = Color.white },
                hover = { textColor = Color.white },
                active = { textColor = Color.white }
            };
            FollowerAiOverlayGui.ApplyBackground(textFieldStyle, fieldBackground);
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = fontSize,
                wordWrap = false
            };
            statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(22, Mathf.RoundToInt(fontSize * 0.72f)),
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.9f, 0.9f, 0.84f, 1f) },
                wordWrap = false
            };
        }

        private static int GetFontSize()
        {
            return Mathf.Clamp(AICharacterPlugin.ConversationFontSize?.Value ?? 52, 28, 72);
        }

        private static void EnsureWindowRect()
        {
            if (!windowRectInitialized || lastScreenWidth != Screen.width || lastScreenHeight != Screen.height)
                CenterWindow();

            windowRect = FollowerAiOverlayGui.ClampToScreen(windowRect, 760f, 520f, 12f);
        }

        private static void CenterWindow()
        {
            var targetWidth = Mathf.Clamp(Screen.width * 0.88f, 860f, Screen.width - 48f);
            var targetHeight = Mathf.Clamp(Screen.height * 0.82f, 560f, Screen.height - 48f);
            windowRect = FollowerAiOverlayGui.ClampToScreen(new Rect(
                    Mathf.Max(24f, (Screen.width - targetWidth) * 0.5f),
                    Mathf.Max(24f, (Screen.height - targetHeight) * 0.5f),
                    targetWidth,
                    targetHeight),
                760f,
                520f,
                12f);
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            windowRectInitialized = true;
        }

        private static void SetStatus(string message)
        {
            status = message ?? string.Empty;
            statusUntilRealtime = Time.realtimeSinceStartup + 4f;
        }
    }
}

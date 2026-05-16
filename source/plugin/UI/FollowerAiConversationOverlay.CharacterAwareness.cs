using System;
using UnityEngine;

namespace COTL_AL_NPCs
{
    internal static partial class FollowerAiConversationOverlay
    {
        private static bool characterLoreExpanded;
        private static string characterLoreDraft = string.Empty;
        private static bool characterLoreDirty;
        private static float characterLoreSavedMessageUntilRealtime;
        private static Vector2 characterLoreScroll;
        private static GUIStyle awarenessBoxStyle;
        private static GUIStyle awarenessToggleOnStyle;
        private static GUIStyle awarenessToggleOffStyle;
        private static GUIStyle awarenessToggleLabelStyle;
        private static GUIStyle awarenessLengthOptionStyle;
        private static GUIStyle awarenessLengthSelectedStyle;
        private static Texture2D awarenessToggleOnTexture;
        private static Texture2D awarenessToggleOffTexture;
        private static Texture2D awarenessLengthSelectedTexture;

        private static void DrawCharacterAwarenessSettings()
        {
            if (FollowerAIManager.GetMode(speakerID) != FollowerAiMode.Character)
                return;

            var settings = FollowerAiCharacterModeSettings.Get(speakerID);
            GUILayout.BeginVertical(awarenessBoxStyle, GUILayout.ExpandWidth(true));
            GUILayout.Label("Character Awareness", commandLabelStyle);
            var changed = false;

            var row = BeginAwarenessWrapRow();
            changed |= DrawWrappedAwarenessToggle("Traits", ref settings.PersonalTraits, ref row);
            changed |= DrawWrappedAwarenessToggle("Cult About", ref settings.CultAbout, ref row);
            changed |= DrawWrappedAwarenessToggle("Events", ref settings.CurrentEvents, ref row);
            changed |= DrawWrappedAwarenessToggle("Tournament", ref settings.TournamentDetails, ref row);
            changed |= DrawWrappedAwarenessToggle("World State", ref settings.WorldState, ref row);
            changed |= DrawWrappedAwarenessToggle("Lore", ref settings.Lore, ref row);
            changed |= DrawWrappedAwarenessToggle("Long-Term", ref settings.LongTermConversationHistory, ref row);
            changed |= DrawWrappedReplyLengthMenu(settings, ref row);
            EndAwarenessWrapRow();
            GUILayout.EndVertical();

            if (changed)
                FollowerAiCharacterModeSettings.Save(speakerID, settings);
        }

        private struct AwarenessWrapRow
        {
            public float UsedWidth;
            public float AvailableWidth;
        }

        private static AwarenessWrapRow BeginAwarenessWrapRow()
        {
            GUILayout.BeginHorizontal();
            return new AwarenessWrapRow
            {
                UsedWidth = 0f,
                AvailableWidth = GetAwarenessWrapAvailableWidth()
            };
        }

        private static void EndAwarenessWrapRow()
        {
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private static void WrapAwarenessRowIfNeeded(float nextWidth, ref AwarenessWrapRow row)
        {
            if (row.UsedWidth <= 0f || row.UsedWidth + nextWidth <= row.AvailableWidth)
                return;

            EndAwarenessWrapRow();
            row = BeginAwarenessWrapRow();
        }

        private static bool DrawWrappedAwarenessToggle(string label, ref bool value, ref AwarenessWrapRow row)
        {
            var width = GetAwarenessToggleWidth(label);
            WrapAwarenessRowIfNeeded(width, ref row);
            row.UsedWidth += width;
            return DrawAwarenessToggle(label, ref value);
        }

        private static bool DrawWrappedReplyLengthMenu(FollowerAiCharacterAwarenessSettings settings, ref AwarenessWrapRow row)
        {
            var width = GetReplyLengthMenuWidth();
            WrapAwarenessRowIfNeeded(width, ref row);
            row.UsedWidth += width;
            return DrawReplyLengthMenu(settings);
        }

        private static float GetCharacterAwarenessHeight()
        {
            if (FollowerAIManager.GetMode(speakerID) != FollowerAiMode.Character)
                return 0f;

            var fontSize = GetFontSize();
            var titleHeight = Math.Max(28f, fontSize * 0.95f);
            var rowHeight = Math.Max(28f, fontSize * 0.86f);
            var padding = 24f;
            return titleHeight + GetCharacterAwarenessRowCount() * rowHeight + padding;
        }

        private static int GetCharacterAwarenessRowCount()
        {
            var availableWidth = GetAwarenessWrapAvailableWidth();
            var rows = 1;
            var usedWidth = 0f;
            var widths = new[]
            {
                GetAwarenessToggleWidth("Traits"),
                GetAwarenessToggleWidth("Cult About"),
                GetAwarenessToggleWidth("Events"),
                GetAwarenessToggleWidth("Tournament"),
                GetAwarenessToggleWidth("World State"),
                GetAwarenessToggleWidth("Lore"),
                GetAwarenessToggleWidth("Long-Term"),
                GetReplyLengthMenuWidth()
            };

            foreach (var width in widths)
            {
                if (usedWidth > 0f && usedWidth + width > availableWidth)
                {
                    rows++;
                    usedWidth = 0f;
                }

                usedWidth += width;
            }

            return rows;
        }

        private static float GetAwarenessWrapAvailableWidth()
        {
            return Math.Max(220f, windowRect.width - 76f);
        }

        private static float GetAwarenessToggleWidth(string label)
        {
            var fontSize = GetFontSize();
            var boxSize = Math.Max(18f, fontSize * 0.66f);
            var labelWidth = awarenessToggleLabelStyle?.CalcSize(new GUIContent(label)).x ?? label.Length * fontSize * 0.55f;
            return boxSize + labelWidth + Math.Max(18f, fontSize * 0.44f);
        }

        private static float GetReplyLengthMenuWidth()
        {
            var fontSize = GetFontSize();
            var width = awarenessToggleLabelStyle?.CalcSize(new GUIContent("Reply Length")).x ?? fontSize * 5f;
            width += GetReplyLengthOptionWidth("Short");
            width += GetReplyLengthOptionWidth("Medium");
            width += GetReplyLengthOptionWidth("Long");
            return width + Math.Max(28f, fontSize * 0.72f);
        }

        private static float GetReplyLengthOptionWidth(string label)
        {
            var fontSize = GetFontSize();
            return Math.Max(fontSize * 2.45f, label.Length * fontSize * 0.78f);
        }

        private static void DrawCharacterLoreLeafTab()
        {
            var tabRect = GetCharacterLoreLeafTabRect();
            var label = characterLoreExpanded ? "Lore <" : "Lore >";
            if (GUI.Button(tabRect, label, awarenessToggleLabelStyle))
            {
                if (!characterLoreExpanded)
                    characterLoreDraft = FollowerAiCharacterModeSettings.Get(speakerID).LoreText ?? string.Empty;
                characterLoreExpanded = !characterLoreExpanded;
            }
        }

        private static Rect GetCharacterLoreLeafTabRect()
        {
            var gap = Math.Max(8f, GetFontSize() * 0.18f);
            var width = Math.Max(104f, GetFontSize() * 2.6f);
            var height = Math.Max(58f, GetFontSize() * 1.2f);
            var x = windowRect.xMax + gap;
            var y = windowRect.y + Math.Max(76f, GetFontSize() * 1.7f);
            if (x + width > Screen.width - 10f)
                x = Math.Max(10f, windowRect.xMax - width - gap);
            return new Rect(x, y, width, height);
        }

        private static Rect GetCharacterLoreLeafRect()
        {
            var gap = Math.Max(8f, GetFontSize() * 0.18f);
            var tabRect = GetCharacterLoreLeafTabRect();
            var width = Mathf.Clamp(windowRect.width * 0.38f, 420f, 620f);
            var height = windowRect.height;
            var x = tabRect.xMax + gap;
            if (x + width > Screen.width - 10f)
                x = Math.Max(10f, Screen.width - width - 10f);
            return new Rect(x, windowRect.y, width, height);
        }

        private static void DrawCharacterLoreLeaf(int windowID)
        {
            var settings = FollowerAiCharacterModeSettings.Get(speakerID);
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            characterLoreScroll = GUILayout.BeginScrollView(characterLoreScroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUI.SetNextControlName("CharacterLoreText");
            var updatedLore = GUILayout.TextArea(characterLoreDraft ?? string.Empty, textAreaStyle, GUILayout.ExpandWidth(true), GUILayout.MinHeight(Mathf.Max(360f, windowRect.height - GetFontSize() * 4.4f)));
            if (!string.Equals(updatedLore, characterLoreDraft ?? string.Empty, StringComparison.Ordinal))
            {
                characterLoreDraft = updatedLore;
                characterLoreDirty = true;
            }
            GUILayout.EndScrollView();

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            GUI.enabled = characterLoreDirty || !string.Equals(characterLoreDraft ?? string.Empty, settings.LoreText ?? string.Empty, StringComparison.Ordinal);
            if (GUILayout.Button("Save Lore", buttonStyle, GUILayout.Height(Math.Max(56f, GetFontSize() * 1.15f))))
            {
                settings.LoreText = characterLoreDraft ?? string.Empty;
                FollowerAiCharacterModeSettings.Save(speakerID, settings);
                characterLoreDirty = false;
                characterLoreSavedMessageUntilRealtime = Time.realtimeSinceStartup + 1.8f;
                GUI.FocusControl("CharacterLoreText");
            }
            GUI.enabled = true;
            if (GUILayout.Button("Close", buttonStyle, GUILayout.Width(Math.Max(120f, GetFontSize() * 2.8f)), GUILayout.Height(Math.Max(56f, GetFontSize() * 1.15f))))
                characterLoreExpanded = false;
            GUILayout.EndHorizontal();

            if (Time.realtimeSinceStartup < characterLoreSavedMessageUntilRealtime)
                GUILayout.Label("Saved.", commandLabelStyle);

            GUILayout.EndVertical();
        }

        private static bool DrawAwarenessToggle(string label, ref bool value)
        {
            var before = value;
            var boxSize = Math.Max(18f, GetFontSize() * 0.66f);
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
            if (GUILayout.Button(string.Empty, value ? awarenessToggleOnStyle : awarenessToggleOffStyle, GUILayout.Width(boxSize), GUILayout.Height(boxSize)))
                value = !value;
            if (GUILayout.Button(label, awarenessToggleLabelStyle, GUILayout.Height(boxSize), GUILayout.ExpandWidth(false)))
                value = !value;
            GUILayout.EndHorizontal();
            return before != value;
        }

        private static bool DrawReplyLengthMenu(FollowerAiCharacterAwarenessSettings settings)
        {
            if (settings == null)
                return false;

            var before = settings.ReplyLength;
            var height = Math.Max(22f, GetFontSize() * 0.72f);
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
            GUILayout.Label("Reply Length", awarenessToggleLabelStyle, GUILayout.Height(height), GUILayout.ExpandWidth(false));
            DrawReplyLengthOption(settings, FollowerAiReplyLength.Short, height);
            DrawReplyLengthOption(settings, FollowerAiReplyLength.Medium, height);
            DrawReplyLengthOption(settings, FollowerAiReplyLength.Long, height);
            GUILayout.EndHorizontal();
            return before != settings.ReplyLength;
        }

        private static void DrawReplyLengthOption(FollowerAiCharacterAwarenessSettings settings, FollowerAiReplyLength length, float height)
        {
            var selected = settings.ReplyLength == length;
            var label = length.ToString();
            var width = GetReplyLengthOptionWidth(label);
            if (GUILayout.Button(label, selected ? awarenessLengthSelectedStyle : awarenessLengthOptionStyle, GUILayout.Width(width), GUILayout.Height(height)))
                settings.ReplyLength = length;
        }

        private static void EnsureCharacterAwarenessStyles(int fontSize)
        {
            awarenessBoxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 8, 8)
            };

            awarenessToggleOnTexture = FollowerAiOverlayGui.SolidTexture(new Color(0.0f, 0.92f, 0.2f, 1f));
            awarenessToggleOffTexture = FollowerAiOverlayGui.SolidTexture(new Color(0.16f, 0.16f, 0.16f, 0.95f));
            awarenessLengthSelectedTexture = FollowerAiOverlayGui.SolidTexture(new Color(0.54f, 0.42f, 0.18f, 0.98f));
            awarenessToggleOnStyle = new GUIStyle(GUI.skin.button)
            {
                margin = new RectOffset(8, 6, 4, 4),
                padding = new RectOffset(0, 0, 0, 0)
            };
            awarenessToggleOnStyle.normal.background = awarenessToggleOnTexture;
            awarenessToggleOnStyle.hover.background = awarenessToggleOnTexture;
            awarenessToggleOnStyle.active.background = awarenessToggleOnTexture;

            awarenessToggleOffStyle = new GUIStyle(awarenessToggleOnStyle);
            awarenessToggleOffStyle.normal.background = awarenessToggleOffTexture;
            awarenessToggleOffStyle.hover.background = awarenessToggleOffTexture;
            awarenessToggleOffStyle.active.background = awarenessToggleOffTexture;

            awarenessToggleLabelStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Math.Max(14, fontSize - 7),
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(4, 8, 0, 0),
                margin = new RectOffset(0, 10, 3, 3)
            };
            awarenessToggleLabelStyle.normal.textColor = new Color(0.98f, 0.94f, 0.82f);
            awarenessToggleLabelStyle.hover.textColor = new Color(1f, 1f, 0.88f);

            awarenessLengthOptionStyle = new GUIStyle(awarenessToggleLabelStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                margin = new RectOffset(0, 5, 3, 3),
                padding = new RectOffset(6, 6, 0, 0)
            };

            awarenessLengthSelectedStyle = new GUIStyle(awarenessLengthOptionStyle);
            awarenessLengthSelectedStyle.normal.background = awarenessLengthSelectedTexture;
            awarenessLengthSelectedStyle.hover.background = awarenessLengthSelectedTexture;
            awarenessLengthSelectedStyle.active.background = awarenessLengthSelectedTexture;
        }
    }
}

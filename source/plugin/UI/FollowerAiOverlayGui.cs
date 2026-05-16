using UnityEngine;

namespace COTL_AL_NPCs
{
    internal static class FollowerAiOverlayGui
    {
        internal static Texture2D SolidTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        internal static void ApplyBackground(GUIStyle style, Texture2D texture)
        {
            if (style == null || texture == null)
                return;

            style.normal.background = texture;
            style.onNormal.background = texture;
            style.hover.background = texture;
            style.onHover.background = texture;
            style.active.background = texture;
            style.onActive.background = texture;
            style.focused.background = texture;
            style.onFocused.background = texture;
        }

        internal static Rect ClampToScreen(
            Rect rect,
            float minimumWidth,
            float minimumHeight,
            float margin = 10f)
        {
            var maxWidth = Mathf.Max(minimumWidth, Screen.width - margin * 2f);
            var maxHeight = Mathf.Max(minimumHeight, Screen.height - margin * 2f);
            rect.width = Mathf.Clamp(rect.width, minimumWidth, maxWidth);
            rect.height = Mathf.Clamp(rect.height, minimumHeight, maxHeight);
            rect.x = Mathf.Clamp(rect.x, 0f, Mathf.Max(0f, Screen.width - rect.width));
            rect.y = Mathf.Clamp(rect.y, 0f, Mathf.Max(0f, Screen.height - rect.height));
            return rect;
        }

        internal static int GetScaledFontSize(int minimum, int maximum)
        {
            var configured = Mathf.Clamp(AICharacterPlugin.ConversationFontSize?.Value ?? 52, 18, 96);
            var scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            scale = Mathf.Clamp(scale, 0.58f, 1f);
            return Mathf.Clamp(Mathf.RoundToInt(configured * scale), minimum, maximum);
        }
    }
}

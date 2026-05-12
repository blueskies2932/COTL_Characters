using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace COTL_AL_NPCs
{
    internal static class FollowerAiOverlayInputBlocker
    {
        private const string Name = "COTL_AL_NPCs_InputBlocker";
        private static readonly HashSet<string> Owners = new HashSet<string>();
        private static GameObject blocker;

        internal static void Show(string owner)
        {
            if (string.IsNullOrWhiteSpace(owner))
                return;

            Owners.Add(owner);
            EnsureBlocker();
        }

        internal static void Hide(string owner)
        {
            if (!string.IsNullOrWhiteSpace(owner))
                Owners.Remove(owner);

            if (Owners.Count == 0)
                DestroyBlocker();
        }

        internal static void HideAll()
        {
            Owners.Clear();
            DestroyBlocker();
        }

        internal static void ConsumeImGuiPointerEvents()
        {
            var ev = Event.current;
            if (ev == null)
                return;

            if (ev.type == EventType.MouseDown ||
                ev.type == EventType.MouseUp ||
                ev.type == EventType.MouseDrag ||
                ev.type == EventType.ScrollWheel)
            {
                ev.Use();
            }
        }

        private static void EnsureBlocker()
        {
            if (blocker != null)
                return;

            blocker = new GameObject(Name, typeof(Canvas), typeof(GraphicRaycaster));
            Object.DontDestroyOnLoad(blocker);

            var canvas = blocker.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            var shield = new GameObject("ClickShield", typeof(RectTransform), typeof(Image));
            shield.transform.SetParent(blocker.transform, false);

            var rect = shield.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = shield.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;
        }

        private static void DestroyBlocker()
        {
            if (blocker == null)
                return;

            Object.Destroy(blocker);
            blocker = null;
        }
    }
}

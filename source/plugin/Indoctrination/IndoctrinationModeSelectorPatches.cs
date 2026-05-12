using HarmonyLib;
using Lamb.UI;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace COTL_AL_NPCs
{
    [HarmonyPatch(typeof(UIFollowerIndoctrinationMenuController), "Show")]
    public static class IndoctrinationMenuPatch
    {
        static void Postfix(UIFollowerIndoctrinationMenuController __instance)
        {
            if (__instance == null || __instance.transform == null)
                return;

            if (__instance.transform.Find("NPCModeSelectorHolder") != null)
                return;

            AICharacterPlugin.NextFollowerIsNPC = false;
            AICharacterPlugin.NextFollowerMode = FollowerAiMode.Vanilla;
            AICharacterPlugin.Log.LogInfo("Indoctrination menu opened - adding NPC mode selector.");

            try
            {
                var toggleHolder = new GameObject("NPCModeSelectorHolder", typeof(RectTransform));
                toggleHolder.transform.SetParent(__instance.transform, false);

                var holderRect = toggleHolder.GetComponent<RectTransform>();
                holderRect.anchorMin = new Vector2(0.5f, 1f);
                holderRect.anchorMax = new Vector2(0.5f, 1f);
                holderRect.pivot = new Vector2(0.5f, 1f);
                holderRect.anchoredPosition = new Vector2(0f, -40f);
                holderRect.sizeDelta = new Vector2(300f, 34f);

                CreateModeToggle(toggleHolder.transform, "Vanilla", FollowerAiMode.Vanilla, true, true, 0f);
                CreateModeToggle(toggleHolder.transform, "Character", FollowerAiMode.Character, false, true, 150f);
            }
            catch (Exception ex)
            {
                AICharacterPlugin.Log.LogError($"Failed to add NPC mode selector: {ex}");
            }
        }

        private static void CreateModeToggle(Transform parent, string label, FollowerAiMode mode, bool isOn, bool selectable, float x)
        {
            var toggleObject = new GameObject($"{mode}Toggle", typeof(RectTransform));
            toggleObject.transform.SetParent(parent, false);
            var toggleRect = toggleObject.GetComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(0f, 0f);
            toggleRect.anchorMax = new Vector2(0f, 1f);
            toggleRect.anchoredPosition = new Vector2(x, 0f);
            toggleRect.sizeDelta = new Vector2(132f, 0f);

            var toggle = toggleObject.AddComponent<Toggle>();
            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(toggleObject.transform, false);
            var backgroundImage = background.GetComponent<Image>();
            backgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            var bgRect = background.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0f);
            bgRect.anchorMax = new Vector2(0f, 1f);
            bgRect.sizeDelta = new Vector2(24f, 0f);
            bgRect.anchoredPosition = new Vector2(10f, 0f);

            var checkmark = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            checkmark.transform.SetParent(background.transform, false);
            var checkmarkImage = checkmark.GetComponent<Image>();
            checkmarkImage.color = new Color(0f, 1f, 0.2f, 1f);

            var checkRect = checkmark.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.2f, 0.2f);
            checkRect.anchorMax = new Vector2(0.8f, 0.8f);
            checkRect.offsetMin = Vector2.zero;
            checkRect.offsetMax = Vector2.zero;

            toggle.targetGraphic = backgroundImage;
            toggle.graphic = checkmarkImage;
            toggle.isOn = isOn;
            toggle.interactable = selectable;
            toggle.onValueChanged.AddListener(value =>
            {
                if (!value || !selectable)
                    return;

                foreach (var other in parent.GetComponentsInChildren<Toggle>(true))
                {
                    if (other != toggle)
                        other.isOn = false;
                }

                AICharacterPlugin.SetNextFollowerMode(mode);
            });

            var labelObject = new GameObject("ToggleLabel", typeof(RectTransform));
            labelObject.transform.SetParent(toggleObject.transform, false);
            var labelText = labelObject.AddComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            labelText.text = label;
            labelText.fontSize = 14;
            labelText.color = selectable ? Color.white : new Color(0.65f, 0.65f, 0.65f, 1f);
            labelText.alignment = TextAnchor.MiddleLeft;

            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.anchoredPosition = new Vector2(42f, 0f);
            labelRect.sizeDelta = new Vector2(-42f, 0f);
        }
    }

    [HarmonyPatch(typeof(UIFollowerIndoctrinationMenuController), "OnAcceptButtonSelected")]
    public static class IndoctrinationAcceptPatch
    {
        static void Postfix(UIFollowerIndoctrinationMenuController __instance)
        {
            AICharacterPlugin.Log.LogInfo("UIFollowerIndoctrinationMenuController.OnAcceptButtonSelected called.");
            AICharacterPlugin.TryApplyNPCToIndoctrinationTarget(__instance, "OnAcceptButtonSelected");
        }
    }

    [HarmonyPatch(typeof(FollowerRecruit), "DoRecruit")]
    public static class FollowerRecruitPatch
    {
        static void Postfix(FollowerRecruit __instance)
        {
            AICharacterPlugin.TryApplyNPCToRecruit(__instance, "DoRecruit");
        }
    }

    [HarmonyPatch(typeof(FollowerRecruit), "CompleteCallBack")]
    public static class FollowerRecruitCompleteCallbackPatch
    {
        static void Postfix(FollowerRecruit __instance)
        {
            AICharacterPlugin.TryApplyNPCToRecruit(__instance, "CompleteCallBack");
        }
    }
}

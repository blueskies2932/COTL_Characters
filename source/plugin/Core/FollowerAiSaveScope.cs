using BepInEx;
using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace COTL_AL_NPCs
{
    internal static class FollowerAiSaveScope
    {
        private const string UnsavedScopeKey = "no-active-save";
        private static string observedScopeKey = string.Empty;

        internal static string RootDirectory => Path.Combine(Paths.ConfigPath, "COTL_AL_NPCs");

        internal static string CurrentSaveKey
        {
            get
            {
                var uniqueID = GetSaveUniqueID();
                var slot = GetSaveSlot();
                if (!string.IsNullOrWhiteSpace(uniqueID))
                    return SanitizeScopeKey(slot >= 0 ? $"slot-{slot}-{uniqueID}" : uniqueID);

                if (slot >= 0)
                    return SanitizeScopeKey($"slot-{slot}");

                return UnsavedScopeKey;
            }
        }

        internal static bool HasStableSaveIdentity => !string.IsNullOrWhiteSpace(GetSaveUniqueID());

        internal static string CurrentScopedDirectory => Path.Combine(RootDirectory, "Saves", CurrentSaveKey);

        internal static string CurrentDisplayName
        {
            get
            {
                var uniqueID = GetSaveUniqueID();
                var slot = GetSaveSlot();
                if (!string.IsNullOrWhiteSpace(uniqueID))
                    return slot >= 0 ? $"slot {slot} / {uniqueID}" : uniqueID;

                return slot >= 0 ? $"slot {slot}" : "no active save";
            }
        }

        internal static void Initialize()
        {
            observedScopeKey = CurrentSaveKey;
        }

        internal static void Update()
        {
            var currentScopeKey = CurrentSaveKey;
            if (string.IsNullOrWhiteSpace(observedScopeKey))
            {
                observedScopeKey = currentScopeKey;
                return;
            }

            if (string.Equals(observedScopeKey, currentScopeKey, StringComparison.OrdinalIgnoreCase))
                return;

            var previousScopeKey = observedScopeKey;
            observedScopeKey = currentScopeKey;
            AICharacterPlugin.Log?.LogInfo($"AI NPC save scope changed: {previousScopeKey} -> {currentScopeKey} ({CurrentDisplayName}). Reloading save-scoped AI state.");

            ResetSaveScopedProductState();
        }

        private static void ResetSaveScopedProductState()
        {
            RunSaveScopeReset("conversation overlay", FollowerAiConversationOverlay.ResetForSaveScopeChange);
            RunSaveScopeReset("current event observer", FollowerAiCurrentEventObservation.ResetForSaveScopeChange);
            RunSaveScopeReset("diagnostics", FollowerAiDiagnostics.ResetForSaveScopeChange);
            RunSaveScopeReset("live diagnostics", FollowerAiLiveDiagnostics.ResetForSaveScopeChange);
            RunSaveScopeReset("social memory", FollowerAiSocialMemory.ResetForSaveScopeChange);
            RunSaveScopeReset("tournament ledger", FollowerAiTournamentLedger.ResetForSaveScopeChange);
            RunSaveScopeReset("invocations", FollowerAiInvocations.ResetForSaveScopeChange);
            RunSaveScopeReset("character mode settings", FollowerAiCharacterModeSettings.ResetForSaveScopeChange);
            RunSaveScopeReset("NPC statuses", FollowerAIManager.LoadNPCStatuses);
        }

        private static void RunSaveScopeReset(string componentName, Action reset)
        {
            try
            {
                reset?.Invoke();
            }
            catch (Exception ex)
            {
                AICharacterPlugin.Log?.LogWarning($"AI NPC save scope reset failed for {componentName}: {ex.Message}");
            }
        }

        private static string GetSaveUniqueID()
        {
            try
            {
                return DataManager.Instance?.SaveUniqueID?.Trim() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static int GetSaveSlot()
        {
            try
            {
                return SaveAndLoad.SAVE_SLOT;
            }
            catch
            {
                return -1;
            }
        }

        private static string SanitizeScopeKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return UnsavedScopeKey;

            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(value
                    .Select(ch => invalid.Contains(ch) || char.IsWhiteSpace(ch) ? '-' : ch)
                    .Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.')
                    .ToArray())
                .Trim('-', '.', '_');

            while (cleaned.Contains("--"))
                cleaned = cleaned.Replace("--", "-");

            if (string.IsNullOrWhiteSpace(cleaned))
                return UnsavedScopeKey;

            return cleaned.Length <= 96 ? cleaned : cleaned.Substring(0, 96);
        }
    }
}

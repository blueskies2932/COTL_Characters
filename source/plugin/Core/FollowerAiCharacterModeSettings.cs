using BepInEx;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace COTL_AL_NPCs
{
    internal enum FollowerAiReplyLength
    {
        Short,
        Medium,
        Long
    }

    internal sealed class FollowerAiCharacterAwarenessSettings
    {
        public bool PersonalTraits = true;
        public bool CultAbout = true;
        public bool CurrentEvents = true;
        public bool TournamentDetails = true;
        public bool WorldState = true;
        public bool CurrentSessionConversation = true;
        public bool LongTermConversationHistory = false;
        public FollowerAiReplyLength ReplyLength = FollowerAiReplyLength.Medium;
        public bool Lore = false;
        public string LoreText = string.Empty;
    }

    internal sealed class FollowerAiCharacterModeSettingsSaveData
    {
        public Dictionary<int, FollowerAiCharacterAwarenessSettings> ByFollowerID = new Dictionary<int, FollowerAiCharacterAwarenessSettings>();
    }

    internal static class FollowerAiCharacterModeSettings
    {
        private static FollowerAiCharacterModeSettingsSaveData data = new FollowerAiCharacterModeSettingsSaveData();
        private static string loadedScope = string.Empty;

        private static string SavePath => Path.Combine(FollowerAiSaveScope.CurrentScopedDirectory, "CharacterModeSettings.json");

        internal static FollowerAiCharacterAwarenessSettings Get(int followerID)
        {
            EnsureLoaded();
            if (followerID < 0)
                return Default();

            if (!data.ByFollowerID.TryGetValue(followerID, out var settings) || settings == null)
            {
                settings = Default();
                data.ByFollowerID[followerID] = settings;
            }

            Normalize(settings);
            return settings;
        }

        internal static void Save(int followerID, FollowerAiCharacterAwarenessSettings settings)
        {
            EnsureLoaded();
            if (followerID < 0 || settings == null)
                return;

            Normalize(settings);
            data.ByFollowerID[followerID] = settings;
            Save();
        }

        internal static void ResetForSaveScopeChange()
        {
            loadedScope = string.Empty;
            data = new FollowerAiCharacterModeSettingsSaveData();
        }

        private static FollowerAiCharacterAwarenessSettings Default()
        {
            return new FollowerAiCharacterAwarenessSettings();
        }

        private static void EnsureLoaded()
        {
            var scope = FollowerAiSaveScope.CurrentSaveKey;
            if (string.Equals(loadedScope, scope, StringComparison.OrdinalIgnoreCase))
                return;

            loadedScope = scope;
            data = new FollowerAiCharacterModeSettingsSaveData();

            try
            {
                if (!File.Exists(SavePath))
                    return;

                data = JsonConvert.DeserializeObject<FollowerAiCharacterModeSettingsSaveData>(File.ReadAllText(SavePath)) ?? new FollowerAiCharacterModeSettingsSaveData();
                if (data.ByFollowerID == null)
                    data.ByFollowerID = new Dictionary<int, FollowerAiCharacterAwarenessSettings>();
                foreach (var settings in data.ByFollowerID.Values)
                    Normalize(settings);
            }
            catch (Exception ex)
            {
                AICharacterPlugin.Log?.LogWarning($"Failed to load Character Mode settings: {ex.Message}");
                data = new FollowerAiCharacterModeSettingsSaveData();
            }
        }

        private static void Save()
        {
            try
            {
                FollowerAiFileStore.WriteAllTextAtomic(SavePath, JsonConvert.SerializeObject(data, Formatting.Indented));
            }
            catch (Exception ex)
            {
                AICharacterPlugin.Log?.LogWarning($"Failed to save Character Mode settings: {ex.Message}");
            }
        }

        private static void Normalize(FollowerAiCharacterAwarenessSettings settings)
        {
            if (settings == null)
                return;

            settings.LoreText = (settings.LoreText ?? string.Empty).Trim();
            if (settings.LoreText.Length > 2000)
                settings.LoreText = settings.LoreText.Substring(0, 2000);
        }
    }
}

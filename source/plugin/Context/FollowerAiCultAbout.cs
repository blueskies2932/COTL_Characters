using System;
using System.IO;
using BepInEx;

namespace COTL_AL_NPCs
{
    internal static class FollowerAiCultAbout
    {
        private static string cachedScope = string.Empty;
        private static string cachedText = string.Empty;
        private static bool loaded;

        private static string FilePath => Path.Combine(FollowerAiSaveScope.CurrentScopedDirectory, "CultAbout.txt");

        internal static string Get()
        {
            EnsureLoaded();
            return cachedText ?? string.Empty;
        }

        internal static void Save(string text)
        {
            cachedScope = FollowerAiSaveScope.CurrentSaveKey;
            cachedText = Normalize(text);
            loaded = true;

            try
            {
                FollowerAiFileStore.WriteAllTextAtomic(FilePath, cachedText);
                AICharacterPlugin.Log?.LogInfo($"Saved AI cult about context to {FilePath}");
            }
            catch (Exception ex)
            {
                AICharacterPlugin.Log?.LogWarning($"Failed to save AI cult about context: {ex.Message}");
            }
        }

        internal static string BuildPromptContext()
        {
            var text = Get();
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            return $"Player-authored cult about context (higher priority than generic setting context): {text}";
        }

        private static void EnsureLoaded()
        {
            var scope = FollowerAiSaveScope.CurrentSaveKey;
            if (loaded && string.Equals(cachedScope, scope, StringComparison.OrdinalIgnoreCase))
                return;

            cachedScope = scope;
            loaded = true;
            cachedText = string.Empty;

            try
            {
                if (File.Exists(FilePath))
                    cachedText = Normalize(File.ReadAllText(FilePath));
            }
            catch (Exception ex)
            {
                AICharacterPlugin.Log?.LogWarning($"Failed to load AI cult about context: {ex.Message}");
                cachedText = string.Empty;
            }
        }

        private static string Normalize(string text)
        {
            return (text ?? string.Empty).Trim();
        }
    }
}

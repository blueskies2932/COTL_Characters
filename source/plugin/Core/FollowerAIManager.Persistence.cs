using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace COTL_AL_NPCs
{
    public static partial class FollowerAIManager
    {
        public static void LoadNPCStatuses()
        {
            loaded = true;
            loadedScopeKey = FollowerAiSaveScope.CurrentSaveKey;
            followerAIs.Clear();

            try
            {
                if (!File.Exists(SavePath))
                {
                    AICharacterPlugin.Log?.LogInfo($"No product AI follower state file found yet for save scope {FollowerAiSaveScope.CurrentDisplayName} at {SavePath}");
                    return;
                }

                var saved = JsonConvert.DeserializeObject<FollowerAiSaveData>(File.ReadAllText(SavePath)) ?? new FollowerAiSaveData();
                foreach (var savedFollower in saved.Followers ?? new List<FollowerAiSavedFollower>())
                {
                    if (savedFollower == null || savedFollower.FollowerID < 0)
                        continue;

                    var ai = GetOrCreateAIWithoutLoading(savedFollower.FollowerID);
                    ai.Mode = ParseMode(savedFollower.Mode, FollowerAiMode.Vanilla);
                    ai.IsNPC = ai.Mode != FollowerAiMode.Vanilla;
                    ai.ConversationHistory = FilterLoadedConversationHistory(savedFollower.ConversationHistory);
                    ai.OutcomeMemory = FilterLoadedMemory(savedFollower.OutcomeMemory);
                    ai.LastInteraction = ParseSavedDateTime(savedFollower.LastInteraction);
                }

                if (NormalizeLoadedNpcMemory())
                    MarkStateDirty();

                AICharacterPlugin.Log?.LogInfo($"Loaded {GetNPCFollowerIDs().Count} product AI follower records for save scope {FollowerAiSaveScope.CurrentDisplayName} from {SavePath}");
            }
            catch (Exception ex)
            {
                AICharacterPlugin.Log?.LogWarning($"Failed to load product AI follower state: {ex.Message}");
            }
        }

        internal static void CommitForGameSave(string source)
        {
            SaveFollowerState(force: true);
            AICharacterPlugin.Log?.LogInfo($"Committed AI NPC memory for game save: source={source}; scope={FollowerAiSaveScope.CurrentDisplayName}.");
        }

        private static void SaveNPCStatuses()
        {
            SaveNPCStatuses(commitToDisk: false);
        }

        private static void SaveNPCStatuses(bool commitToDisk)
        {
            try
            {
                if (!commitToDisk)
                {
                    MarkStateDirty();
                    return;
                }

                SaveFollowerState(force: true);
            }
            catch (Exception ex)
            {
                AICharacterPlugin.Log?.LogWarning($"Failed to save AI NPC follower state: {ex.Message}");
            }
        }

        private static bool NormalizeLoadedNpcMemory()
        {
            var changed = false;

            foreach (var ai in followerAIs.Values)
            {
                if (ScrubObsoleteMemory(ai))
                    changed = true;
            }

            return changed;
        }

        private static void SaveMemory()
        {
            SaveFollowerState(force: false);
        }

        private static void SaveFollowerState(bool force)
        {
            try
            {
                if (!force && !stateDirty && File.Exists(SavePath))
                    return;

                var saveData = BuildSaveData();
                FollowerAiFileStore.WriteAllTextAtomic(SavePath, JsonConvert.SerializeObject(saveData, Formatting.Indented));
                stateDirty = false;
            }
            catch (Exception ex)
            {
                MarkStateDirty();
                AICharacterPlugin.Log?.LogWarning($"Failed to save product AI follower state: {ex.Message}");
            }
        }

        private static FollowerAiSaveData BuildSaveData()
        {
            return new FollowerAiSaveData
            {
                SaveScope = FollowerAiSaveScope.CurrentSaveKey,
                Followers = followerAIs.Values
                    .Where(ShouldSaveFollowerRecord)
                    .OrderBy(ai => ai.FollowerID)
                    .Select(BuildSavedFollower)
                    .ToList()
            };
        }

        private static bool ShouldSaveFollowerRecord(FollowerAI ai)
        {
            return ai != null &&
                   (ai.IsNPC ||
                    (ai.ConversationHistory?.Count ?? 0) > 0 ||
                    (ai.OutcomeMemory?.Count ?? 0) > 0);
        }

        private static FollowerAiSavedFollower BuildSavedFollower(FollowerAI ai)
        {
            return new FollowerAiSavedFollower
            {
                FollowerID = ai.FollowerID,
                Mode = NormalizeMode(ai).ToString(),
                LastInteraction = FormatSavedDateTime(ai.LastInteraction),
                ConversationHistory = FilterLoadedConversationHistory(ai.ConversationHistory),
                OutcomeMemory = FilterLoadedMemory(ai.OutcomeMemory)
            };
        }

        private static List<string> FilterLoadedMemory(IEnumerable<string> lines)
        {
            return (lines ?? Enumerable.Empty<string>())
                .Where(line => !IsObsoleteMemoryLine(line))
                .Select(line => line?.Trim() ?? string.Empty)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();
        }

        private static List<string> FilterLoadedConversationHistory(IEnumerable<string> lines)
        {
            var filtered = (lines ?? Enumerable.Empty<string>())
                .Select(line => line?.Trim() ?? string.Empty)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();
            return filtered
                .Skip(Math.Max(0, filtered.Count - 80))
                .ToList();
        }

        private static bool ScrubObsoleteMemory(FollowerAI ai)
        {
            if (ai == null)
                return false;

            var beforeOutcomes = ai.OutcomeMemory?.Count ?? 0;
            ai.OutcomeMemory = FilterLoadedMemory(ai.OutcomeMemory);
            return beforeOutcomes != ai.OutcomeMemory.Count;
        }

        private static FollowerAiMode ParseMode(string value, FollowerAiMode fallback)
        {
            if (Enum.TryParse(value?.Trim() ?? string.Empty, ignoreCase: true, out FollowerAiMode parsed))
                return parsed;

            return fallback;
        }

        private static string FormatSavedDateTime(DateTime value)
        {
            var safeValue = value == default(DateTime) ? DateTime.Now : value;
            return safeValue.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private static DateTime ParseSavedDateTime(string value)
        {
            return DateTime.TryParse(value, out var parsed)
                ? parsed
                : DateTime.Now;
        }
    }
}

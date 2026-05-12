using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace COTL_AL_NPCs
{
    internal sealed class FollowerAiSocialEvent
    {
        [JsonProperty("key")]
        public string Key { get; set; } = string.Empty;

        [JsonProperty("type")]
        public string EventType { get; set; } = string.Empty;

        [JsonProperty("day")]
        public int Day { get; set; }

        [JsonProperty("recorded_at")]
        public DateTime Timestamp { get; set; }

        [JsonProperty("participant_ids")]
        public List<int> ParticipantIDs { get; set; } = new List<int>();

        [JsonProperty("summary")]
        public string Summary { get; set; } = string.Empty;

        [JsonProperty("data")]
        public Dictionary<string, string> Data { get; set; } = new Dictionary<string, string>();
    }

    internal sealed class FollowerAiCurrentEventsSaveData
    {
        [JsonProperty("schema")]
        public string Schema = "COTL_AL_NPCs.Product.CurrentEvents.v1";

        [JsonProperty("save_scope")]
        public string SaveScope = string.Empty;

        [JsonProperty("retained_days")]
        public int RetainedDays;

        [JsonProperty("events")]
        public List<FollowerAiSocialEvent> Events = new List<FollowerAiSocialEvent>();
    }

    internal static class FollowerAiSocialMemory
    {
        private const int RetainedCurrentEventDays = 3;
        private const int MaxStoredEvents = 300;
        private static readonly List<FollowerAiSocialEvent> events = new List<FollowerAiSocialEvent>();
        private static bool loaded;
        private static string loadedScopeKey = string.Empty;
        private static bool dirty;

        private static string SavePath => Path.Combine(FollowerAiSaveScope.CurrentScopedDirectory, "CurrentEvents.json");

        internal static bool RecordEvent(string key, string eventType, string summary, IEnumerable<int> participantIDs, Dictionary<string, string> data = null)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(summary))
                return false;

            EnsureLoaded();
            PruneOldCurrentEvents();
            if (events.Any(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)))
                return false;

            events.Add(new FollowerAiSocialEvent
            {
                Key = key.Trim(),
                EventType = string.IsNullOrWhiteSpace(eventType) ? "social_event" : eventType.Trim(),
                Day = GetCurrentGameDay(),
                Timestamp = DateTime.Now,
                ParticipantIDs = (participantIDs ?? Enumerable.Empty<int>())
                    .Where(id => id >= 0)
                    .Distinct()
                    .ToList(),
                Summary = summary.Trim(),
                Data = data ?? new Dictionary<string, string>()
            });

            PruneExcessEvents();
            MarkDirty();
            return true;
        }

        internal static List<string> GetRecentLinesForFollower(int followerID, int maxLines)
        {
            EnsureLoaded();
            PruneOldCurrentEvents();
            var lines = events
                .Where(item => item.ParticipantIDs != null && item.ParticipantIDs.Contains(followerID))
                .OrderBy(item => item.Timestamp)
                .Select(FormatLine)
                .ToList();

            return TakeLast(lines, maxLines);
        }

        internal static List<string> GetRecentGlobalLines(int maxLines)
        {
            EnsureLoaded();
            PruneOldCurrentEvents();
            var lines = events
                .OrderBy(item => item.Timestamp)
                .Select(FormatLine)
                .ToList();

            return TakeLast(lines, maxLines);
        }

        internal static List<string> GetCurrentDayLines(int maxLines)
        {
            EnsureLoaded();
            PruneOldCurrentEvents();
            var currentDay = GetCurrentGameDay();
            var lines = events
                .Where(item => item.Day == currentDay)
                .OrderBy(item => item.Timestamp)
                .Select(FormatLine)
                .ToList();

            return TakeLast(lines, maxLines);
        }

        internal static List<string> GetRecentDayLines(int dayWindow, int maxLines)
        {
            EnsureLoaded();
            PruneOldCurrentEvents();
            var currentDay = GetCurrentGameDay();
            var earliestDay = currentDay - Math.Max(0, dayWindow - 1);
            var lines = events
                .Where(item => item.Day >= earliestDay && item.Day <= currentDay)
                .OrderBy(item => item.Day)
                .ThenBy(item => item.Timestamp)
                .Select(FormatLine)
                .ToList();

            return TakeLast(lines, maxLines);
        }

        internal static bool SetEventData(string key, string dataKey, string dataValue)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(dataKey))
                return false;

            EnsureLoaded();
            PruneOldCurrentEvents();
            var item = FindByKey(key);
            if (item == null)
                return false;

            item.Data = item.Data ?? new Dictionary<string, string>();
            item.Data[dataKey] = dataValue ?? string.Empty;
            MarkDirty();
            return true;
        }

        internal static bool SetEventSummary(string key, string summary)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(summary))
                return false;

            EnsureLoaded();
            PruneOldCurrentEvents();
            var item = FindByKey(key);
            if (item == null)
                return false;

            item.Summary = summary.Trim();
            MarkDirty();
            return true;
        }

        internal static void ResetForSaveScopeChange()
        {
            events.Clear();
            loaded = false;
            loadedScopeKey = string.Empty;
            dirty = false;
        }

        internal static void CommitForGameSave(string source)
        {
            EnsureLoaded();
            PruneOldCurrentEvents();
            if (!dirty)
                return;

            Save();
            dirty = false;
            AICharacterPlugin.Log?.LogInfo($"Committed AI current events for game save: source={source}; scope={FollowerAiSaveScope.CurrentDisplayName}.");
        }

        private static FollowerAiSocialEvent FindByKey(string key)
        {
            return events.FirstOrDefault(record => string.Equals(record.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        private static List<string> TakeLast(List<string> lines, int maxLines)
        {
            return lines
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Skip(Math.Max(0, lines.Count - Math.Max(1, maxLines)))
                .ToList();
        }

        private static void MarkDirty()
        {
            dirty = true;
        }

        private static string FormatLine(FollowerAiSocialEvent item)
        {
            if (item == null)
                return string.Empty;

            return $"day={item.Day} type={item.EventType}: {item.Summary}";
        }

        private static void EnsureLoaded()
        {
            var currentScopeKey = FollowerAiSaveScope.CurrentSaveKey;
            if (loaded && string.Equals(loadedScopeKey, currentScopeKey, StringComparison.OrdinalIgnoreCase))
                return;

            loaded = true;
            loadedScopeKey = currentScopeKey;
            events.Clear();
            dirty = false;

            try
            {
                if (!File.Exists(SavePath))
                    return;

                var saved = JsonConvert.DeserializeObject<FollowerAiCurrentEventsSaveData>(File.ReadAllText(SavePath));
                events.AddRange((saved?.Events ?? new List<FollowerAiSocialEvent>())
                    .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Key)));
                PruneOldCurrentEvents();
            }
            catch (Exception ex)
            {
                AICharacterPlugin.Log?.LogWarning($"Failed to load current events: {ex.Message}");
            }
        }

        private static void PruneOldCurrentEvents()
        {
            var currentDay = GetCurrentGameDay();
            var earliestDay = currentDay - (RetainedCurrentEventDays - 1);
            var removed = events.RemoveAll(item => item == null || item.Day < earliestDay);
            if (removed > 0)
                MarkDirty();

            PruneExcessEvents();
        }

        private static void PruneExcessEvents()
        {
            if (events.Count <= MaxStoredEvents)
                return;

            events.Sort((left, right) =>
            {
                var dayCompare = left.Day.CompareTo(right.Day);
                return dayCompare != 0 ? dayCompare : left.Timestamp.CompareTo(right.Timestamp);
            });
            events.RemoveRange(0, events.Count - MaxStoredEvents);
            MarkDirty();
        }

        private static void Save()
        {
            try
            {
                var saveData = new FollowerAiCurrentEventsSaveData
                {
                    SaveScope = FollowerAiSaveScope.CurrentSaveKey,
                    RetainedDays = RetainedCurrentEventDays,
                    Events = events
                        .OrderBy(item => item.Day)
                        .ThenBy(item => item.Timestamp)
                        .ToList()
                };

                FollowerAiFileStore.WriteAllTextAtomic(SavePath, JsonConvert.SerializeObject(saveData, Formatting.Indented));
            }
            catch (Exception ex)
            {
                AICharacterPlugin.Log?.LogWarning($"Failed to save current events: {ex.Message}");
            }
        }

        private static int GetCurrentGameDay()
        {
            try
            {
                return TimeManager.CurrentDay;
            }
            catch
            {
                return DateTime.Now.DayOfYear;
            }
        }
    }
}

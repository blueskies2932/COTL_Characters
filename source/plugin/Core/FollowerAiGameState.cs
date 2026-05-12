using System;
using System.Linq;
using UnityEngine;

namespace COTL_AL_NPCs
{
    internal static class FollowerAiGameState
    {
        private const float SpecialEventScanIntervalSeconds = 0.5f;
        private const float SpecialEventRecoverySeconds = 5f;

        private static bool lastSimulationPaused;
        private static bool lastSpecialEventActive;
        private static bool lastSpecialEventRecoveryActive;
        private static bool cachedSpecialEventActive;
        private static float nextLogAtRealtime;
        private static float nextSpecialEventScanAtRealtime;
        private static float specialEventSuppressedUntilRealtime;
        private static float specialEventRecoveryUntilRealtime;
        private static float specialEventStartedAtRealtime;
        private static string specialEventSuppressionSource = string.Empty;
        private static string activeSpecialEventReason = string.Empty;
        private static string cachedSpecialEventReason = string.Empty;
        private static string specialEventRecoveryReason = string.Empty;

        internal static float TotalSpecialEventPauseSeconds { get; private set; }

        internal static bool IsSimulationPaused()
        {
            return Time.timeScale <= 0.001f;
        }

        internal static bool IsSpecialEventActive()
        {
            try
            {
                return TryFindCachedSpecialEventReason(out _);
            }
            catch (Exception ex)
            {
                AICharacterPlugin.Log?.LogWarning($"AI special-event detector failed: {ex.Message}");
                return false;
            }
        }

        internal static bool ShouldRunBackgroundBrainWork()
        {
            return !IsSimulationPaused() && !IsSpecialEventActive() && !IsSpecialEventRecoveryActive();
        }

        internal static bool ShouldAllowTaskInterference()
        {
            return !IsSpecialEventActive() && !IsSpecialEventRecoveryActive();
        }

        internal static bool IsSpecialEventRecoveryActive()
        {
            return Time.realtimeSinceStartup < specialEventRecoveryUntilRealtime;
        }

        internal static string GetSpecialEventRecoveryReason()
        {
            if (!IsSpecialEventRecoveryActive())
                return string.Empty;

            var source = string.IsNullOrWhiteSpace(specialEventRecoveryReason)
                ? "ritual/sermon/special event"
                : specialEventRecoveryReason;
            return $"{source} recovery, remaining={specialEventRecoveryUntilRealtime - Time.realtimeSinceStartup:0.0}s";
        }

        internal static float ConsumeSpecialEventPause(ref float observedTotalPauseSeconds)
        {
            var current = TotalSpecialEventPauseSeconds;
            if (observedTotalPauseSeconds < 0f)
            {
                observedTotalPauseSeconds = current;
                return 0f;
            }

            var delta = Math.Max(0f, current - observedTotalPauseSeconds);
            observedTotalPauseSeconds = current;
            return delta;
        }

        internal static string GetCurrentSpecialEventReason()
        {
            return string.IsNullOrWhiteSpace(activeSpecialEventReason)
                ? "ritual/sermon/special event"
                : activeSpecialEventReason;
        }

        internal static void SuppressTaskInterferenceForSeconds(string source, float seconds)
        {
            specialEventSuppressedUntilRealtime = Math.Max(
                specialEventSuppressedUntilRealtime,
                Time.realtimeSinceStartup + Math.Max(1f, seconds));
            specialEventSuppressionSource = source ?? string.Empty;
            nextSpecialEventScanAtRealtime = 0f;

            if (Time.realtimeSinceStartup >= nextLogAtRealtime)
            {
                nextLogAtRealtime = Time.realtimeSinceStartup + 2f;
                AICharacterPlugin.Log?.LogInfo($"AI task interference suppressed for special event via {source}.");
            }
        }

        internal static void UpdatePauseLogging()
        {
            var paused = IsSimulationPaused();
            var specialEventActive = TryFindCachedSpecialEventReason(out var specialEventReason);
            UpdateSpecialEventPauseClock(specialEventActive, specialEventReason);

            var recoveryActive = IsSpecialEventRecoveryActive();
            if (paused == lastSimulationPaused &&
                specialEventActive == lastSpecialEventActive &&
                recoveryActive == lastSpecialEventRecoveryActive)
            {
                return;
            }

            lastSimulationPaused = paused;
            lastSpecialEventActive = specialEventActive;
            lastSpecialEventRecoveryActive = recoveryActive;
            nextLogAtRealtime = Time.realtimeSinceStartup + 2f;

            if (paused)
                AICharacterPlugin.Log?.LogInfo("AI background brain work paused because game simulation is paused.");
            else if (specialEventActive)
                AICharacterPlugin.Log?.LogInfo($"AI queue paused for ritual/sermon/special event: {specialEventReason}");
            else if (recoveryActive)
                AICharacterPlugin.Log?.LogInfo($"AI queue waiting for ritual/sermon/special event recovery: {GetSpecialEventRecoveryReason()}");
            else
                AICharacterPlugin.Log?.LogInfo($"AI queue resumed because game simulation is running; event_pause_total={TotalSpecialEventPauseSeconds:0.0}s.");
        }

        private static void UpdateSpecialEventPauseClock(bool specialEventActive, string reason)
        {
            if (specialEventActive)
            {
                activeSpecialEventReason = string.IsNullOrWhiteSpace(reason) ? activeSpecialEventReason : reason;
                if (!lastSpecialEventActive)
                    specialEventStartedAtRealtime = Time.realtimeSinceStartup;
                return;
            }

            if (!lastSpecialEventActive)
            {
                activeSpecialEventReason = string.Empty;
                return;
            }

            var duration = Math.Max(0f, Time.realtimeSinceStartup - specialEventStartedAtRealtime);
            TotalSpecialEventPauseSeconds += duration;
            specialEventRecoveryUntilRealtime = Math.Max(
                specialEventRecoveryUntilRealtime,
                Time.realtimeSinceStartup + SpecialEventRecoverySeconds);
            specialEventRecoveryReason = string.IsNullOrWhiteSpace(activeSpecialEventReason)
                ? "ritual/sermon/special event"
                : activeSpecialEventReason;
            activeSpecialEventReason = string.Empty;
            specialEventStartedAtRealtime = 0f;
        }

        private static bool TryFindCachedSpecialEventReason(out string reason)
        {
            if (Time.realtimeSinceStartup >= nextSpecialEventScanAtRealtime)
            {
                nextSpecialEventScanAtRealtime = Time.realtimeSinceStartup + SpecialEventScanIntervalSeconds;
                cachedSpecialEventActive = TryFindSpecialEventReason(out cachedSpecialEventReason);
            }

            reason = cachedSpecialEventReason;
            return cachedSpecialEventActive;
        }

        private static bool TryFindSpecialEventReason(out string reason)
        {
            reason = string.Empty;
            if (Time.realtimeSinceStartup < specialEventSuppressedUntilRealtime)
            {
                var source = string.IsNullOrWhiteSpace(specialEventSuppressionSource)
                    ? "special-event guard"
                    : specialEventSuppressionSource;
                reason = $"suppression from {source}, remaining={specialEventSuppressedUntilRealtime - Time.realtimeSinceStartup:0.0}s";
                return true;
            }

            if (FollowerBrain.AllBrains == null)
                return false;

            foreach (var brain in FollowerBrain.AllBrains)
            {
                if (TryDescribeBrainSpecialEvent(brain, out reason))
                    return true;
            }

            return false;
        }

        private static bool TryDescribeBrainSpecialEvent(FollowerBrain brain, out string reason)
        {
            reason = string.Empty;
            if (brain == null)
                return false;

            var followerText = TryGetFollowerID(brain, out var followerID)
                ? $"follower={followerID}"
                : "follower=unknown";

            if (brain.InRitual)
            {
                reason = $"{followerText} InRitual";
                return true;
            }

            if (TryDescribeSpecialEventValue("state", brain.CurrentState, out var stateReason))
            {
                reason = $"{followerText} {stateReason}";
                return true;
            }

            if (TryDescribeSpecialEventValue("task", brain.CurrentTask, out var taskReason))
            {
                reason = $"{followerText} {taskReason}";
                return true;
            }

            if (TryDescribeSpecialEventValue("taskType", brain.CurrentTaskType, out var taskTypeReason))
            {
                reason = $"{followerText} {taskTypeReason}";
                return true;
            }

            return false;
        }

        private static bool TryDescribeSpecialEventValue(string label, object value, out string reason)
        {
            reason = string.Empty;
            if (value == null)
                return false;

            var text = value.GetType().IsEnum || value is string
                ? Convert.ToString(value) ?? string.Empty
                : value.GetType().Name ?? string.Empty;

            if (ContainsAny(text, "Ritual", "Sermon", "Ceremony", "Church"))
            {
                reason = $"{label}={text}";
                return true;
            }

            return false;
        }

        private static bool TryGetFollowerID(FollowerBrain brain, out int followerID)
        {
            followerID = -1;
            try
            {
                return FollowerAiNativeRoleTools.TryGetFollowerIDFromBrain(brain, out followerID);
            }
            catch
            {
                return false;
            }
        }

        private static bool ContainsAny(string value, params string[] terms)
        {
            return terms.Any(term => !string.IsNullOrWhiteSpace(term) &&
                                     (value ?? string.Empty).IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}

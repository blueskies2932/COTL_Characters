using BepInEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace COTL_AL_NPCs
{
    internal static class FollowerAiLiveDiagnostics
    {
        private const long MaxStreamBytes = 2L * 1024L * 1024L;
        private static float nextWriteAtRealtime;
        private static string lastScopeKey = string.Empty;

        internal static string ReportDirectory => Path.Combine(FollowerAiSaveScope.CurrentScopedDirectory, "LiveDiagnostics");
        private static string EventStreamPath => Path.Combine(ReportDirectory, "GameEventStream.jsonl");

        internal static void Update()
        {
            if (AICharacterPlugin.LiveDiagnosticsEnabled == null || !AICharacterPlugin.LiveDiagnosticsEnabled.Value)
                return;

            var now = Time.realtimeSinceStartup;
            if (now < nextWriteAtRealtime)
                return;

            nextWriteAtRealtime = now + GetIntervalSeconds();
            TryAppendSnapshot();
        }

        internal static void ResetForSaveScopeChange()
        {
            lastScopeKey = string.Empty;
            nextWriteAtRealtime = 0f;
        }

        private static void TryAppendSnapshot()
        {
            try
            {
                Directory.CreateDirectory(ReportDirectory);
                RotateIfNeeded(EventStreamPath);
                File.AppendAllText(EventStreamPath, BuildSnapshotLine() + Environment.NewLine, Encoding.UTF8);

                var scope = FollowerAiSaveScope.CurrentSaveKey;
                if (!string.Equals(lastScopeKey, scope, StringComparison.OrdinalIgnoreCase))
                {
                    lastScopeKey = scope;
                    AICharacterPlugin.Log?.LogInfo($"AI live diagnostics event stream active: {EventStreamPath}");
                }
            }
            catch (Exception ex)
            {
                AICharacterPlugin.LogInfoVerbose($"AI live diagnostics stream append failed: {ex.Message}");
            }
        }

        private static string BuildSnapshotLine()
        {
            var summaries = SafeTrackedFollowers();
            var builder = new StringBuilder();
            builder.Append("{");
            AppendJson(builder, "time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            builder.Append(",");
            AppendJson(builder, "scope", FollowerAiSaveScope.CurrentSaveKey);
            builder.Append(",");
            AppendJson(builder, "background", FollowerAiGameState.ShouldRunBackgroundBrainWork().ToString());
            builder.Append(",");
            AppendJson(builder, "paused", FollowerAiGameState.IsSimulationPaused().ToString());
            builder.Append(",");
            AppendJson(builder, "special_event", FollowerAiGameState.IsSpecialEventActive().ToString());
            builder.Append(",");
            AppendJson(builder, "world_state", Trim(FollowerAiWorldStateContext.BuildPromptContext(), 700));
            builder.Append(",");
            AppendJson(builder, "followers", string.Join(" || ", summaries.Select(BuildFollowerText)));
            builder.Append(",");
            AppendJson(builder, "diagnostics", string.Join(" || ", FollowerAiDiagnostics.GetRecent(6).Select(line => Trim(Redact(line), 420))));
            builder.Append("}");
            return builder.ToString();
        }

        private static List<FollowerAiFollowerFact> SafeTrackedFollowers()
        {
            try
            {
                return FollowerAiFollowerFacts.GetCurrentFollowers()
                    .Where(follower => follower != null && (follower.IsAiNpc || IsSpecial(follower.ID)))
                    .OrderBy(follower => follower.ID)
                    .ToList();
            }
            catch (Exception ex)
            {
                FollowerAiDiagnostics.Record("live diagnostics snapshot failed", ex.Message);
                return new List<FollowerAiFollowerFact>();
            }
        }

        private static string BuildFollowerText(FollowerAiFollowerFact follower)
        {
            var outcomes = string.Join(" / ", FollowerAIManager.GetOutcomeMemory(follower.ID).TakeLastCompat(2).Select(line => Trim(Redact(line), 160)));
            return $"id={follower.ID} name={follower.Name} flags={BuildFlags(follower)} role={follower.Role} task={follower.CurrentTask}/{follower.CurrentOverrideTask} state={follower.CurrentState} block={FollowerAiConversationOverlay.IsBlockingAutonomy(follower.ID)} hunger={follower.Satiation:0.00} exhaust={follower.Exhaustion:0.00} outcomes=[{outcomes}]";
        }

        private static string BuildFlags(FollowerAiFollowerFact follower)
        {
            var flags = new List<string>();
            if (follower.IsAiNpc) flags.Add("AI_NPC");
            return flags.Count == 0 ? "none" : string.Join(",", flags);
        }

        private static bool IsSpecial(int followerID)
        {
            return false;
        }

        private static void RotateIfNeeded(string path)
        {
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || info.Length < MaxStreamBytes)
                    return;

                var archive = Path.Combine(ReportDirectory, "GameEventStream.previous.jsonl");
                if (File.Exists(archive))
                    File.Delete(archive);
                File.Move(path, archive);
            }
            catch
            {
                // Rotation is best effort only.
            }
        }

        private static float GetIntervalSeconds()
        {
            return Mathf.Clamp(AICharacterPlugin.LiveDiagnosticsIntervalSeconds?.Value ?? 5f, 2f, 60f);
        }

        private static void AppendJson(StringBuilder builder, string key, string value)
        {
            builder.Append('"').Append(Escape(key)).Append("\":\"").Append(Escape(value ?? string.Empty)).Append('"');
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", " ")
                .Replace("\n", " ");
        }

        private static string Redact(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return ContainsAny(value, "api_key", "apikey", "secret", "token", "password", "credential", "authorization:", "bearer ")
                ? "[redacted credential-like diagnostic line]"
                : value;
        }

        private static string Trim(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var trimmed = value.Trim();
            return trimmed.Length <= maxLength ? trimmed : trimmed.Substring(0, Math.Max(0, maxLength - 3)) + "...";
        }

        private static string FormatIDs(IEnumerable<int> ids)
        {
            var list = (ids ?? Enumerable.Empty<int>()).ToList();
            return list.Count == 0 ? "none" : string.Join(",", list);
        }

        private static bool ContainsAny(string value, params string[] terms)
        {
            return !string.IsNullOrWhiteSpace(value)
                && terms.Any(term => value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static IEnumerable<string> TakeLastCompat(this List<string> values, int count)
        {
            if (values == null || values.Count == 0 || count <= 0)
                return Enumerable.Empty<string>();

            return values.Skip(Math.Max(0, values.Count - count));
        }
    }
}

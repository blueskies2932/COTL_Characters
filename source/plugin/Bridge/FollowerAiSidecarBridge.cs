using System;
using System.IO;
using System.Threading;
using UnityEngine;

namespace COTL_AL_NPCs
{
    internal static partial class FollowerAiSidecarBridge
    {
        private static float nextSnapshotRealtime;

        internal static void Update()
        {
            if (!IsEnabled())
            {
                StopStartedSidecar("AI sidecar disabled");
                return;
            }

            EnsureSidecarProcess();

            if (Time.realtimeSinceStartup < nextSnapshotRealtime)
                return;

            nextSnapshotRealtime = Time.realtimeSinceStartup + Math.Max(3f, AICharacterPlugin.SidecarSnapshotIntervalSeconds?.Value ?? 12f);
            ExportSnapshot();
            CleanupOldFiles();
        }

        internal static void Shutdown()
        {
            shutdownRequested = true;
            StopStartedSidecar("game closing");
        }

        internal static bool TryDecide(
            OpenAiFollowerDecisionContext context,
            out OpenAiFollowerDecision decision,
            out string message,
            Action<string> onProgress = null)
        {
            decision = null;
            message = string.Empty;

            if (!IsEnabled() || context == null)
                return false;

            if (!IsReady())
                return false;

            try
            {
                EnsureDirectories();

                var requestID = $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}";
                var requestPath = Path.Combine(RequestDirectory, $"{requestID}.request.json");
                var responsePath = Path.Combine(ResponseDirectory, $"{requestID}.response.json");
                var progressPath = Path.Combine(ProgressDirectory, $"{requestID}.progress.json");
                WriteJsonAtomic(requestPath, BuildDecisionRequestJson(context, requestID, progressPath));

                var timeout = Math.Max(5, AICharacterPlugin.SidecarDecisionTimeoutSeconds?.Value ?? 90);
                var deadline = DateTime.UtcNow.AddSeconds(timeout);
                var progressReported = false;
                while (DateTime.UtcNow < deadline)
                {
                    if (!progressReported &&
                        File.Exists(progressPath) &&
                        TryReadProgress(progressPath, out var progressMessage))
                    {
                        progressReported = true;
                        onProgress?.Invoke(progressMessage);
                    }

                    if (File.Exists(responsePath) && TryReadSidecarResponse(responsePath, out decision, out message))
                    {
                        TryArchiveRequest(requestPath);
                        return decision != null;
                    }

                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        TryArchiveRequest(requestPath);
                        return false;
                    }

                    Thread.Sleep(100);
                }

                message = $"Sidecar did not answer request {requestID} within {timeout}s.";
                return false;
            }
            catch (Exception ex)
            {
                message = $"Sidecar bridge failed: {ex.Message}.";
                AICharacterPlugin.Log?.LogWarning(message);
                return false;
            }
        }

        private static bool IsEnabled()
        {
            return AICharacterPlugin.SidecarEnabled != null && AICharacterPlugin.SidecarEnabled.Value;
        }

        internal static bool IsEnabledForDecisionRequests()
        {
            return IsEnabled();
        }
    }
}

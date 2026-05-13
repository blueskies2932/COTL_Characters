using BepInEx;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace COTL_AL_NPCs
{
    internal static partial class FollowerAiSidecarBridge
    {
        private const float ProcessStartRetrySeconds = 30f;

        private static float nextProcessStartAttemptRealtime;
        private static Process startedProcess;
        private static string startedRootDirectory = string.Empty;
        private static string lastLaunchFailure = string.Empty;
        private static bool shutdownRequested;

        private static void EnsureSidecarProcess()
        {
            if (shutdownRequested)
                return;

            if (AICharacterPlugin.SidecarAutoStartEnabled == null || !AICharacterPlugin.SidecarAutoStartEnabled.Value)
                return;

            var rootDirectory = RootDirectory;
            if (startedProcess != null)
            {
                if (HasExited(startedProcess))
                {
                    AICharacterPlugin.LogInfoVerbose("AI sidecar process exited; auto-start will retry after cooldown.");
                    DisposeStartedProcess();
                    nextProcessStartAttemptRealtime = Time.realtimeSinceStartup + ProcessStartRetrySeconds;
                    return;
                }

                if (string.Equals(startedRootDirectory, rootDirectory, StringComparison.OrdinalIgnoreCase))
                    return;

                StopStartedSidecar("save scope changed");
            }

            if (IsReady())
                return;

            if (Time.realtimeSinceStartup < nextProcessStartAttemptRealtime)
                return;

            nextProcessStartAttemptRealtime = Time.realtimeSinceStartup + ProcessStartRetrySeconds;

            try
            {
                EnsureDirectories();

                if (!TryResolveSidecarExecutable(out var sidecarPath))
                {
                    LogLaunchFailure("AI sidecar auto-start could not find CotlAiNpcSidecar.exe or CotlAiNpcSidecar.dll. Install the packaged sidecar beside the plugin or set AI Sidecar.ExecutablePath.");
                    return;
                }

                var startInfo = BuildSidecarStartInfo(sidecarPath, rootDirectory);
                var process = Process.Start(startInfo);
                if (process == null)
                {
                    LogLaunchFailure($"AI sidecar auto-start failed to launch {sidecarPath}.");
                    return;
                }

                startedProcess = process;
                startedRootDirectory = rootDirectory;
                lastLaunchFailure = string.Empty;
                AICharacterPlugin.Log?.LogInfo($"AI sidecar auto-started pid={process.Id} root={rootDirectory}");
            }
            catch (Exception ex)
            {
                LogLaunchFailure($"AI sidecar auto-start failed: {ex.Message}");
            }
        }

        private static ProcessStartInfo BuildSidecarStartInfo(string sidecarPath, string rootDirectory)
        {
            var extension = Path.GetExtension(sidecarPath).ToLowerInvariant();
            var useDotnet = extension == ".dll";
            var dotnetPath = AICharacterPlugin.SidecarDotnetPath?.Value;
            if (string.IsNullOrWhiteSpace(dotnetPath))
                dotnetPath = "dotnet";

            var startInfo = new ProcessStartInfo
            {
                FileName = useDotnet ? dotnetPath : sidecarPath,
                Arguments = useDotnet
                    ? $"{QuoteArgument(sidecarPath)} --root {QuoteArgument(rootDirectory)} --provider-config {QuoteArgument(FollowerAiProviderSetup.ProviderConfigPath)} --parent-pid {Process.GetCurrentProcess().Id}"
                    : $"--root {QuoteArgument(rootDirectory)} --provider-config {QuoteArgument(FollowerAiProviderSetup.ProviderConfigPath)} --parent-pid {Process.GetCurrentProcess().Id}",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = Path.GetDirectoryName(sidecarPath) ?? RootDirectory
            };

            return startInfo;
        }

        internal static bool TryTestProviderConfiguration(
            FollowerAiProviderSetupDraft draft,
            string apiKey,
            out string message)
        {
            message = string.Empty;
            var usedProvidedKey = !string.IsNullOrWhiteSpace((apiKey ?? string.Empty).Trim());
            try
            {
                EnsureDirectories();
                if (!TryResolveSidecarExecutable(out var sidecarPath))
                {
                    message = "Could not find the packaged AI sidecar runtime.";
                    return FinishProviderSetupTest(draft, false, message, usedProvidedKey);
                }

                var testDirectory = Path.Combine(FollowerAiProviderSetup.ConfigDirectory, "ProviderSetupTests");
                Directory.CreateDirectory(testDirectory);
                var testID = Guid.NewGuid().ToString("N");
                var configPath = Path.Combine(testDirectory, $"AiProvider.Test.{testID}.json");
                var keyPath = Path.Combine(testDirectory, $"AiProviderKey.Test.{testID}.txt");

                try
                {
                    var trimmedKey = (apiKey ?? string.Empty).Trim();
                    var apiKeyFile = string.Empty;
                    if (!string.IsNullOrWhiteSpace(trimmedKey))
                    {
                        File.WriteAllText(keyPath, trimmedKey);
                        apiKeyFile = keyPath;
                    }

                    var config = new JObject
                    {
                        ["providerType"] = (draft?.ProviderType ?? string.Empty).Trim(),
                        ["apiKeyEnvVar"] = (draft?.ApiKeyEnvVar ?? string.Empty).Trim(),
                        ["apiKeyFile"] = apiKeyFile,
                        ["requiresApiKey"] = draft?.RequiresApiKey ?? true,
                        ["setupComplete"] = true,
                        ["baseUrl"] = (draft?.BaseUrl ?? string.Empty).Trim(),
                        ["endpointPath"] = (draft?.EndpointPath ?? string.Empty).Trim(),
                        ["model"] = (draft?.Model ?? string.Empty).Trim(),
                        ["timeoutSeconds"] = Math.Max(10, draft?.TimeoutSeconds ?? 120),
                        ["temperature"] = null,
                        ["maxTokens"] = null,
                        ["headers"] = new JObject()
                    };
                    File.WriteAllText(configPath, config.ToString());

                    var startInfo = BuildSidecarTestStartInfo(sidecarPath, configPath, Math.Max(10, draft?.TimeoutSeconds ?? 120));
                    using (var process = Process.Start(startInfo))
                    {
                        if (process == null)
                        {
                            message = "Could not start the AI sidecar test process.";
                            return FinishProviderSetupTest(draft, false, message, usedProvidedKey);
                        }

                        var timeoutMs = Math.Max(15000, (Math.Max(10, draft?.TimeoutSeconds ?? 120) + 8) * 1000);
                        if (!process.WaitForExit(timeoutMs))
                        {
                            TryKill(process);
                            message = "Provider/model test timed out.";
                            return FinishProviderSetupTest(draft, false, message, usedProvidedKey);
                        }

                        var output = (process.StandardOutput.ReadToEnd() + "\n" + process.StandardError.ReadToEnd()).Trim();
                        message = RedactProviderTestOutput(output);
                        if (process.ExitCode == 0)
                        {
                            if (string.IsNullOrWhiteSpace(message))
                                message = "Provider/model test succeeded.";
                            return FinishProviderSetupTest(draft, true, message, usedProvidedKey);
                        }

                        if (string.IsNullOrWhiteSpace(message))
                            message = "Provider/model test failed.";
                        return FinishProviderSetupTest(draft, false, message, usedProvidedKey);
                    }
                }
                finally
                {
                    TryDelete(configPath);
                    TryDelete(keyPath);
                }
            }
            catch (Exception ex)
            {
                message = $"Provider/model test failed: {ex.Message}";
                return FinishProviderSetupTest(draft, false, message, usedProvidedKey);
            }
        }

        private static bool FinishProviderSetupTest(FollowerAiProviderSetupDraft draft, bool success, string message, bool usedProvidedKey)
        {
            FollowerAiProviderSetup.WriteLastProviderSetupTest(draft, success, message, usedProvidedKey);
            if (success)
                AICharacterPlugin.Log?.LogInfo($"AI provider setup test passed for {draft?.ProviderType}/{draft?.Model}.");
            else
                AICharacterPlugin.Log?.LogWarning($"AI provider setup test failed for {draft?.ProviderType}/{draft?.Model}: {message}");
            return success;
        }

        internal static void RestartForProviderSetupChange()
        {
            StopStartedSidecar("AI provider setup changed");
            try
            {
                if (File.Exists(ReadyPath))
                    File.Delete(ReadyPath);
            }
            catch
            {
                // Best effort.
            }

            nextProcessStartAttemptRealtime = 0f;
        }

        private static ProcessStartInfo BuildSidecarTestStartInfo(string sidecarPath, string configPath, int timeoutSeconds)
        {
            var extension = Path.GetExtension(sidecarPath).ToLowerInvariant();
            var useDotnet = extension == ".dll";
            var dotnetPath = AICharacterPlugin.SidecarDotnetPath?.Value;
            if (string.IsNullOrWhiteSpace(dotnetPath))
                dotnetPath = "dotnet";

            return new ProcessStartInfo
            {
                FileName = useDotnet ? dotnetPath : sidecarPath,
                Arguments = useDotnet
                    ? $"{QuoteArgument(sidecarPath)} --root {QuoteArgument(RootDirectory)} --provider-config {QuoteArgument(configPath)} --timeout {timeoutSeconds} --test-provider"
                    : $"--root {QuoteArgument(RootDirectory)} --provider-config {QuoteArgument(configPath)} --timeout {timeoutSeconds} --test-provider",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = Path.GetDirectoryName(sidecarPath) ?? RootDirectory
            };
        }

        private static string RedactProviderTestOutput(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var redacted = Regex.Replace(text, @"sk-[A-Za-z0-9_\-]+", "[REDACTED_KEY]");
            redacted = Regex.Replace(redacted, @"(?i)(api[_ -]?key\s*[:=]\s*)\S+", "$1[REDACTED_KEY]");
            redacted = Regex.Replace(redacted, @"proj_[A-Za-z0-9_\-]+", "[REDACTED_PROJECT]");
            redacted = Regex.Replace(redacted, @"org-[A-Za-z0-9_\-]+", "[REDACTED_ORG]");
            return Trim(redacted, 900);
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (process != null && !process.HasExited)
                    process.Kill();
            }
            catch
            {
                // Best effort.
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Best effort.
            }
        }

        private static bool TryResolveSidecarExecutable(out string sidecarPath)
        {
            sidecarPath = string.Empty;

            foreach (var candidate in GetSidecarExecutableCandidates())
            {
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                try
                {
                    var expanded = Environment.ExpandEnvironmentVariables(candidate.Trim());
                    var fullPath = Path.GetFullPath(expanded);
                    if (!File.Exists(fullPath))
                        continue;

                    var extension = Path.GetExtension(fullPath).ToLowerInvariant();
                    if (extension != ".exe" && extension != ".dll")
                        continue;

                    sidecarPath = fullPath;
                    return true;
                }
                catch
                {
                    // Keep probing other candidates.
                }
            }

            return false;
        }

        private static IEnumerable<string> GetSidecarExecutableCandidates()
        {
            var configured = AICharacterPlugin.SidecarExecutablePath?.Value;
            if (!string.IsNullOrWhiteSpace(configured))
            {
                var expanded = Environment.ExpandEnvironmentVariables(configured.Trim());
                if (Directory.Exists(expanded))
                {
                    yield return Path.Combine(expanded, "CotlAiNpcSidecar.exe");
                    yield return Path.Combine(expanded, "CotlAiNpcSidecar.dll");
                }
                else
                {
                    yield return expanded;
                }
            }

            foreach (var root in GetPackagedSidecarRoots())
            {
                foreach (var candidate in BuildSidecarOutputCandidates(root))
                    yield return candidate;
            }
        }

        private static IEnumerable<string> GetPackagedSidecarRoots()
        {
            var pluginDir = Path.Combine(Paths.PluginPath, "COTL_AL_NPCs");
            yield return Path.Combine(pluginDir, "sidecar");
            yield return pluginDir;

            var assemblyPath = typeof(AICharacterPlugin).Assembly.Location;
            if (!string.IsNullOrWhiteSpace(assemblyPath))
            {
                var assemblyDir = Path.GetDirectoryName(assemblyPath);
                if (!string.IsNullOrWhiteSpace(assemblyDir))
                {
                    yield return Path.Combine(assemblyDir, "sidecar");
                    yield return assemblyDir;
                }
            }
        }

        private static IEnumerable<string> BuildSidecarOutputCandidates(string directory)
        {
            yield return Path.Combine(directory, "CotlAiNpcSidecar.exe");
            yield return Path.Combine(directory, "CotlAiNpcSidecar.dll");
        }

        private static void StopStartedSidecar(string reason)
        {
            if (startedProcess == null)
                return;

            try
            {
                if (!HasExited(startedProcess))
                {
                    AICharacterPlugin.Log?.LogInfo($"Stopping AI sidecar pid={startedProcess.Id}: {reason}.");
                    try
                    {
                        startedProcess.CloseMainWindow();
                    }
                    catch
                    {
                        // Hidden console apps usually do not have a main window.
                    }

                    if (!startedProcess.WaitForExit(1500))
                    {
                        startedProcess.Kill();
                        startedProcess.WaitForExit(2500);
                    }
                }
            }
            catch (Exception ex)
            {
                AICharacterPlugin.LogInfoVerbose($"AI sidecar stop skipped: {ex.Message}");
            }
            finally
            {
                DisposeStartedProcess();
            }
        }

        private static bool IsReady()
        {
            try
            {
                if (!File.Exists(ReadyPath))
                    return false;

                var maxAge = Math.Max(3, AICharacterPlugin.SidecarReadyStaleSeconds?.Value ?? 20);
                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(ReadyPath) > TimeSpan.FromSeconds(maxAge))
                    return false;

                var ready = JObject.Parse(File.ReadAllText(ReadyPath));
                var pid = ready["pid"]?.Value<int>() ?? 0;
                return pid <= 0 || IsProcessAlive(pid);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsProcessAlive(int pid)
        {
            try
            {
                using (var process = Process.GetProcessById(pid))
                    return !process.HasExited;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch
            {
                return true;
            }
        }

        private static bool HasExited(Process process)
        {
            try
            {
                return process.HasExited;
            }
            catch
            {
                return true;
            }
        }

        private static void DisposeStartedProcess()
        {
            try
            {
                startedProcess?.Dispose();
            }
            catch
            {
                // Best effort.
            }

            startedProcess = null;
            startedRootDirectory = string.Empty;
        }

        private static void LogLaunchFailure(string message)
        {
            if (string.Equals(lastLaunchFailure, message, StringComparison.Ordinal))
                return;

            lastLaunchFailure = message;
            AICharacterPlugin.Log?.LogWarning(message);
        }
    }
}

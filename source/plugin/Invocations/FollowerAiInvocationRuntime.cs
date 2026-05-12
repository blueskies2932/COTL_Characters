using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace COTL_AL_NPCs
{
    internal static partial class FollowerAiInvocations
    {
        internal static bool TryHandleSubmittedCode(int speakerID, string submittedText, out string receipt, out string invocationLabel)
        {
            receipt = string.Empty;
            invocationLabel = string.Empty;
            if (string.IsNullOrWhiteSpace(submittedText))
                return false;

            EnsureLoaded();
            FollowerAiInvocationEntry match;
            lock (Sync)
            {
                match = state.Invocations.FirstOrDefault(entry =>
                    entry != null &&
                    !string.IsNullOrWhiteSpace(entry.Code) &&
                    string.Equals(entry.Code.Trim(), submittedText.Trim(), StringComparison.Ordinal));
            }

            if (match == null)
                return false;

            receipt = ExecuteInvocation(match.Id, speakerID, match.Name);
            return true;
        }

        private static string ExecuteInvocation(string id, int speakerID, string name)
        {
            try
            {
                if (string.Equals(id, MaxCultFaithID, StringComparison.OrdinalIgnoreCase))
                    return FormatInvocationReceipt(InvokeMaxCultFaith());

                if (string.Equals(id, ClearVanillaFollowerRolesID, StringComparison.OrdinalIgnoreCase))
                    return FormatInvocationReceipt(InvokeClearVanillaFollowerRoles());

                return FormatInvocationReceipt(success: false);
            }
            catch (Exception ex)
            {
                AICharacterPlugin.Log?.LogError($"Invocation failed for {name}: {ex}");
                return FormatInvocationReceipt(success: false);
            }
        }

        private static bool InvokeMaxCultFaith()
        {
            var cultFaithManagerType = FindType("CultFaithManager");
            if (cultFaithManagerType == null)
                return false;

            var instance = GetStaticMemberValue(cultFaithManagerType, "Instance");
            if (instance == null)
                return false;

            var current = ReadFloatMember(instance, "CurrentFaith");
            var max = ReadStaticFloatMember(cultFaithManagerType, "MAX_FAITH");
            if (max <= 0f)
                max = 100f;

            var delta = Mathf.Max(0f, max - current);
            if (delta <= 0.01f)
                return true;

            var flairType = FindType("NotificationBase+Flair");
            if (flairType == null)
                return false;

            var method = cultFaithManagerType.GetMethod(
                "GetFaith",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[]
                {
                    typeof(float),
                    typeof(float),
                    typeof(bool),
                    flairType,
                    typeof(string),
                    typeof(int),
                    typeof(string[])
                },
                null);
            if (method == null)
                return false;

            var flair = Enum.ToObject(flairType, 0);
            method.Invoke(null, new object[] { delta, delta, true, flair, string.Empty, -1, new string[0] });
            return true;
        }

        private static bool InvokeClearVanillaFollowerRoles()
        {
            var facts = FollowerAiFollowerFacts.GetCurrentFollowers();
            var attempted = 0;
            var cleared = 0;
            var skippedAi = 0;
            var failures = new List<string>();

            foreach (var fact in facts ?? new List<FollowerAiFollowerFact>())
            {
                if (fact == null || fact.ID < 0)
                    continue;

                if (fact.IsAiNpc)
                {
                    skippedAi++;
                    continue;
                }

                attempted++;
                if (FollowerAiNativeRoleTools.TryClearVanillaFollowerRole(fact.ID, out var message))
                {
                    cleared++;
                    AICharacterPlugin.LogInfoVerbose($"Invocation clear vanilla follower roles: {message}");
                }
                else
                {
                    failures.Add(message);
                    AICharacterPlugin.Log?.LogWarning($"Invocation clear vanilla follower roles failed: {message}");
                }
            }

            AICharacterPlugin.Log?.LogInfo($"Invocation clear vanilla follower roles finished: attempted={attempted}; cleared={cleared}; skipped_ai={skippedAi}; failures={failures.Count}.");
            return attempted == cleared && failures.Count == 0;
        }

        private static Type FindType(string fullNameOrName)
        {
            if (string.IsNullOrWhiteSpace(fullNameOrName))
                return null;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type exact = null;
                try
                {
                    exact = assembly.GetType(fullNameOrName, false);
                }
                catch
                {
                    exact = null;
                }

                if (exact != null)
                    return exact;

                try
                {
                    var match = assembly.GetTypes().FirstOrDefault(type =>
                        string.Equals(type.FullName, fullNameOrName, StringComparison.Ordinal) ||
                        string.Equals(type.Name, fullNameOrName, StringComparison.Ordinal));
                    if (match != null)
                        return match;
                }
                catch
                {
                    // Some dynamic assemblies do not allow type enumeration.
                }
            }

            return null;
        }

        private static object GetStaticMemberValue(Type type, string name)
        {
            if (type == null || string.IsNullOrWhiteSpace(name))
                return null;

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            var field = type.GetField(name, flags);
            if (field != null)
                return field.GetValue(null);

            var property = type.GetProperty(name, flags);
            return property?.GetValue(null, null);
        }

        private static float ReadStaticFloatMember(Type type, string name)
        {
            return ConvertToFloat(GetStaticMemberValue(type, name), -1f);
        }

        private static float ReadFloatMember(object instance, string name)
        {
            if (instance == null || string.IsNullOrWhiteSpace(name))
                return -1f;

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var type = instance.GetType();
            var property = type.GetProperty(name, flags);
            if (property != null)
                return ConvertToFloat(property.GetValue(instance, null), -1f);

            var field = type.GetField(name, flags);
            return field != null ? ConvertToFloat(field.GetValue(instance), -1f) : -1f;
        }

        private static float ConvertToFloat(object value, float fallback)
        {
            if (value == null)
                return fallback;

            try
            {
                return Convert.ToSingle(value);
            }
            catch
            {
                return fallback;
            }
        }
    }
}

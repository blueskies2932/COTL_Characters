using Lamb.UI;
using System;
using System.Reflection;

namespace COTL_AL_NPCs
{
    public partial class AICharacterPlugin
    {
        internal static void TryApplyNPCToRecruit(FollowerRecruit __instance, string source)
        {
            AICharacterPlugin.Log.LogInfo($"TryApplyNPCToRecruit called from {source}. NextFollowerMode={AICharacterPlugin.NextFollowerMode}");

            if (__instance == null)
            {
                AICharacterPlugin.Log.LogWarning("TryApplyNPCToRecruit received null __instance.");
                return;
            }

            if (AICharacterPlugin.NextFollowerMode == FollowerAiMode.Vanilla)
                return;

            try
            {
                var recruitType = __instance.GetType();
                AICharacterPlugin.Log.LogInfo($"FollowerRecruit instance type: {recruitType.FullName}");

                object followerObject = null;
                var followerField = recruitType.GetField("Follower", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (followerField != null)
                    followerObject = followerField.GetValue(__instance);

                if (followerObject == null)
                {
                    var followerProperty = recruitType.GetProperty("Follower", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (followerProperty != null)
                        followerObject = followerProperty.GetValue(__instance);
                }

                if (followerObject == null)
                {
                    var allFields = recruitType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (var field in allFields)
                    {
                        if (field.Name.IndexOf("follower", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            followerObject = field.GetValue(__instance);
                            if (followerObject != null)
                            {
                                AICharacterPlugin.Log.LogInfo($"Found follower via field {field.Name}.");
                                break;
                            }
                        }
                    }
                }

                if (followerObject == null)
                {
                    var allProperties = recruitType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (var property in allProperties)
                    {
                        if (property.Name.IndexOf("follower", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            followerObject = property.GetValue(__instance);
                            if (followerObject != null)
                            {
                                AICharacterPlugin.Log.LogInfo($"Found follower via property {property.Name}.");
                                break;
                            }
                        }
                    }
                }

                if (followerObject == null)
                {
                    AICharacterPlugin.Log.LogWarning("Unable to locate recruited follower object on FollowerRecruit.");
                    return;
                }

                var followerInfoType = followerObject.GetType();
                var idField = followerInfoType.GetField("ID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                int followerID = -1;

                if (idField != null)
                {
                    followerID = (int)idField.GetValue(followerObject);
                }
                else
                {
                    var idProperty = followerInfoType.GetProperty("ID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (idProperty != null)
                    {
                        followerID = (int)idProperty.GetValue(followerObject);
                    }
                }

                if (followerID < 0)
                {
                    AICharacterPlugin.Log.LogWarning("Recruited follower ID could not be read from FollowerRecruit.");
                    return;
                }

                var appliedMode = AICharacterPlugin.NextFollowerMode == FollowerAiMode.Character
                    ? FollowerAiMode.Character
                    : FollowerAiMode.Vanilla;
                if (appliedMode == FollowerAiMode.Vanilla)
                {
                    AICharacterPlugin.NextFollowerIsNPC = false;
                    AICharacterPlugin.NextFollowerMode = FollowerAiMode.Vanilla;
                    AICharacterPlugin.Log.LogWarning($"NPC mode was not applied to recruited follower {followerID}; requested mode was unavailable.");
                    return;
                }

                FollowerAIManager.SetNPCMode(followerID, appliedMode);
                AICharacterPlugin.NextFollowerIsNPC = false;
                AICharacterPlugin.NextFollowerMode = FollowerAiMode.Vanilla;
                AICharacterPlugin.Log.LogInfo($"Applied NPC {appliedMode} Mode to recruited follower {followerID}.");
            }
            catch (Exception ex)
            {
                AICharacterPlugin.Log.LogError($"Failed to apply NPC Character after recruit: {ex}");
            }
        }

        internal static void TryApplyNPCToIndoctrinationTarget(UIFollowerIndoctrinationMenuController __instance, string source)
        {
            AICharacterPlugin.Log.LogInfo($"TryApplyNPCToIndoctrinationTarget called from {source}. NextFollowerMode={AICharacterPlugin.NextFollowerMode}");

            if (__instance == null)
            {
                AICharacterPlugin.Log.LogWarning("TryApplyNPCToIndoctrinationTarget received null __instance.");
                return;
            }

            try
            {
                var targetFollower = GetMemberValue(__instance, "_targetFollower") ?? GetMemberValue(__instance, "targetFollower");
                if (targetFollower == null)
                {
                    AICharacterPlugin.Log.LogWarning("Unable to locate _targetFollower on indoctrination menu.");
                    DumpRecruitSource(__instance);
                    return;
                }

                if (!TryGetFollowerID(targetFollower, out var followerID))
                {
                    AICharacterPlugin.Log.LogWarning($"Unable to read follower ID from indoctrination target type {targetFollower.GetType().FullName}.");
                    DumpObjectMembers(targetFollower, "IndoctrinationTargetFollower", 1);
                    return;
                }

                var wasNpc = FollowerAIManager.IsModNPC(followerID);
                if (AICharacterPlugin.NextFollowerMode == FollowerAiMode.Vanilla)
                {
                    if (wasNpc)
                    {
                        FollowerAiConversationOverlay.ClearLiveConversationForFollower(followerID, $"vanilla reindoctrination from {source}");
                        FollowerAIManager.ResetForFreshIndoctrination(followerID, source);
                        AICharacterPlugin.Log.LogInfo($"Cleared AI NPC Character from reindoctrinated follower {followerID}; NPC toggle was not selected.");
                    }

                    AICharacterPlugin.NextFollowerIsNPC = false;
                    AICharacterPlugin.NextFollowerMode = FollowerAiMode.Vanilla;
                    return;
                }

                if (wasNpc)
                {
                    FollowerAiConversationOverlay.ClearLiveConversationForFollower(followerID, $"fresh indoctrination from {source}");
                    FollowerAIManager.ResetForFreshIndoctrination(followerID, source);
                }

                var appliedMode = AICharacterPlugin.NextFollowerMode == FollowerAiMode.Character
                    ? FollowerAiMode.Character
                    : FollowerAiMode.Vanilla;
                if (appliedMode == FollowerAiMode.Vanilla)
                {
                    AICharacterPlugin.NextFollowerIsNPC = false;
                    AICharacterPlugin.NextFollowerMode = FollowerAiMode.Vanilla;
                    AICharacterPlugin.Log.LogWarning($"NPC mode was not applied to indoctrination target follower {followerID}; requested mode was unavailable.");
                    return;
                }

                FollowerAIManager.SetNPCMode(followerID, appliedMode);
                AICharacterPlugin.NextFollowerIsNPC = false;
                AICharacterPlugin.NextFollowerMode = FollowerAiMode.Vanilla;
                AICharacterPlugin.Log.LogInfo($"Applied NPC {appliedMode} Mode to indoctrination target follower {followerID}.");
            }
            catch (Exception ex)
            {
                AICharacterPlugin.Log.LogError($"Failed to apply NPC Character from indoctrination menu: {ex}");
            }
        }

        private static object GetMemberValue(object instance, string memberName)
        {
            var type = instance.GetType();

            var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
                return field.GetValue(instance);

            var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
                return property.GetValue(instance);

            return null;
        }

        private static bool TryGetFollowerID(object followerObject, out int followerID)
        {
            followerID = -1;
            if (TryReadIntMember(followerObject, out followerID, "ID", "id", "Id", "FollowerID", "followerID", "FollowerId", "followerId"))
                return followerID >= 0;

            foreach (var memberName in new[] { "Info", "info", "_info", "Brain", "brain", "_brain", "Data", "data", "_data", "FollowerInfo", "followerInfo", "_followerInfo" })
            {
                var nestedObject = GetMemberValue(followerObject, memberName);
                if (nestedObject == null)
                    continue;

                if (TryReadIntMember(nestedObject, out followerID, "ID", "id", "Id", "FollowerID", "followerID", "FollowerId", "followerId"))
                {
                    AICharacterPlugin.Log.LogInfo($"Found follower ID via {memberName}.");
                    return followerID >= 0;
                }

                foreach (var secondMemberName in new[] { "Info", "info", "_info", "Data", "data", "_data", "FollowerInfo", "followerInfo", "_followerInfo" })
                {
                    var secondNestedObject = GetMemberValue(nestedObject, secondMemberName);
                    if (secondNestedObject == null)
                        continue;

                    if (TryReadIntMember(secondNestedObject, out followerID, "ID", "id", "Id", "FollowerID", "followerID", "FollowerId", "followerId"))
                    {
                        AICharacterPlugin.Log.LogInfo($"Found follower ID via {memberName}.{secondMemberName}.");
                        return followerID >= 0;
                    }
                }
            }

            return false;
        }

        private static bool TryReadIntMember(object instance, out int value, params string[] memberNames)
        {
            value = -1;

            foreach (var memberName in memberNames)
            {
                var memberValue = GetMemberValue(instance, memberName);
                if (memberValue is int intValue)
                {
                    value = intValue;
                    return true;
                }

                if (memberValue != null && int.TryParse(memberValue.ToString(), out var parsedValue))
                {
                    value = parsedValue;
                    return true;
                }
            }

            return false;
        }

        private static void DumpObjectMembers(object instance, string label, int maxNestedDepth)
        {
            if (instance == null)
            {
                AICharacterPlugin.Log.LogInfo($"{label}: null");
                return;
            }

            try
            {
                DumpObjectMembers(instance, label, 0, maxNestedDepth);
            }
            catch (Exception ex)
            {
                AICharacterPlugin.Log.LogWarning($"{label} dump failed: {ex.Message}");
            }
        }

        private static void DumpObjectMembers(object instance, string label, int depth, int maxNestedDepth)
        {
            var type = instance.GetType();
            AICharacterPlugin.Log.LogInfo($"{label}: type={type.FullName} value={instance}");

            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!ShouldDumpMember(field.Name))
                    continue;

                var value = field.GetValue(instance);
                AICharacterPlugin.Log.LogInfo($"{label} field {field.Name} type={(value?.GetType().FullName ?? "null")} value={FormatDumpValue(value)}");

                if (depth < maxNestedDepth && ShouldInspectNestedValue(value))
                    DumpObjectMembers(value, $"{label}.{field.Name}", depth + 1, maxNestedDepth);
            }

            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!ShouldDumpMember(property.Name) || property.GetIndexParameters().Length > 0)
                    continue;

                object value = null;
                try
                {
                    value = property.GetValue(instance);
                }
                catch (Exception ex)
                {
                    AICharacterPlugin.Log.LogWarning($"{label} property {property.Name} get failed: {ex.Message}");
                    continue;
                }

                AICharacterPlugin.Log.LogInfo($"{label} property {property.Name} type={(value?.GetType().FullName ?? "null")} value={FormatDumpValue(value)}");

                if (depth < maxNestedDepth && ShouldInspectNestedValue(value))
                    DumpObjectMembers(value, $"{label}.{property.Name}", depth + 1, maxNestedDepth);
            }
        }

        private static bool ShouldDumpMember(string memberName)
        {
            return memberName.IndexOf("id", StringComparison.OrdinalIgnoreCase) >= 0
                || memberName.IndexOf("info", StringComparison.OrdinalIgnoreCase) >= 0
                || memberName.IndexOf("data", StringComparison.OrdinalIgnoreCase) >= 0
                || memberName.IndexOf("brain", StringComparison.OrdinalIgnoreCase) >= 0
                || memberName.IndexOf("follower", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ShouldInspectNestedValue(object value)
        {
            if (value == null)
                return false;

            var type = value.GetType();
            return !type.IsPrimitive && type != typeof(string) && !type.IsEnum;
        }

        private static string FormatDumpValue(object value)
        {
            if (value == null)
                return "null";

            var type = value.GetType();
            if (type.IsPrimitive || type == typeof(string) || type.IsEnum)
                return value.ToString();

            return value.ToString();
        }

        internal static void DumpRecruitSource(UIFollowerIndoctrinationMenuController __instance)
        {
            if (__instance == null)
            {
                AICharacterPlugin.Log.LogWarning("DumpRecruitSource: __instance null.");
                return;
            }

            AICharacterPlugin.Log.LogInfo($"DumpRecruitSource: instance type={__instance.GetType().FullName}");

            try
            {
                var type = __instance.GetType();
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (field.Name.IndexOf("Follower", StringComparison.OrdinalIgnoreCase) >= 0 || field.Name.IndexOf("Recruit", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var value = field.GetValue(__instance);
                        AICharacterPlugin.Log.LogInfo($"DumpRecruitSource field {field.Name} type={(value?.GetType().FullName ?? "null")} value={value}");
                    }
                }

                foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (prop.Name.IndexOf("Follower", StringComparison.OrdinalIgnoreCase) >= 0 || prop.Name.IndexOf("Recruit", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        object value = null;
                        try
                        {
                            value = prop.GetValue(__instance);
                        }
                        catch (Exception ex)
                        {
                            AICharacterPlugin.Log.LogWarning($"DumpRecruitSource property {prop.Name} get failed: {ex.Message}");
                        }
                        AICharacterPlugin.Log.LogInfo($"DumpRecruitSource property {prop.Name} type={(value?.GetType().FullName ?? "null")} value={value}");
                    }
                }

                var go = __instance.gameObject;
                if (go != null)
                {
                    var recruitComponent = go.GetComponentInChildren<FollowerRecruit>(true);
                    if (recruitComponent != null)
                    {
                        AICharacterPlugin.Log.LogInfo($"DumpRecruitSource found FollowerRecruit component in children: {recruitComponent.GetType().FullName}");
                    }
                    else
                    {
                        AICharacterPlugin.Log.LogInfo("DumpRecruitSource did not find FollowerRecruit component in children.");
                    }
                }
            }
            catch (Exception ex)
            {
                AICharacterPlugin.Log.LogError($"DumpRecruitSource failed: {ex}");
            }
        }
    }
}

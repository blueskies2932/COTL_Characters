using System;
using System.Linq;
using System.Reflection;

namespace COTL_AL_NPCs
{
    internal static class FollowerAiNativeRoleTools
    {
        internal static bool TryClearVanillaFollowerRole(int followerID, out string message)
        {
            message = string.Empty;
            if (followerID < 0)
            {
                message = "Follower ID was invalid.";
                return false;
            }

            if (FollowerAIManager.IsNPC(followerID))
            {
                message = $"Follower {followerID} is a Mod NPC; vanilla role clear skipped.";
                return false;
            }

            var brain = FindBrainByID(followerID);
            if (brain == null)
            {
                message = $"Could not find brain for follower {followerID}.";
                return false;
            }

            var role = FollowerRole.Worshipper;
            var changed = 0;
            var notes = string.Empty;
            try
            {
                InvokeMethod(brain, "ClearPersonalOverrideTaskProvider");
                notes += "Cleared personal override. ";
            }
            catch (Exception ex)
            {
                notes += $"Could not clear personal override: {ex.Message}. ";
            }

            try
            {
                InvokeMethod(brain, "NewRoleSet", role);
                changed++;
            }
            catch (Exception ex)
            {
                notes += $"NewRoleSet(Worshipper) failed: {ex.Message}. ";
            }

            var info = GetMemberValue(brain, "Info") ?? GetMemberValue(brain, "_directInfoAccess");
            if (TrySetMemberValue(info, "FollowerRole", role))
                changed++;
            if (TrySetMemberValue(brain, "FollowerRole", role))
                changed++;

            try
            {
                InvokeMethod(brain, "CheckChangeTask");
                notes += "Asked native scheduler to reconsider tasks.";
            }
            catch (Exception ex)
            {
                notes += $"CheckChangeTask failed: {ex.Message}.";
            }

            var afterRole = Convert.ToString(GetMemberValue(info, "FollowerRole") ?? GetMemberValue(brain, "FollowerRole")) ?? string.Empty;
            if (string.Equals(afterRole, nameof(FollowerRole.Worshipper), StringComparison.OrdinalIgnoreCase))
            {
                message = $"Cleared vanilla follower {followerID} role to Worshipper. changed={changed}; {notes}".Trim();
                return true;
            }

            message = $"Attempted vanilla follower {followerID} role clear to Worshipper, but role is now `{afterRole}`. changed={changed}; {notes}".Trim();
            return false;
        }

        internal static bool TryGetFollowerIDFromFollower(Follower follower, out int followerID)
        {
            followerID = -1;
            var info = (object)follower?.Brain?._directInfoAccess ?? follower?.Brain?.Info;
            if (info == null)
                return false;

            var idValue = GetMemberValue(info, "ID");
            if (idValue is int id)
                followerID = id;
            return followerID >= 0;
        }

        internal static bool TryGetFollowerIDFromBrain(object brain, out int followerID)
        {
            followerID = -1;
            var info = GetMemberValue(brain, "Info") ?? GetMemberValue(brain, "_directInfoAccess");
            var idValue = GetMemberValue(info, "ID");
            if (idValue is int id)
            {
                followerID = id;
                return followerID >= 0;
            }

            return idValue != null && int.TryParse(idValue.ToString(), out followerID) && followerID >= 0;
        }

        private static object FindBrainByID(int followerID)
        {
            var brainType = FindType("FollowerBrain");
            var method = brainType?.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(candidate => candidate.Name == "FindBrainByID" && candidate.GetParameters().Length == 1);
            return method?.Invoke(null, new object[] { followerID });
        }

        private static Type FindType(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly =>
                {
                    try
                    {
                        return assembly.GetType(name, false) ??
                               assembly.GetTypes().FirstOrDefault(type => string.Equals(type.Name, name, StringComparison.Ordinal));
                    }
                    catch
                    {
                        return null;
                    }
                })
                .FirstOrDefault(type => type != null);
        }

        private static object GetMemberValue(object instance, string memberName)
        {
            if (instance == null || string.IsNullOrWhiteSpace(memberName))
                return null;

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = instance.GetType();
            var field = type.GetField(memberName, flags);
            if (field != null)
                return field.GetValue(instance);

            var property = type.GetProperty(memberName, flags);
            return property != null && property.GetIndexParameters().Length == 0
                ? property.GetValue(instance, null)
                : null;
        }

        private static bool TrySetMemberValue(object instance, string memberName, object value)
        {
            if (instance == null || value == null || string.IsNullOrWhiteSpace(memberName))
                return false;

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = instance.GetType();
            var field = type.GetField(memberName, flags);
            if (field != null && field.FieldType.IsInstanceOfType(value))
            {
                field.SetValue(instance, value);
                return true;
            }

            var property = type.GetProperty(memberName, flags);
            if (property != null && property.CanWrite && property.PropertyType.IsInstanceOfType(value))
            {
                property.SetValue(instance, value, null);
                return true;
            }

            return false;
        }

        private static object InvokeMethod(object instance, string methodName, params object[] args)
        {
            if (instance == null)
                throw new MissingMethodException("null", methodName);

            var method = instance.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(candidate => candidate.Name == methodName && candidate.GetParameters().Length == (args?.Length ?? 0));
            if (method == null)
                throw new MissingMethodException(instance.GetType().FullName, methodName);

            try
            {
                return method.Invoke(instance, args ?? new object[0]);
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException ?? ex;
            }
        }
    }
}

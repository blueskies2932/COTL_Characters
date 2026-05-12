using System.Reflection;

namespace COTL_AL_NPCs
{
    internal static class FollowerAiInteractionReflection
    {
        internal static object GetMemberValue(object instance, string memberName)
        {
            if (instance == null)
                return null;

            var type = instance.GetType();
            var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
                return field.GetValue(instance);

            var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.GetIndexParameters().Length == 0)
                return property.GetValue(instance);

            return null;
        }
    }
}

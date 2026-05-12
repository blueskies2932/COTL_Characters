using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace COTL_AL_NPCs
{
    internal static class FollowerAiCultTraitProfile
    {
        internal static string Build()
        {
            try
            {
                var traits = Enum.GetValues(typeof(DoctrineUpgradeSystem.DoctrineType))
                    .Cast<DoctrineUpgradeSystem.DoctrineType>()
                    .Where(type => type != DoctrineUpgradeSystem.DoctrineType.None)
                    .Where(IsUnlockedCultTrait)
                    .Select(FormatDoctrineName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .Take(16)
                    .ToList();

                return traits.Count == 0
                    ? "none"
                    : string.Join(", ", traits);
            }
            catch (Exception ex)
            {
                AICharacterPlugin.LogInfoVerbose($"Could not build cult trait profile: {ex.Message}");
                return "unknown";
            }
        }

        private static bool IsUnlockedCultTrait(DoctrineUpgradeSystem.DoctrineType type)
        {
            try
            {
                return DoctrineUpgradeSystem.GetUnlocked(type) && IsTraitUnlock(type);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsTraitUnlock(DoctrineUpgradeSystem.DoctrineType type)
        {
            var method = typeof(DoctrineUpgradeSystem).GetMethod(
                "GetUnlockType",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null)
                return type.ToString().IndexOf("Trait", StringComparison.OrdinalIgnoreCase) >= 0;

            var value = method.Invoke(null, new object[] { type });
            return string.Equals(Convert.ToString(value), "Trait", StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatDoctrineName(DoctrineUpgradeSystem.DoctrineType type)
        {
            try
            {
                var localized = DoctrineUpgradeSystem.GetLocalizedName(type);
                if (!string.IsNullOrWhiteSpace(localized))
                    return localized.Trim();
            }
            catch
            {
            }

            return type.ToString();
        }
    }
}

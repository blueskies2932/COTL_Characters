using System;
using System.Collections.Generic;
using System.Linq;

namespace COTL_AL_NPCs
{
    internal static class FollowerTraitVoiceProfile
    {
        internal static string Build(FollowerAiFollowerFact follower)
        {
            if (follower == null)
                return "No follower profile available.";

            var parts = new List<string>
            {
                BuildPersonal(follower),
                BuildCult()
            };

            return string.Join("; ", parts);
        }

        internal static string BuildPersonal(FollowerAiFollowerFact follower)
        {
            if (follower == null)
                return "current_traits=[unavailable]";

            return $"current_traits=[{FormatTraits(follower.Traits)}]";
        }

        internal static string BuildCult()
        {
            return $"cult_traits_secondary=[{FollowerAiCultTraitProfile.Build()}]";
        }

        private static string FormatTraits(List<FollowerAiTraitFact> traits)
        {
            if (traits == null || traits.Count == 0)
                return "none";

            return string.Join(", ", traits.Select(trait =>
                string.IsNullOrWhiteSpace(trait.Title) ? trait.Name : trait.Title));
        }

    }
}

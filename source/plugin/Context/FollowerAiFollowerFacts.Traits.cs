using System;
using System.Collections.Generic;
using System.Linq;
using Lamb.UI.FollowerSelect;

namespace COTL_AL_NPCs
{
    internal static partial class FollowerAiFollowerFacts
    {
        internal static List<FollowerAiFollowerFact> FindByTrait(FollowerTrait.TraitType trait, bool availableOnly = true)
        {
            return GetCurrentFollowers()
                .Where(fact => (!availableOnly || fact.AvailabilityStatus == FollowerSelectEntry.Status.Available) && fact.HasTrait(trait))
                .OrderBy(fact => fact.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(fact => fact.ID)
                .ToList();
        }

        internal static bool TryFindTrait(string query, out FollowerTrait.TraitType trait)
        {
            trait = FollowerTrait.TraitType.None;
            var needle = FollowerAiTextNormalize.CompactLettersAndDigits(query);
            if (string.IsNullOrWhiteSpace(needle))
                return false;

            foreach (FollowerTrait.TraitType candidate in Enum.GetValues(typeof(FollowerTrait.TraitType)))
            {
                if (candidate == FollowerTrait.TraitType.None)
                    continue;

                if (FollowerAiTextNormalize.CompactLettersAndDigits(candidate.ToString()) == needle ||
                    FollowerAiTextNormalize.CompactLettersAndDigits(TryGetTraitTitle(candidate)) == needle)
                {
                    trait = candidate;
                    return true;
                }
            }

            return false;
        }

        internal static string GetTraitDisplayName(FollowerTrait.TraitType trait)
        {
            var title = TryGetTraitTitle(trait);
            return string.IsNullOrWhiteSpace(title) ? trait.ToString() : title;
        }

        internal static string BuildTraitListReport(FollowerTrait.TraitType trait)
        {
            var matches = FindByTrait(trait);
            var label = GetTraitDisplayName(trait);

            if (matches.Count == 0)
                return $"No current available followers have trait {trait}/{label}.";

            return $"{matches.Count} current available follower(s) have trait {trait}/{label}: " +
                   string.Join("; ", matches.Select(FormatCompactFact));
        }

        private static List<FollowerAiTraitFact> BuildTraitFacts(FollowerInfo info)
        {
            if (info?.Traits == null || info.Traits.Count == 0)
                return new List<FollowerAiTraitFact>();

            return info.Traits
                .Where(trait => trait != FollowerTrait.TraitType.None)
                .Select(trait => new FollowerAiTraitFact
                {
                    Type = trait,
                    Name = trait.ToString(),
                    Title = TryGetTraitTitle(trait),
                    IsPositive = TryGetTraitSentiment(trait)
                })
                .ToList();
        }

        private static bool TryFindTraitInText(string text, out FollowerTrait.TraitType trait)
        {
            trait = FollowerTrait.TraitType.None;
            var needle = FollowerAiTextNormalize.CompactLettersAndDigits(text);
            if (string.IsNullOrWhiteSpace(needle))
                return false;

            var bestLength = 0;
            foreach (FollowerTrait.TraitType candidate in Enum.GetValues(typeof(FollowerTrait.TraitType)))
            {
                if (candidate == FollowerTrait.TraitType.None)
                    continue;

                foreach (var name in new[] { candidate.ToString(), TryGetTraitTitle(candidate) })
                {
                    var normalizedName = FollowerAiTextNormalize.CompactLettersAndDigits(name);
                    if (string.IsNullOrWhiteSpace(normalizedName) || normalizedName.Length <= bestLength)
                        continue;

                    if (!needle.Contains(normalizedName))
                        continue;

                    trait = candidate;
                    bestLength = normalizedName.Length;
                }
            }

            return trait != FollowerTrait.TraitType.None;
        }

        private static string TryGetTraitTitle(FollowerTrait.TraitType trait)
        {
            try
            {
                return FollowerTrait.GetLocalizedTitle(trait);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool? TryGetTraitSentiment(FollowerTrait.TraitType trait)
        {
            try
            {
                return FollowerTrait.IsPositiveTrait(trait);
            }
            catch
            {
                return null;
            }
        }
    }
}

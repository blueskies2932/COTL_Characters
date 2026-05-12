using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Lamb.UI.FollowerSelect;

namespace COTL_AL_NPCs
{
    internal static partial class FollowerAiFollowerFacts
    {
        internal static string BuildFollowerObservation(string payload, string playerText)
        {
            var capturedUtc = DateTime.UtcNow;
            var facts = BuildFactsFromLiveFollowers();
            TryWriteCurrentFollowerFactsReport(facts, capturedUtc, "mod-side current follower facts provider");

            var builder = new StringBuilder();
            builder.AppendLine("current_follower_facts result:");
            builder.AppendLine($"- Captured UTC: {capturedUtc:O}");
            builder.AppendLine("- Source: fresh live Follower.Followers snapshot through the mod-side follower facts provider.");
            builder.AppendLine("- Mutation: none; this is read-only roster perception.");
            builder.AppendLine($"- Current follower count: {facts.Count}");

            if (TryFindTraitInText($"{payload} {playerText}", out var requestedTrait))
            {
                var title = TryGetTraitTitle(requestedTrait);
                var label = string.IsNullOrWhiteSpace(title) ? requestedTrait.ToString() : title;
                var matches = facts
                    .Where(fact => fact.AvailabilityStatus == FollowerSelectEntry.Status.Available && fact.HasTrait(requestedTrait))
                    .OrderBy(fact => fact.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(fact => fact.ID)
                    .ToList();
                builder.AppendLine($"- Matched trait query: {requestedTrait}/{label}; available follower count={matches.Count}; names={FormatNameList(matches)}");
            }

            builder.AppendLine("- Follower rows:");
            foreach (var fact in facts.OrderBy(fact => fact.Name, StringComparer.OrdinalIgnoreCase).ThenBy(fact => fact.ID))
            {
                builder.AppendLine(
                    $"  - id={fact.ID}; name={fact.Name}; status={fact.AvailabilityStatus}; role={fact.Role}; " +
                    $"level={fact.Level}; age={fact.Age}; member_days={fact.MemberDays}; old={fact.OldAge}; ai_npc={fact.IsAiNpc}; " +
                    $"location={fact.Location}; desired_location={fact.DesiredLocation}; state={fact.CurrentState}; " +
                    $"task={fact.CurrentTask}; override_task={fact.CurrentOverrideTask}; faith={FormatFloat(fact.Faith)}; " +
                    $"illness={FormatFloat(fact.Illness)}; dissent={FormatFloat(fact.Dissent)}; satiation={FormatFloat(fact.Satiation)}; " +
                    $"starvation={FormatFloat(fact.Starvation)}; exhaustion={FormatFloat(fact.Exhaustion)}; rest={FormatFloat(fact.Rest)}; " +
                    $"drunk={FormatFloat(fact.Drunk)}; bathroom={FormatFloat(fact.Bathroom)}; social={FormatFloat(fact.Social)}; " +
                    $"pleasure={FormatInt(fact.Pleasure)}/{FormatInt(fact.TotalPleasure)}; necklace={fact.Necklace}; " +
                    $"clothing={fact.Clothing}; hat={fact.Hat}; cult_follower_traits=[{FormatTraitList(fact)}]");
            }

            builder.AppendLine("- Trait index:");
            foreach (var group in facts
                .SelectMany(fact => fact.Traits.Select(trait => new { Trait = trait, Fact = fact }))
                .GroupBy(pair => pair.Trait.Type)
                .OrderBy(group => group.Key.ToString(), StringComparer.OrdinalIgnoreCase))
            {
                var first = group.First().Trait;
                builder.AppendLine($"  - {first.Name}/{first.Title}: {group.Count()} follower(s): {string.Join(", ", group.Select(pair => pair.Fact.Name).OrderBy(name => name, StringComparer.OrdinalIgnoreCase))}");
            }

            return builder.ToString();
        }

        internal static string BuildCurrentFollowerFactsReport(List<FollowerAiFollowerFact> facts, DateTime capturedUtc, string source)
        {
            var safeFacts = facts ?? new List<FollowerAiFollowerFact>();
            var builder = new StringBuilder();
            builder.AppendLine("# Current Follower Facts Report");
            builder.AppendLine();
            builder.AppendLine($"Captured UTC: {capturedUtc:O}");
            builder.AppendLine($"Source: {source}");
            builder.AppendLine($"Follower count: {safeFacts.Count}");
            builder.AppendLine();
            builder.AppendLine("This report is built from the live follower facts provider. It is intended as a verification artifact for the internal AI follower facts provider, not an AI-facing action menu.");
            builder.AppendLine();
            builder.AppendLine("## Followers");
            foreach (var fact in safeFacts.OrderBy(fact => fact.Name, StringComparer.OrdinalIgnoreCase).ThenBy(fact => fact.ID))
            {
                builder.AppendLine(
                    $"- id={fact.ID}; name={fact.Name}; status={fact.AvailabilityStatus}; role={fact.Role}; " +
                    $"location={fact.Location}; desired_location={fact.DesiredLocation}; state={fact.CurrentState}; " +
                    $"task={fact.CurrentTask}; override_task={fact.CurrentOverrideTask}; level={fact.Level}; age={fact.Age}; " +
                    $"member_days={fact.MemberDays}; old={fact.OldAge}; ai_npc={fact.IsAiNpc}; faith={FormatFloat(fact.Faith)}; happiness={FormatFloat(fact.Happiness)}; " +
                    $"illness={FormatFloat(fact.Illness)}; dissent={FormatFloat(fact.Dissent)}; satiation={FormatFloat(fact.Satiation)}; " +
                    $"starvation={FormatFloat(fact.Starvation)}; exhaustion={FormatFloat(fact.Exhaustion)}; rest={FormatFloat(fact.Rest)}; " +
                    $"drunk={FormatFloat(fact.Drunk)}; bathroom={FormatFloat(fact.Bathroom)}; reeducation={FormatFloat(fact.Reeducation)}; " +
                    $"social={FormatFloat(fact.Social)}; pleasure={FormatInt(fact.Pleasure)}/{FormatInt(fact.TotalPleasure)}; " +
                    $"necklace={fact.Necklace}; showing_necklace={fact.ShowingNecklace}; cursed_state={fact.CursedState}; " +
                    $"special={fact.Special}; clothing={fact.Clothing}; outfit={fact.Outfit}; hat={fact.Hat}; cult_follower_traits=[{FormatTraitList(fact)}]");
            }

            builder.AppendLine();
            builder.AppendLine("## Trait Index");
            foreach (var group in safeFacts
                .SelectMany(fact => fact.Traits.Select(trait => new { Trait = trait, Fact = fact }))
                .GroupBy(pair => pair.Trait.Type)
                .OrderBy(group => group.Key.ToString(), StringComparer.OrdinalIgnoreCase))
            {
                var first = group.First().Trait;
                builder.AppendLine($"- {first.Name}/{first.Title}: {group.Count()} follower(s): {string.Join(", ", group.Select(pair => pair.Fact.Name).OrderBy(name => name, StringComparer.OrdinalIgnoreCase))}");
            }

            return builder.ToString();
        }

        private static void TryWriteCurrentFollowerFactsReport(List<FollowerAiFollowerFact> facts, DateTime capturedUtc, string source)
        {
            try
            {
                var directory = Path.Combine(BepInEx.Paths.ConfigPath, "COTL_AL_NPCs");
                Directory.CreateDirectory(directory);
                File.WriteAllText(
                    Path.Combine(directory, "CurrentFollowerFactsReport.txt"),
                    BuildCurrentFollowerFactsReport(facts, capturedUtc, source),
                    Encoding.UTF8);
            }
            catch (Exception ex)
            {
                AICharacterPlugin.Log.LogWarning($"AI follower facts report write failed: {ex.Message}");
            }
        }

        private static string FormatCompactFact(FollowerAiFollowerFact fact)
        {
            return $"{fact.Name} (id={fact.ID}, level={fact.Level}, age={fact.Age}, old={fact.OldAge}, role={fact.Role})";
        }

        private static string FormatNameList(List<FollowerAiFollowerFact> facts)
        {
            return facts == null || facts.Count == 0
                ? "none"
                : string.Join(", ", facts.Select(fact => fact.Name));
        }

        private static string FormatFloat(float value)
        {
            return value >= 0f ? value.ToString("0.00") : "unknown";
        }

        private static string FormatInt(int value)
        {
            return value >= 0 ? value.ToString() : "unknown";
        }

        private static string FormatTraitList(FollowerAiFollowerFact fact)
        {
            return fact.Traits.Count == 0 ? "none" : string.Join(",", fact.Traits.Select(trait => trait.ToString()));
        }
    }
}

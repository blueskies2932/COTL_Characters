using System.Collections.Generic;
using System.Linq;
using Lamb.UI.FollowerSelect;

namespace COTL_AL_NPCs
{
    internal sealed class FollowerAiTraitFact
    {
        public FollowerTrait.TraitType Type;
        public string Name;
        public string Title;
        public bool? IsPositive;

        public override string ToString()
        {
            var title = string.IsNullOrWhiteSpace(Title) ? Name : Title;
            var sentiment = IsPositive.HasValue ? (IsPositive.Value ? "positive" : "negative") : "unknown";
            return $"{Name}/{title}:{sentiment}";
        }
    }

    internal sealed class FollowerAiFollowerFact
    {
        public int ID;
        public string Name;
        public FollowerRole Role;
        public FollowerLocation Location;
        public FollowerLocation DesiredLocation;
        public FollowerSelectEntry.Status AvailabilityStatus;
        public FollowerState CurrentState;
        public FollowerTaskType CurrentTask;
        public FollowerTaskType CurrentOverrideTask;
        public int Age;
        public int MemberDays;
        public int Level;
        public bool OldAge;
        public InventoryItem.ITEM_TYPE Necklace;
        public bool ShowingNecklace;
        public Thought CursedState;
        public FollowerSpecialType Special;
        public FollowerClothingType Clothing;
        public FollowerOutfitType Outfit;
        public FollowerHatType Hat;
        public float Faith = -1f;
        public float Happiness = -1f;
        public float Illness = -1f;
        public float Dissent = -1f;
        public float Satiation = -1f;
        public float Starvation = -1f;
        public float Exhaustion = -1f;
        public float Rest = -1f;
        public float Drunk = -1f;
        public float Bathroom = -1f;
        public float Reeducation = -1f;
        public float Social = -1f;
        public int Pleasure = -1;
        public int TotalPleasure = -1;
        public List<FollowerAiTraitFact> Traits = new List<FollowerAiTraitFact>();
        public bool IsAiNpc;

        public bool HasTrait(FollowerTrait.TraitType trait)
        {
            return Traits.Any(current => current.Type == trait);
        }

        public override string ToString()
        {
            var traits = Traits.Count == 0 ? "none" : string.Join(",", Traits.Select(trait => trait.Name));
            return $"id={ID} name={Name} status={AvailabilityStatus} role={Role} location={Location} level={Level} age={Age} old={OldAge} ai_npc={IsAiNpc} traits={traits}";
        }
    }
}

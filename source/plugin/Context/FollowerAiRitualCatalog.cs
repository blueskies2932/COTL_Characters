namespace COTL_AL_NPCs
{
    internal static class FollowerAiRitualCatalog
    {
        internal static string GetPlayerFacingName(UpgradeSystem.Type ritualType)
        {
            switch (ritualType.ToString())
            {
                case "Ritual_AlmsToPoor":
                    return "Alms for the Poor";
                case "Ritual_AssignFaithEnforcer":
                    return "Loyalty Enforcer";
                case "Ritual_AssignTaxCollector":
                    return "Tax Enforcer";
                case "Ritual_Brainwashing":
                    return "Brainwashing Ritual";
                case "Ritual_Cannibal":
                    return "Gluttony of Cannibals";
                case "Ritual_ConsumeFollower":
                    return "Consume Follower";
                case "Ritual_DonationRitual":
                    return "Ritual of Enrichment";
                case "Ritual_DrinkingFestival":
                    return "Drinking Festival";
                case "Ritual_Enlightenment":
                    return "Ritual of Enlightenment";
                case "Ritual_Fast":
                    return "Ritual Fast";
                case "Ritual_FasterBuilding":
                    return "The Glory of Construction";
                case "Ritual_Feast":
                    return "Feasting Ritual";
                case "Ritual_Fightpit":
                    return "Fight Pit Ritual";
                case "Ritual_FishingRitual":
                    return "Ritual of the Ocean's Bounty";
                case "Ritual_FollowerWedding":
                    return "Follower Wedding";
                case "Ritual_Funeral":
                    return "Funeral";
                case "Ritual_HarvestRitual":
                    return "Ritual of the Harvest";
                case "Ritual_Holiday":
                    return "Holy Day Ritual";
                case "Ritual_RanchHarvest":
                    return "Ritual of Shearing";
                case "Ritual_RanchMeat":
                    return "Ritual of Gorging";
                case "Ritual_Ressurect":
                    return "Ritual of Resurrection";
                case "Ritual_Sacrifice":
                    return "Sacrifice of the Flesh";
                case "Ritual_Snowman":
                    return "Life to the Ice";
                case "Ritual_Wedding":
                    return "Wedding";
                case "Ritual_WorkThroughNight":
                    return "Glory Through Toil";
                default:
                    return "Unknown Ritual";
            }
        }

        internal static string GetDescription(string ritualName)
        {
            switch (ritualName)
            {
                case "Alms for the Poor":
                    return "Distributes coins to all followers, increases Loyalty, and gives faith.";
                case "Loyalty Enforcer":
                    return "Appoints a follower to patrol the cult and raise follower Loyalty.";
                case "Tax Enforcer":
                    return "Appoints a follower to collect coins from other cult members.";
                case "Brainwashing Ritual":
                    return "Brainwashes followers and locks Faith at full for two days.";
                case "Gluttony of Cannibals":
                    return "Followers feast upon a chosen follower, generating Sin and lowering Faith.";
                case "Consume Follower":
                    return "Allows the Crown to consume a follower in return for Divine Inspiration.";
                case "Ritual of Enrichment":
                    return "All followers donate coins to the Lamb.";
                case "Drinking Festival":
                    return "filler text";
                case "Ritual of Enlightenment":
                    return "Temporarily increases devotion generation speed at the shrine.";
                case "Ritual Fast":
                    return "Followers do not need to eat for three days.";
                case "The Glory of Construction":
                    return "Instantly builds all structures currently under construction.";
                case "Feasting Ritual":
                    return "Throws a feast that fills cult hunger and gives faith.";
                case "Fight Pit Ritual":
                    return "Commands two followers to fight, with the Lamb deciding whether to show mercy.";
                case "Ritual of the Ocean's Bounty":
                    return "For two days, catches double fish and makes special fish more common.";
                case "Follower Wedding":
                    return "Allows two followers to be joined together in marriage.";
                case "Funeral":
                    return "Conducts a funeral for a recently dead follower and gives faith.";
                case "Ritual of the Harvest":
                    return "Makes all sown farm plots immediately ready for harvest.";
                case "Holy Day Ritual":
                    return "Followers do not work for a day and gain faith.";
                case "Ritual of Shearing":
                    return "Doubles resources harvested from ranch animals for one day.";
                case "Ritual of Gorging":
                    return "Doubles meat from butchered ranch animals for one day.";
                case "Ritual of Resurrection":
                    return "Brings a dead follower back to life.";
                case "Sacrifice of the Flesh":
                    return "Sacrifices a follower to grow the Lamb's strength.";
                case "Life to the Ice":
                    return "Temporarily gives Snowlambs life, allowing them to work as followers.";
                case "Wedding":
                    return "Marries one of the Lamb's followers and gives faith.";
                case "Glory Through Toil":
                    return "Followers work through three days and nights without getting tired.";
                default:
                    return "filler text";
            }
        }
    }
}

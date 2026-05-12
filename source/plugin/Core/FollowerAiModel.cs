using System;
using System.Collections.Generic;

namespace COTL_AL_NPCs
{
    public enum FollowerPersonality
    {
        LoyalProtector,
        SarcasticSkeptic,
        DevoutFollower,
        CuriousScholar,
        PlayfulTrickster
    }

    public enum FollowerAiMode
    {
        Vanilla,
        Character
    }

    public class FollowerAI
    {
        public int FollowerID { get; set; }
        public bool IsNPC { get; set; }
        public FollowerAiMode Mode { get; set; }
        public FollowerPersonality Personality { get; set; }
        public List<string> ConversationHistory { get; set; } = new List<string>();
        public List<string> OutcomeMemory { get; set; } = new List<string>();
        public DateTime LastInteraction { get; set; }

        public FollowerAI()
        {
            Mode = FollowerAiMode.Vanilla;
            Personality = FollowerPersonality.DevoutFollower;
            LastInteraction = DateTime.Now;
        }

        public FollowerAI(int followerID)
        {
            FollowerID = followerID;
            IsNPC = false;
            Mode = FollowerAiMode.Vanilla;
            Personality = FollowerPersonality.DevoutFollower;
            LastInteraction = DateTime.Now;
        }
    }
}

using System;
using System.Collections.Generic;

namespace GameDemo.Rogue
{
    [Serializable]
    public sealed class RogueNodeResult
    {
        public bool success = true;
        public int hopeDelta;
        public int ingotsDelta;
        public int healthDelta;
        public int maxHealthDelta;
        public int keysDelta;
        public int commandExpDelta;
        public List<string> flagsSet = new List<string>();
        public List<string> curiosAdded = new List<string>();
        public List<RogueReward> rewards = new List<RogueReward>();
    }
}

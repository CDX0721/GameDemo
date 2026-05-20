using System;
using System.Collections.Generic;

namespace GameDemo.Rogue
{
    public enum RogueRunStatus
    {
        NotStarted,
        InProgress,
        Success,
        Failed
    }

    [Serializable]
    public sealed class RogueRunState
    {
        public string runId;
        public int version;
        public int seed;
        public RogueRunStatus status = RogueRunStatus.NotStarted;
        public int currentFloor;
        public string currentNodeId;
        public int hope;
        public int ingots;
        public int commandExp;
        public int health;
        public int maxHealth;
        public int keys;
        public int potentialPoints;
        public int totalRefreshCount;
        public int rewardPityRare;
        public int rewardPityLegend;
        public List<string> visitedNodeIds = new List<string>();
        public List<string> curios = new List<string>();
        public List<string> flags = new List<string>();
        public List<RogueRewardRecord> rewards = new List<RogueRewardRecord>();

        public bool IsRunning => status == RogueRunStatus.InProgress;

        public void ClampHealth()
        {
            if (maxHealth < 0)
            {
                maxHealth = 0;
            }

            if (health > maxHealth)
            {
                health = maxHealth;
            }

            if (health < 0)
            {
                health = 0;
            }

            if (hope < 0) hope = 0;
            if (ingots < 0) ingots = 0;
            if (keys < 0) keys = 0;
        }
    }
}

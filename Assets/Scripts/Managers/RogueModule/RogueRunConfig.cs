using System;
using System.Collections.Generic;

namespace GameDemo.Rogue
{
    [Serializable]
    public sealed class RogueNodeWeight
    {
        public RogueNodeType type = RogueNodeType.Battle;
        public int weight = 1;
    }

    [Serializable]
    public sealed class RogueContentPool
    {
        public List<string> battleEncounters = new List<string>();
        public List<string> eliteEncounters = new List<string>();
        public List<string> midBossEncounters = new List<string>();
        public List<string> bossEncounters = new List<string>();
        public List<string> events = new List<string>();
        public List<string> shops = new List<string>();
        public List<string> rests = new List<string>();

        public string PickContent(RogueNodeType type, Random rng)
        {
            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            List<string> pool = GetPool(type);
            if (pool == null || pool.Count == 0)
            {
                return string.Empty;
            }

            return pool[rng.Next(pool.Count)];
        }

        List<string> GetPool(RogueNodeType type)
        {
            switch (type)
            {
                case RogueNodeType.Battle:
                    return battleEncounters;
                case RogueNodeType.Elite:
                    return eliteEncounters;
                case RogueNodeType.MidBoss:
                    return midBossEncounters;
                case RogueNodeType.Boss:
                    return bossEncounters;
                case RogueNodeType.Event:
                    return events;
                case RogueNodeType.Shop:
                    return shops;
                case RogueNodeType.Rest:
                    return rests;
                default:
                    return battleEncounters;
            }
        }
    }

    [Serializable]
    public sealed class RogueFloorNodeWeight
    {
        public RogueNodeType type = RogueNodeType.Battle;
        public int weight = 1;
    }

    [Serializable]
    public sealed class RogueFloorRule
    {
        public int floor;
        public int nodeCount;
        public int maxBranch;
        public int maxShop;
        public int maxRest;
        public bool forceOneShop;
        public bool noRest;
        public bool includeMidBoss;
        public bool includeFinalBoss;
        public bool hidden;
        public List<RogueFloorNodeWeight> weights = new List<RogueFloorNodeWeight>();
    }

    [Serializable]
    public sealed class RogueRunConfig
    {
        public int seed;
        public int floors = 5;
        public int maxNodeRetries = 16;
        public int fallbackNodeCount = 3;
        public int maxConnectionsPerNode = 2;
        public int startingHope = 10;
        public int startingIngots = 5;
        public int startingHealth = 3;
        public int startingMaxHealth = 3;
        public int runVersion = 1;
        public List<RogueNodeWeight> nodeWeights = new List<RogueNodeWeight>();
        public List<RogueFloorRule> floorRules = new List<RogueFloorRule>();
        public RogueContentPool contentPool = new RogueContentPool();

        public List<RogueNodeWeight> GetNodeWeightsOrDefault()
        {
            if (nodeWeights != null && nodeWeights.Count > 0)
            {
                return nodeWeights;
            }

            return new List<RogueNodeWeight>
            {
                new RogueNodeWeight { type = RogueNodeType.Battle, weight = 60 },
                new RogueNodeWeight { type = RogueNodeType.Event, weight = 15 },
                new RogueNodeWeight { type = RogueNodeType.Shop, weight = 10 },
                new RogueNodeWeight { type = RogueNodeType.Rest, weight = 10 },
                new RogueNodeWeight { type = RogueNodeType.Elite, weight = 5 },
            };
        }

        public RogueContentPool GetContentPoolOrDefault()
        {
            return contentPool ?? new RogueContentPool();
        }

        public IReadOnlyList<RogueFloorRule> GetFloorRulesOrDefault()
        {
            if (floorRules != null && floorRules.Count > 0)
            {
                return floorRules;
            }

            return new List<RogueFloorRule>
            {
                new RogueFloorRule
                {
                    floor = 1,
                    nodeCount = 3,
                    maxBranch = 2,
                    maxShop = 1,
                    maxRest = 1,
                    forceOneShop = true,
                    weights = new List<RogueFloorNodeWeight>
                    {
                        new RogueFloorNodeWeight { type = RogueNodeType.Battle, weight = 55 },
                        new RogueFloorNodeWeight { type = RogueNodeType.Elite, weight = 5 },
                        new RogueFloorNodeWeight { type = RogueNodeType.Event, weight = 20 },
                        new RogueFloorNodeWeight { type = RogueNodeType.Shop, weight = 10 },
                        new RogueFloorNodeWeight { type = RogueNodeType.Rest, weight = 10 },
                    }
                },
                new RogueFloorRule
                {
                    floor = 2,
                    nodeCount = 3,
                    maxBranch = 2,
                    maxShop = 1,
                    maxRest = 1,
                    forceOneShop = true,
                    weights = new List<RogueFloorNodeWeight>
                    {
                        new RogueFloorNodeWeight { type = RogueNodeType.Battle, weight = 45 },
                        new RogueFloorNodeWeight { type = RogueNodeType.Elite, weight = 10 },
                        new RogueFloorNodeWeight { type = RogueNodeType.Event, weight = 20 },
                        new RogueFloorNodeWeight { type = RogueNodeType.Shop, weight = 15 },
                        new RogueFloorNodeWeight { type = RogueNodeType.Rest, weight = 10 },
                    }
                },
                new RogueFloorRule
                {
                    floor = 3,
                    nodeCount = 4,
                    maxBranch = 3,
                    maxShop = 2,
                    maxRest = 1,
                    includeMidBoss = true,
                    weights = new List<RogueFloorNodeWeight>
                    {
                        new RogueFloorNodeWeight { type = RogueNodeType.Battle, weight = 35 },
                        new RogueFloorNodeWeight { type = RogueNodeType.Elite, weight = 15 },
                        new RogueFloorNodeWeight { type = RogueNodeType.Event, weight = 15 },
                        new RogueFloorNodeWeight { type = RogueNodeType.Shop, weight = 20 },
                        new RogueFloorNodeWeight { type = RogueNodeType.Rest, weight = 5 },
                    }
                },
                new RogueFloorRule
                {
                    floor = 4,
                    nodeCount = 4,
                    maxBranch = 3,
                    maxShop = 2,
                    maxRest = 0,
                    noRest = true,
                    weights = new List<RogueFloorNodeWeight>
                    {
                        new RogueFloorNodeWeight { type = RogueNodeType.Battle, weight = 40 },
                        new RogueFloorNodeWeight { type = RogueNodeType.Elite, weight = 20 },
                        new RogueFloorNodeWeight { type = RogueNodeType.Event, weight = 10 },
                        new RogueFloorNodeWeight { type = RogueNodeType.Shop, weight = 20 },
                    }
                },
                new RogueFloorRule
                {
                    floor = 5,
                    nodeCount = 3,
                    maxBranch = 2,
                    maxShop = 0,
                    maxRest = 0,
                    noRest = true,
                    includeFinalBoss = true,
                    weights = new List<RogueFloorNodeWeight>()
                },
                new RogueFloorRule
                {
                    floor = 6,
                    nodeCount = 3,
                    maxBranch = 1,
                    maxShop = 0,
                    maxRest = 0,
                    noRest = true,
                    includeFinalBoss = true,
                    hidden = true,
                    weights = new List<RogueFloorNodeWeight>
                    {
                        new RogueFloorNodeWeight { type = RogueNodeType.Battle, weight = 60 },
                        new RogueFloorNodeWeight { type = RogueNodeType.Event, weight = 20 },
                        new RogueFloorNodeWeight { type = RogueNodeType.Elite, weight = 20 }
                    }
                }
            };
        }
    }
}

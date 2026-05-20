using System;
using System.Collections.Generic;

namespace GameDemo.Rogue
{
    public enum RogueRewardRarity
    {
        Common,
        Rare,
        Epic,
        Legendary
    }

    [Serializable]
    public sealed class RogueRewardPoolEntry
    {
        public string id;
        public RogueRewardType type;
        public RogueRewardRarity rarity = RogueRewardRarity.Common;
        public int weight = 1;
        public int amount = 1;
        public int floorMin = 1;
        public int floorMax = 6;
        public bool unique;
    }

    [Serializable]
    public sealed class RogueRewardPoolConfig
    {
        public List<RogueRewardPoolEntry> entries = new List<RogueRewardPoolEntry>();
    }

    public static class RogueRewardRoller
    {
        static readonly List<RogueRewardPoolEntry> DefaultEntries = new List<RogueRewardPoolEntry>
        {
            new RogueRewardPoolEntry { id = "reward_ingot_10", type = RogueRewardType.Gold, rarity = RogueRewardRarity.Common, weight = 40, amount = 10, floorMin = 1, floorMax = 6 },
            new RogueRewardPoolEntry { id = "reward_hope_2", type = RogueRewardType.Currency, rarity = RogueRewardRarity.Common, weight = 35, amount = 2, floorMin = 1, floorMax = 6 },
            new RogueRewardPoolEntry { id = "reward_heal_1", type = RogueRewardType.Heal, rarity = RogueRewardRarity.Common, weight = 25, amount = 1, floorMin = 1, floorMax = 6 },
            new RogueRewardPoolEntry { id = "reward_max_hp_1", type = RogueRewardType.MaxHealth, rarity = RogueRewardRarity.Rare, weight = 15, amount = 1, floorMin = 2, floorMax = 6 },
            new RogueRewardPoolEntry { id = "reward_curio_secret_1", type = RogueRewardType.Relic, rarity = RogueRewardRarity.Epic, weight = 10, amount = 1, floorMin = 3, floorMax = 6, unique = true },
            new RogueRewardPoolEntry { id = "reward_curio_legend_1", type = RogueRewardType.Relic, rarity = RogueRewardRarity.Legendary, weight = 5, amount = 1, floorMin = 5, floorMax = 6, unique = true },
        };

        public static RogueReward RollBattleReward(RogueRunState state, RogueNode node, RogueRewardPoolConfig config = null)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (node == null) throw new ArgumentNullException(nameof(node));

            if (state.rewardPityRare >= 8)
            {
                state.rewardPityRare = 0;
                return BuildReward(PickEntry(node, config, RogueRewardRarity.Rare, 2f), state);
            }

            if (state.rewardPityLegend >= 12)
            {
                state.rewardPityLegend = 0;
                return BuildReward(PickEntry(node, config, RogueRewardRarity.Legendary, 10f), state);
            }

            RogueRewardPoolEntry entry = PickAny(node, config);
            if (entry.rarity == RogueRewardRarity.Rare) state.rewardPityRare = 0; else state.rewardPityRare++;
            if (entry.rarity == RogueRewardRarity.Legendary) state.rewardPityLegend = 0; else state.rewardPityLegend++;
            return BuildReward(entry, state);
        }

        static RogueRewardPoolEntry PickAny(RogueNode node, RogueRewardPoolConfig config)
        {
            return PickEntry(node, config, RogueRewardRarity.Common, 1f)
                ?? new RogueRewardPoolEntry { id = "reward_fallback_ingot", type = RogueRewardType.Gold, amount = 5, rarity = RogueRewardRarity.Common };
        }

        static RogueRewardPoolEntry PickEntry(RogueNode node, RogueRewardPoolConfig config, RogueRewardRarity minRarity, float rarityWeightBoost)
        {
            List<RogueRewardPoolEntry> pool = config != null && config.entries != null && config.entries.Count > 0
                ? config.entries
                : DefaultEntries;
            var filtered = new List<RogueRewardPoolEntry>();
            for (int i = 0; i < pool.Count; i++)
            {
                RogueRewardPoolEntry entry = pool[i];
                if (entry == null || entry.floorMin > node.floor || entry.floorMax < node.floor)
                {
                    continue;
                }
                if (entry.rarity < minRarity)
                {
                    continue;
                }
                filtered.Add(entry);
            }
            if (filtered.Count == 0)
            {
                return null;
            }
            int total = 0;
            for (int i = 0; i < filtered.Count; i++)
            {
                total += Math.Max(1, (int)(filtered[i].weight * rarityWeightBoost));
            }
            int roll = new Random(StableSeed(node)).Next(total);
            for (int i = 0; i < filtered.Count; i++)
            {
                int weight = Math.Max(1, (int)(filtered[i].weight * rarityWeightBoost));
                if (roll < weight)
                {
                    return filtered[i];
                }
                roll -= weight;
            }
            return filtered[0];
        }

        static RogueReward BuildReward(RogueRewardPoolEntry entry, RogueRunState state)
        {
            if (entry == null)
            {
                return new RogueReward { type = RogueRewardType.Gold, amount = 5, id = "reward_fallback" };
            }

            if (entry.rarity == RogueRewardRarity.Rare)
            {
                state.rewardPityRare = 0;
            }
            else if (entry.rarity == RogueRewardRarity.Legendary)
            {
                state.rewardPityLegend = 0;
            }

            return new RogueReward
            {
                type = entry.type,
                amount = entry.amount,
                id = entry.id
            };
        }

        static int StableSeed(RogueNode node)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + node.floor;
                if (!string.IsNullOrWhiteSpace(node.id))
                {
                    for (int i = 0; i < node.id.Length; i++)
                    {
                        hash = hash * 31 + node.id[i];
                    }
                }
                return hash;
            }
        }
    }
}

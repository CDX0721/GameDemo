using System;
using System.Collections.Generic;

namespace GameDemo.Rogue
{
    [Serializable]
    public sealed class RogueShopItem
    {
        public string itemId;
        public string category;
        public int basePrice;
        public RogueReward reward = new RogueReward();
    }

    [Serializable]
    public sealed class RogueShopInventoryEntry
    {
        public string slotId;
        public string itemId;
        public string category;
        public int price;
        public RogueReward reward = new RogueReward();
        public bool purchased;
    }

    [Serializable]
    public sealed class RogueShopState
    {
        public string nodeId;
        public int floor;
        public int refreshCount;
        public List<RogueShopInventoryEntry> entries = new List<RogueShopInventoryEntry>();
    }

    public static class RogueShopService
    {
        public static RogueShopState BuildInventory(
            string nodeId,
            int floor,
            IReadOnlyList<RogueShopItem> pool,
            int minCount,
            int maxCount,
            Random rng)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                throw new ArgumentException("nodeId is empty", nameof(nodeId));
            }
            if (pool == null || pool.Count == 0)
            {
                throw new InvalidOperationException("Shop pool is empty.");
            }
            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            int count = rng.Next(Math.Max(1, minCount), Math.Max(minCount, maxCount) + 1);
            var usedItemIds = new HashSet<string>();
            var state = new RogueShopState
            {
                nodeId = nodeId,
                floor = floor,
                refreshCount = 0
            };

            for (int i = 0; i < count; i++)
            {
                RogueShopItem item = PickUnique(pool, usedItemIds, rng);
                int price = GetScaledPrice(item.basePrice, floor, 0f);
                state.entries.Add(new RogueShopInventoryEntry
                {
                    slotId = $"{nodeId}_slot_{i}",
                    itemId = item.itemId,
                    category = item.category,
                    price = price,
                    reward = item.reward,
                    purchased = false
                });
            }

            return state;
        }

        public static int GetRefreshCost(int refreshCount)
        {
            if (refreshCount <= 0) return 0;
            if (refreshCount == 1) return 10;
            if (refreshCount == 2) return 20;
            return 40;
        }

        public static bool TryRefresh(
            RogueRunState runState,
            RogueShopState shopState,
            IReadOnlyList<RogueShopItem> pool,
            Random rng,
            int minCount,
            int maxCount)
        {
            int cost = GetRefreshCost(shopState.refreshCount);
            if (runState.ingots < cost)
            {
                return false;
            }

            runState.ingots -= cost;
            shopState.refreshCount++;
            shopState.entries.Clear();

            RogueShopState newState = BuildInventory(shopState.nodeId, shopState.floor, pool, minCount, maxCount, rng);
            newState.refreshCount = shopState.refreshCount;
            shopState.entries.AddRange(newState.entries);
            return true;
        }

        public static bool TryPurchase(RogueRunState runState, RogueShopState shopState, string slotId)
        {
            RogueShopInventoryEntry entry = FindEntry(shopState, slotId);
            if (entry == null || entry.purchased)
            {
                return false;
            }
            if (runState.ingots < entry.price)
            {
                return false;
            }

            runState.ingots -= entry.price;
            entry.purchased = true;
            RogueRewardApplier.Apply(runState, entry.reward);
            return true;
        }

        static RogueShopInventoryEntry FindEntry(RogueShopState state, string slotId)
        {
            for (int i = 0; i < state.entries.Count; i++)
            {
                RogueShopInventoryEntry entry = state.entries[i];
                if (entry != null && string.Equals(entry.slotId, slotId, StringComparison.Ordinal))
                {
                    return entry;
                }
            }
            return null;
        }

        static RogueShopItem PickUnique(IReadOnlyList<RogueShopItem> pool, HashSet<string> used, Random rng)
        {
            List<RogueShopItem> available = new List<RogueShopItem>();
            for (int i = 0; i < pool.Count; i++)
            {
                RogueShopItem item = pool[i];
                if (item == null || string.IsNullOrWhiteSpace(item.itemId))
                {
                    continue;
                }
                if (!used.Contains(item.itemId))
                {
                    available.Add(item);
                }
            }

            if (available.Count == 0)
            {
                available.AddRange(pool);
            }

            RogueShopItem selected = available[rng.Next(available.Count)];
            used.Add(selected.itemId);
            return selected;
        }

        static int GetScaledPrice(int basePrice, int floor, float discountRate)
        {
            int scaled = (int)Math.Floor(basePrice * (1f + floor * 0.15f));
            scaled = Math.Max(5, (scaled / 5) * 5);
            int discounted = (int)Math.Floor(scaled * (1f - Math.Max(0f, discountRate)));
            return Math.Max(5, (discounted / 5) * 5);
        }
    }
}

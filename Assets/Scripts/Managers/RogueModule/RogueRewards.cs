using System;

namespace GameDemo.Rogue
{
    public enum RogueRewardType
    {
        Gold,
        Heal,
        MaxHealth,
        Keys,
        Relic,
        Card,
        Item,
        Currency
    }

    [Serializable]
    public sealed class RogueReward
    {
        public RogueRewardType type;
        public int amount;
        public string id;
    }

    [Serializable]
    public sealed class RogueRewardRecord
    {
        public RogueRewardType type;
        public int amount;
        public string id;
    }

    public static class RogueRewardApplier
    {
        public static void Apply(RogueRunState state, RogueReward reward)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (reward == null)
            {
                return;
            }

            switch (reward.type)
            {
                case RogueRewardType.Gold:
                    state.ingots += reward.amount;
                    break;
                case RogueRewardType.Heal:
                    state.health += reward.amount;
                    break;
                case RogueRewardType.MaxHealth:
                    state.maxHealth += reward.amount;
                    state.health += reward.amount;
                    break;
                case RogueRewardType.Keys:
                    state.keys += reward.amount;
                    break;
                case RogueRewardType.Currency:
                    state.hope += reward.amount;
                    break;
                case RogueRewardType.Relic:
                    if (!string.IsNullOrWhiteSpace(reward.id))
                    {
                        if (state.curios.Contains(reward.id))
                        {
                            state.potentialPoints += reward.amount;
                        }
                        else
                        {
                            state.curios.Add(reward.id);
                        }
                    }
                    break;
                case RogueRewardType.Card:
                case RogueRewardType.Item:
                    if (!string.IsNullOrWhiteSpace(reward.id))
                    {
                        if (state.curios.Contains(reward.id))
                        {
                            state.potentialPoints += reward.amount;
                        }
                        else
                        {
                            state.curios.Add(reward.id);
                        }
                    }
                    break;
            }

            if (!string.IsNullOrWhiteSpace(reward.id) ||
                reward.type == RogueRewardType.Relic ||
                reward.type == RogueRewardType.Card ||
                reward.type == RogueRewardType.Item ||
                reward.type == RogueRewardType.Currency)
            {
                state.rewards.Add(new RogueRewardRecord
                {
                    type = reward.type,
                    amount = reward.amount,
                    id = reward.id
                });
            }

            state.ClampHealth();
        }
    }
}

using System;
using System.Collections.Generic;

namespace GameDemo.Rogue
{
    [Serializable]
    public sealed class RogueEventOutcome
    {
        public int weight = 100;
        public int hpDelta;
        public int hopeDelta;
        public int ingotsDelta;
        public string setFlag;
        public List<RogueReward> rewards = new List<RogueReward>();
    }

    [Serializable]
    public sealed class RogueEventOption
    {
        public string optionId;
        public string text;
        public int hpCost;
        public int hopeCost;
        public int ingotsCost;
        public string requiredFlag;
        public List<RogueEventOutcome> outcomes = new List<RogueEventOutcome>();
    }

    [Serializable]
    public sealed class RogueEventDefinition
    {
        public string eventId;
        public string title;
        public string triggerCondition;
        public string fallbackEventId;
        public List<RogueEventOption> options = new List<RogueEventOption>();
    }

    public static class RogueEventService
    {
        public static RogueNodeResult ResolveOption(
            RogueRunState state,
            RogueEventDefinition definition,
            string optionId,
            Random rng)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }
            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            RogueEventOption option = FindOption(definition, optionId);
            if (option == null)
            {
                throw new InvalidOperationException($"Event option not found: {optionId}");
            }

            if (!CanChooseOption(state, option))
            {
                throw new InvalidOperationException($"Event option requirements not met: {optionId}");
            }

            var result = new RogueNodeResult
            {
                success = true,
                healthDelta = -option.hpCost,
                hopeDelta = -option.hopeCost,
                ingotsDelta = -option.ingotsCost,
            };

            RogueEventOutcome outcome = PickOutcome(option.outcomes, rng);
            if (outcome != null)
            {
                result.healthDelta += outcome.hpDelta;
                result.hopeDelta += outcome.hopeDelta;
                result.ingotsDelta += outcome.ingotsDelta;
                if (!string.IsNullOrWhiteSpace(outcome.setFlag))
                {
                    result.flagsSet.Add(outcome.setFlag);
                }

                if (outcome.rewards != null)
                {
                    for (int i = 0; i < outcome.rewards.Count; i++)
                    {
                        result.rewards.Add(outcome.rewards[i]);
                    }
                }
            }

            return result;
        }

        public static bool CanChooseOption(RogueRunState state, RogueEventOption option)
        {
            if (state.health < option.hpCost || state.hope < option.hopeCost || state.ingots < option.ingotsCost)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(option.requiredFlag) && !state.flags.Contains(option.requiredFlag))
            {
                return false;
            }

            return true;
        }

        static RogueEventOption FindOption(RogueEventDefinition definition, string optionId)
        {
            for (int i = 0; i < definition.options.Count; i++)
            {
                RogueEventOption option = definition.options[i];
                if (option != null && string.Equals(option.optionId, optionId, StringComparison.Ordinal))
                {
                    return option;
                }
            }

            return null;
        }

        static RogueEventOutcome PickOutcome(List<RogueEventOutcome> outcomes, Random rng)
        {
            if (outcomes == null || outcomes.Count == 0)
            {
                return null;
            }

            int total = 0;
            for (int i = 0; i < outcomes.Count; i++)
            {
                total += Math.Max(0, outcomes[i].weight);
            }
            if (total <= 0)
            {
                return outcomes[0];
            }

            int roll = rng.Next(total);
            for (int i = 0; i < outcomes.Count; i++)
            {
                int weight = Math.Max(0, outcomes[i].weight);
                if (roll < weight)
                {
                    return outcomes[i];
                }
                roll -= weight;
            }
            return outcomes[outcomes.Count - 1];
        }
    }
}

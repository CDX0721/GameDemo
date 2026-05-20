using System;
using System.Collections.Generic;

namespace GameDemo.Rogue
{
    public static class RogueMapGenerator
    {
        public static RogueMap Generate(RogueRunConfig config, Random rng)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            var map = new RogueMap();
            var contentPool = config.GetContentPoolOrDefault();
            IReadOnlyList<RogueFloorRule> floorRules = config.GetFloorRulesOrDefault();

            var startNode = CreateNode("start_0", 0, RogueNodeType.Start, contentPool, rng);
            map.startNodeId = startNode.id;
            map.nodes.Add(startNode);

            List<RogueNode> incoming = new List<RogueNode> { startNode };
            for (int i = 0; i < floorRules.Count; i++)
            {
                RogueFloorRule rule = floorRules[i];
                BuildFloor(rule, incoming, map, contentPool, rng, config.maxNodeRetries, out List<RogueNode> outgoing);
                incoming = outgoing;
            }

            return map;
        }

        static void BuildFloor(
            RogueFloorRule rule,
            List<RogueNode> incoming,
            RogueMap map,
            RogueContentPool pool,
            Random rng,
            int maxRetries,
            out List<RogueNode> outgoing)
        {
            int floor = Math.Max(1, rule.floor);
            int nodeCount = Math.Max(1, rule.nodeCount);
            int regularCount = nodeCount;
            bool hasMidBoss = rule.includeMidBoss;
            bool hasFinalBoss = rule.includeFinalBoss;
            bool hasBoss = hasMidBoss || hasFinalBoss;
            if (hasBoss)
            {
                regularCount = Math.Max(1, nodeCount - 1);
            }

            var regularNodes = new List<RogueNode>(regularCount);
            int shopCount = 0;
            int restCount = 0;

            for (int i = 0; i < regularCount; i++)
            {
                RogueNodeType type = PickRegularNodeType(rule, incoming, regularNodes, rng, ref shopCount, ref restCount, maxRetries);
                var node = CreateNode($"node_{floor}_{i}", floor, type, pool, rng);
                node.hidden = rule.hidden;
                regularNodes.Add(node);
                map.nodes.Add(node);
            }

            if (rule.forceOneShop && shopCount == 0 && regularNodes.Count > 0)
            {
                RogueNode node = regularNodes[rng.Next(regularNodes.Count)];
                node.type = RogueNodeType.Shop;
                node.contentId = pool.PickContent(RogueNodeType.Shop, rng);
                shopCount = 1;
            }

            if (hasBoss)
            {
                RogueNodeType bossType = hasFinalBoss ? RogueNodeType.Boss : RogueNodeType.MidBoss;
                var bossNode = CreateNode($"node_{floor}_boss", floor, bossType, pool, rng);
                bossNode.hidden = rule.hidden;
                map.nodes.Add(bossNode);

                ConnectIncomingToRegular(incoming, regularNodes, Math.Max(1, rule.maxBranch), rng);
                for (int i = 0; i < regularNodes.Count; i++)
                {
                    regularNodes[i].nextIds.Add(bossNode.id);
                }

                outgoing = new List<RogueNode> { bossNode };
                return;
            }

            ConnectIncomingToRegular(incoming, regularNodes, Math.Max(1, rule.maxBranch), rng);
            outgoing = regularNodes;
        }

        static RogueNodeType PickRegularNodeType(
            RogueFloorRule rule,
            List<RogueNode> incoming,
            List<RogueNode> created,
            Random rng,
            ref int shopCount,
            ref int restCount,
            int maxRetries)
        {
            List<RogueFloorNodeWeight> weights = rule.weights ?? new List<RogueFloorNodeWeight>();
            if (weights.Count == 0)
            {
                return RogueNodeType.Battle;
            }

            for (int attempt = 0; attempt < Math.Max(1, maxRetries); attempt++)
            {
                RogueNodeType type = PickWeighted(weights, rng, RogueNodeType.Battle);
                if (type == RogueNodeType.Shop)
                {
                    if (shopCount >= rule.maxShop || HasImmediateShopParent(incoming))
                    {
                        continue;
                    }
                }

                if (type == RogueNodeType.Rest)
                {
                    if (rule.noRest || restCount >= rule.maxRest)
                    {
                        continue;
                    }
                }

                if (type == RogueNodeType.Boss || type == RogueNodeType.MidBoss || type == RogueNodeType.Start)
                {
                    continue;
                }

                if (type == RogueNodeType.Shop) shopCount++;
                if (type == RogueNodeType.Rest) restCount++;
                return type;
            }

            return RogueNodeType.Battle;
        }

        static bool HasImmediateShopParent(List<RogueNode> incoming)
        {
            for (int i = 0; i < incoming.Count; i++)
            {
                if (incoming[i] != null && incoming[i].type == RogueNodeType.Shop)
                {
                    return true;
                }
            }

            return false;
        }

        static void ConnectIncomingToRegular(List<RogueNode> incoming, List<RogueNode> regular, int maxBranch, Random rng)
        {
            if (incoming.Count == 0 || regular.Count == 0)
            {
                return;
            }

            int maxLinks = Math.Min(maxBranch, regular.Count);
            for (int i = 0; i < incoming.Count; i++)
            {
                RogueNode from = incoming[i];
                int links = rng.Next(1, maxLinks + 1);
                var chosen = new HashSet<int>();
                for (int l = 0; l < links; l++)
                {
                    int index = rng.Next(regular.Count);
                    if (!chosen.Add(index))
                    {
                        l--;
                        continue;
                    }
                    from.nextIds.Add(regular[index].id);
                }
            }

            for (int i = 0; i < regular.Count; i++)
            {
                RogueNode node = regular[i];
                if (HasIncoming(node, incoming))
                {
                    continue;
                }
                incoming[rng.Next(incoming.Count)].nextIds.Add(node.id);
            }
        }

        static RogueNode CreateNode(string id, int floor, RogueNodeType type, RogueContentPool pool, Random rng)
        {
            return new RogueNode
            {
                id = id,
                floor = floor,
                type = type,
                contentId = pool != null ? pool.PickContent(type, rng) : string.Empty,
                hidden = false,
                nextIds = new List<string>()
            };
        }

        static RogueNodeType PickWeighted(IReadOnlyList<RogueFloorNodeWeight> weights, Random rng, RogueNodeType fallback)
        {
            int total = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                total += Math.Max(0, weights[i].weight);
            }

            if (total <= 0)
            {
                return fallback;
            }

            int roll = rng.Next(total);
            for (int i = 0; i < weights.Count; i++)
            {
                int weight = Math.Max(0, weights[i].weight);
                if (roll < weight)
                {
                    return weights[i].type;
                }
                roll -= weight;
            }

            return fallback;
        }

        static bool HasIncoming(RogueNode node, List<RogueNode> previous)
        {
            for (int i = 0; i < previous.Count; i++)
            {
                RogueNode from = previous[i];
                for (int j = 0; j < from.nextIds.Count; j++)
                {
                    if (from.nextIds[j] == node.id)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}

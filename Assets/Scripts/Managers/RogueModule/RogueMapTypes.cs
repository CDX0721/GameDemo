using System;
using System.Collections.Generic;

namespace GameDemo.Rogue
{
    public enum RogueNodeType
    {
        Start,
        Battle,
        Elite,
        Event,
        Shop,
        Rest,
        MidBoss,
        Boss
    }

    [Serializable]
    public sealed class RogueNode
    {
        public string id;
        public int floor;
        public RogueNodeType type;
        public string contentId;
        public bool hidden;
        public List<string> nextIds = new List<string>();
    }

    [Serializable]
    public sealed class RogueMap
    {
        public string startNodeId;
        public List<RogueNode> nodes = new List<RogueNode>();

        public RogueNode FindNode(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return null;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                RogueNode node = nodes[i];
                if (node != null && node.id == nodeId)
                {
                    return node;
                }
            }

            return null;
        }

        public IReadOnlyList<RogueNode> GetFloor(int floorIndex)
        {
            var result = new List<RogueNode>();
            for (int i = 0; i < nodes.Count; i++)
            {
                RogueNode node = nodes[i];
                if (node != null && node.floor == floorIndex)
                {
                    result.Add(node);
                }
            }
            return result;
        }
    }
}

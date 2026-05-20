using System;
using System.Collections.Generic;

namespace GameDemo.Rogue
{
    [Serializable]
    public sealed class RogueRunSnapshot
    {
        public RogueRunConfig config;
        public RogueMap map;
        public RogueRunState state;
        public List<RogueShopState> shopStates = new List<RogueShopState>();
    }
}

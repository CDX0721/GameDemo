using System;
using System.Collections.Generic;

namespace GameDemo.Battle
{
    /// <summary>
    /// 附加效果模板，不直接参与对局。
    /// </summary>
    public class BattleEffect
    {
        public string Id { get; }
        public string DisplayName { get; set; }
        public BattleEffectType EffectType { get; set; }
        public BattleEffectStatusType StatusType { get; set; }

        /// <summary>初始持续回合数。</summary>
        public int InitialTurns { get; set; }

        /// <summary>最大叠加层数。</summary>
        public int MaxStackCount { get; set; }

        /// <summary>
        /// 影响函数列表，签名为 (来源, 承受方)。
        /// </summary>
        public List<Action<BattleUnitInstance?, BattleUnitInstance>> ApplyActions { get; }

        public BattleEffect(string id, string displayName, BattleEffectType effectType, BattleEffectStatusType statusType,
            int initialTurns = 1, int maxStackCount = 1)
        {
            Id = id;
            DisplayName = displayName;
            EffectType = effectType;
            StatusType = statusType;
            InitialTurns = initialTurns;
            MaxStackCount = maxStackCount;
            ApplyActions = new List<Action<BattleUnitInstance?, BattleUnitInstance>>();
        }

        public void Apply(BattleUnitInstance? source, BattleUnitInstance unit)
        {
            foreach (var action in ApplyActions)
                action(source, unit);
        }
    }
}

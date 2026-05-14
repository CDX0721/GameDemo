using System;

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
        /// 影响函数，(单位, 当前层数) => void。
        /// </summary>
        public Action<BattleUnitInstance, int> Apply { get; }

        public BattleEffect(string id, string displayName, BattleEffectType effectType, BattleEffectStatusType statusType,
            Action<BattleUnitInstance, int> apply, int initialTurns = 1, int maxStackCount = 1)
        {
            Id = id;
            DisplayName = displayName;
            EffectType = effectType;
            StatusType = statusType;
            Apply = apply;
            InitialTurns = initialTurns;
            MaxStackCount = maxStackCount;
        }
    }
}

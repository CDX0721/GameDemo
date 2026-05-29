using System;
using System.Collections.Generic;

namespace GameDemo.Battle
{
    /// <summary>
    /// 技能定义。委托签名包含施法者和单个承受方。
    /// </summary>
    public class Skill
    {
        public string Id { get; }
        public string DisplayName { get; set; }
        public SkillType SkillType { get; set; }
        public TargetType TargetType { get; set; }
        public int Level { get; set; }
        public string Description { get; set; } = "";
        /// <summary>AI 技能优先级。参数为目标单位，返回 0-100 浮点数，数值越大越优先。</summary>
        public Func<BattleUnitInstance, float> Priority { get; set; }

        public List<Func<BattleUnitInstance, BattleUnitInstance, bool>> CanCastConditions { get; }
        /// <summary>每个目标分别执行。</summary>
        public List<Action<BattleUnitInstance, BattleUnitInstance>> ApplyActions { get; }
        /// <summary>每次施放仅执行一次（如消耗法力），在所有 Apply 之前调用。</summary>
        public List<Action<BattleUnitInstance>> CastActions { get; }

        public Skill(string id, string displayName, SkillType skillType, TargetType targetType, int level = 1)
        {
            Id = id;
            DisplayName = displayName;
            SkillType = skillType;
            TargetType = targetType;
            Level = level;
            CanCastConditions = new List<Func<BattleUnitInstance, BattleUnitInstance, bool>>();
            ApplyActions = new List<Action<BattleUnitInstance, BattleUnitInstance>>();
            CastActions = new List<Action<BattleUnitInstance>>();
        }

        public bool CanCast(BattleUnitInstance caster, BattleUnitInstance target)
        {
            foreach (var condition in CanCastConditions)
                if (!condition(caster, target))
                    return false;
            return true;
        }

        public void Cast(BattleUnitInstance caster)
        {
            foreach (var action in CastActions)
                action(caster);
        }

        public void Apply(BattleUnitInstance caster, BattleUnitInstance target)
        {
            foreach (var action in ApplyActions)
                action(caster, target);
        }
    }
}

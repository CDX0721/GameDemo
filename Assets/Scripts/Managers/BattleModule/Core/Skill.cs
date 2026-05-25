using System;
using System.Collections.Generic;

namespace GameDemo.Battle
{
    /// <summary>
    /// 技能定义。施法者、数值等信息通过闭包捕获，仅承受方列表作为参数传入。
    /// </summary>
    public class Skill
    {
        public string Id { get; }
        public string DisplayName { get; set; }
        public SkillType SkillType { get; set; }
        public TargetType TargetType { get; set; }
        public int Level { get; set; }

        public List<Func<List<BattleUnitInstance>, bool>> CanCastConditions { get; }
        public List<Action<List<BattleUnitInstance>>> ApplyActions { get; }

        public Skill(string id, string displayName, SkillType skillType, TargetType targetType, int level = 1)
        {
            Id = id;
            DisplayName = displayName;
            SkillType = skillType;
            TargetType = targetType;
            Level = level;
            CanCastConditions = new List<Func<List<BattleUnitInstance>, bool>>();
            ApplyActions = new List<Action<List<BattleUnitInstance>>>();
        }

        public bool CanCast(List<BattleUnitInstance> targets)
        {
            foreach (var condition in CanCastConditions)
                if (!condition(targets))
                    return false;
            return true;
        }

        public void Apply(List<BattleUnitInstance> targets)
        {
            foreach (var action in ApplyActions)
                action(targets);
        }
    }
}

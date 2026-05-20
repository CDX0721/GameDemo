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
        public int ManaCost { get; set; }
        public bool OncePerBattle { get; set; }
        public bool UsedThisBattle { get; set; }
        public BattleUnitInstance? Caster { get; set; }

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
            if (OncePerBattle && UsedThisBattle)
                return false;

            BattleSkillContext.Current = this;
            try
            {
                foreach (var condition in CanCastConditions)
                    if (!condition(targets))
                        return false;
                return true;
            }
            finally
            {
                if (BattleSkillContext.Current == this)
                    BattleSkillContext.Current = null;
            }
        }

        public void Apply(List<BattleUnitInstance> targets)
        {
            BattleSkillContext.Current = this;
            try
            {
                foreach (var action in ApplyActions)
                    action(targets);
            }
            finally
            {
                if (BattleSkillContext.Current == this)
                    BattleSkillContext.Current = null;
            }
        }

        public Skill Clone()
        {
            var clone = new Skill(Id, DisplayName, SkillType, TargetType, Level)
            {
                ManaCost = ManaCost,
                OncePerBattle = OncePerBattle
            };
            clone.CanCastConditions.AddRange(CanCastConditions);
            clone.ApplyActions.AddRange(ApplyActions);
            return clone;
        }
    }
}

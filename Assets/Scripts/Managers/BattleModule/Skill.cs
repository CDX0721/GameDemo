using System;
using System.Collections.Generic;

namespace GameDemo.Battle
{
    /// <summary>
    /// 技能定义。包含等级数值表和一组匿名公式函数，公式按索引从等级数值表和目标单位列表计算最终数值。
    /// </summary>
    public class Skill
    {
        public string Id { get; }
        public string DisplayName { get; set; }
        public SkillType SkillType { get; set; }
        public TargetType TargetType { get; set; }
        public int Level { get; set; }

        /// <summary>
        /// 等级数值表 [levelIndex][valueIndex]。
        /// </summary>
        public List<List<float>> LevelValues { get; }

        /// <summary>
        /// 数值公式列表。每个公式签名为 (当前等级数值行, 目标实例列表) => 计算结果。
        /// </summary>
        public List<Func<List<float>, IReadOnlyList<BattleUnitInstance>, float>> Formulas { get; }

        /// <summary>
        /// 释放条件，传入调用单位和目标单位，返回是否满足释放条件。
        /// </summary>
        public Func<BattleUnitInstance, BattleUnitInstance, bool>? CanCast { get; set; }

        /// <summary>
        /// 影响函数，传入技能调用单位和目标单位，执行实际战斗效果。
        /// </summary>
        public Action<BattleUnitInstance, BattleUnitInstance>? Apply { get; set; }

        public Skill(string id, string displayName, SkillType skillType, TargetType targetType, int level = 1)
        {
            Id = id;
            DisplayName = displayName;
            SkillType = skillType;
            TargetType = targetType;
            Level = level;
            LevelValues = new List<List<float>>();
            Formulas = new List<Func<List<float>, IReadOnlyList<BattleUnitInstance>, float>>();
        }

        /// <summary>
        /// 获取当前等级对应的数值行。
        /// </summary>
        public List<float>? GetCurrentLevelValues()
        {
            int index = Level - 1;
            return index >= 0 && index < LevelValues.Count ? LevelValues[index] : null;
        }

        /// <summary>
        /// 执行指定公式，返回计算结果。
        /// </summary>
        public float EvaluateFormula(int formulaIndex, IReadOnlyList<BattleUnitInstance> targets)
        {
            List<float>? values = GetCurrentLevelValues();
            if (values == null) return 0f;
            if (formulaIndex < 0 || formulaIndex >= Formulas.Count) return 0f;
            return Formulas[formulaIndex](values, targets);
        }
    }
}

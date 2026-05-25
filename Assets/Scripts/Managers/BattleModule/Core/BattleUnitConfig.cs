using System;

namespace GameDemo.Battle
{
    /// <summary>
    /// 战斗单位配置，纯数据，可从代码构造或 JSON 反序列化。
    /// </summary>
    [Serializable]
    public struct BattleUnitConfig
    {
        public string Id;
        public string DisplayName;
        public float Attack;
        public float Defense;
        public float HP;
        public float Speed;
        public float Mana;
        public int Row;
        public int Col;
        public float InitialCost;
        public SkillConfig[] Skills;

        /// <summary>从配置创建 BattleUnit 模板（含技能）。</summary>
        public BattleUnit CreateTemplate()
        {
            var unit = new BattleUnit(Id, DisplayName, Attack, Defense, HP, Speed, Mana);
            if (Skills != null)
            {
                foreach (var sc in Skills)
                    unit.InnateSkills.Add(sc.CreateSkill());
            }
            return unit;
        }
    }

    [Serializable]
    public struct SkillConfig
    {
        public string Id;
        public string DisplayName;
        public string SkillType;
        public string TargetType;
        /// <summary>对应的技能特效ID，如 fx_slash, fx_poison, fx_stun。</summary>
        public string PerformanceFxId;

        public Skill CreateSkill()
        {
            var st = (SkillType)Enum.Parse(typeof(SkillType), SkillType);
            var tt = (TargetType)Enum.Parse(typeof(TargetType), TargetType);
            return new Skill(Id, DisplayName, st, tt);
        }
    }
}

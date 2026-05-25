using System.Collections.Generic;

namespace GameDemo.Battle
{
    /// <summary>
    /// 战斗单位模板（纯数据，不参与对局）。
    /// </summary>
    public class BattleUnit
    {
        public string Id { get; }
        public string DisplayName { get; set; }
        public float Attack { get; set; }
        public float Defense { get; set; }
        public float HP { get; set; }
        public float Speed { get; set; }
        public float Mana { get; set; }

        public List<Skill> InnateSkills { get; }

        public BattleUnit(string id, string displayName, float attack, float defense, float hp, float speed, float mana)
        {
            Id = id;
            DisplayName = displayName;
            Attack = attack;
            Defense = defense;
            HP = hp;
            Speed = speed;
            Mana = mana;
            InnateSkills = new List<Skill>();
        }
    }
}

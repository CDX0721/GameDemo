using System;
using System.Collections.Generic;

namespace DesignDemo
{
    public enum TargetSide
    {
        Self,
        Enemy,
        Both
    }

    public enum RewardKind
    {
        HealPercent,
        FirstTurnBlock,
        EnemyVulnerable,
        EnemyWeak,
        MaxHp,
        MaxEnergy,
        StartStrength,
        KeepBlock
    }

    [Serializable]
    public sealed class SkillData
    {
        public int Id;
        public int OwnerId;
        public int Quality;
        public bool OncePerBattle;
        public string Name;
        public string Type;
        public int BaseCost;
        public bool VariableCost;
        public TargetSide Target;
        public string EffectText;
        public string DetailText;

        public bool IsAttack
        {
            get { return Target != TargetSide.Self && (EffectText.Contains("hp") || EffectText.Contains("伤害")); }
        }
    }

    [Serializable]
    public sealed class EnemyMoveData
    {
        public string Label;
        public int Damage;
        public int Hits;
        public int Block;
        public int Strength;
        public int Vulnerable;

        public EnemyMoveData(string label, int damage, int hits, int block, int strength, int vulnerable)
        {
            Label = label;
            Damage = damage;
            Hits = Math.Max(1, hits);
            Block = block;
            Strength = strength;
            Vulnerable = vulnerable;
        }
    }

    [Serializable]
    public sealed class EnemyData
    {
        public int Id;
        public bool IsElite;
        public string Name;
        public int MaxHp;
        public List<EnemyMoveData> Moves = new List<EnemyMoveData>();
    }

    [Serializable]
    public sealed class RewardData
    {
        public string Name;
        public RewardKind Kind;
        public int Amount;
        public int BattleCount;
        public bool Rare;

        public RewardData(string name, RewardKind kind, int amount, int battleCount, bool rare)
        {
            Name = name;
            Kind = kind;
            Amount = amount;
            BattleCount = battleCount;
            Rare = rare;
        }
    }

    public static class DesignDatabase
    {
        public static List<SkillData> CreateSkills()
        {
            return new List<SkillData>
            {
                Skill(101, 1, 1, false, "A一下", "主动", 1, false, TargetSide.Enemy, "6hp", ""),
                Skill(102, 1, 1, false, "防一下", "主动", 1, false, TargetSide.Self, "5格挡", ""),
                Skill(103, 1, 2, false, "叠荆棘", "主动", 2, false, TargetSide.Self, "1敏捷+4荆棘", ""),
                Skill(104, 1, 2, false, "挂易伤", "主动+buff", 2, false, TargetSide.Enemy, "8hp+2易伤", ""),
                Skill(105, 1, 2, false, "挂虚弱+易伤", "主动+buff", 2, false, TargetSide.Enemy, "7hp+1虚弱+1易伤", ""),
                Skill(106, 1, 2, false, "攒个大的", "主动", 2, false, TargetSide.Self, "15格挡+失去2点生命值", ""),
                Skill(107, 1, 2, false, "终结技来咯！", "主动", 3, false, TargetSide.Enemy, "32hp", ""),
                Skill(108, 1, 2, true, "少女祈祷中", "主动", 3, false, TargetSide.Self, "18格挡+下回合获得额外2能量", ""),
                Skill(109, 1, 2, false, "你被强化了！快上！", "主动", 2, false, TargetSide.Both, "12hp+下一次非攻击技能免费打出", ""),
                Skill(110, 1, 3, true, "baka形态", "主动+被动+buff", 3, false, TargetSide.Self, "攻击附加1冰冻buff，每回合都打乱一次所有技能的耗能（0-3）", "回合结束时可花费1能量保留一张技能的耗能到下一回合。"),
                Skill(111, 1, 1, false, "肘击", "主动", 1, false, TargetSide.Enemy, "造成当前格挡值的伤害", ""),
                Skill(112, 1, 2, false, "花手", "主动", 0, true, TargetSide.Enemy, "造成X2次5hp伤害", "X1是消耗能量值，X2是当前剩余能量值。baka形态会修改X1但不修改X2。"),
                Skill(113, 1, 1, false, "透支潜能", "主动", 0, false, TargetSide.Self, "立刻加2能量，损失3点生命，每次使用多损失2点生命", "")
            };
        }

        public static List<EnemyData> CreateEnemies()
        {
            return new List<EnemyData>
            {
                new EnemyData
                {
                    Id = 101,
                    Name = "练习敌人 101",
                    MaxHp = 48,
                    IsElite = false,
                    Moves = new List<EnemyMoveData>
                    {
                        new EnemyMoveData("重击 8", 8, 1, 0, 0, 0),
                        new EnemyMoveData("连击 2x3", 2, 3, 0, 0, 0),
                        new EnemyMoveData("防御 10 + 力量 1", 0, 1, 10, 1, 0)
                    }
                },
                new EnemyData
                {
                    Id = 102,
                    Name = "练习敌人 102",
                    MaxHp = 54,
                    IsElite = false,
                    Moves = new List<EnemyMoveData>
                    {
                        new EnemyMoveData("重击 10", 10, 1, 0, 0, 0),
                        new EnemyMoveData("给予易伤 1", 0, 1, 0, 0, 1),
                        new EnemyMoveData("连击 2x4", 2, 4, 0, 0, 0),
                        new EnemyMoveData("防御 6 + 力量 1", 0, 1, 6, 1, 0)
                    }
                },
                new EnemyData
                {
                    Id = 201,
                    Name = "精英敌人 201",
                    MaxHp = 78,
                    IsElite = true,
                    Moves = new List<EnemyMoveData>
                    {
                        new EnemyMoveData("重击 19", 19, 1, 0, 0, 0),
                        new EnemyMoveData("连击 5x3", 5, 3, 0, 0, 0),
                        new EnemyMoveData("力量 2 + 重击 10", 10, 1, 0, 2, 0)
                    }
                }
            };
        }

        public static List<RewardData> CreateRewards(bool rare)
        {
            if (rare)
            {
                return new List<RewardData>
                {
                    new RewardData("血上限 +10", RewardKind.MaxHp, 10, 0, true),
                    new RewardData("能量上限 +1", RewardKind.MaxEnergy, 1, 0, true),
                    new RewardData("回血 40%", RewardKind.HealPercent, 40, 0, true),
                    new RewardData("每场战斗开始获得 2 点力量", RewardKind.StartStrength, 2, 0, true),
                    new RewardData("防御不再在回合结束后消失", RewardKind.KeepBlock, 1, 0, true)
                };
            }

            return new List<RewardData>
            {
                new RewardData("回血 20%", RewardKind.HealPercent, 20, 0, false),
                new RewardData("下三场战斗第一回合获得 10 防御", RewardKind.FirstTurnBlock, 10, 3, false),
                new RewardData("下两场战斗开始给予敌人 1 层易伤", RewardKind.EnemyVulnerable, 1, 2, false),
                new RewardData("下两场战斗开始给予敌人 1 层虚弱", RewardKind.EnemyWeak, 1, 2, false)
            };
        }

        private static SkillData Skill(int id, int ownerId, int quality, bool once, string name, string type, int cost, bool variableCost, TargetSide target, string effect, string detail)
        {
            return new SkillData
            {
                Id = id,
                OwnerId = ownerId,
                Quality = quality,
                OncePerBattle = once,
                Name = name,
                Type = type,
                BaseCost = cost,
                VariableCost = variableCost,
                Target = target,
                EffectText = effect,
                DetailText = detail
            };
        }
    }
}

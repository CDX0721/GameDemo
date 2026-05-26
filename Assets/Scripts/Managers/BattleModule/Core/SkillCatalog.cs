using System.Collections.Generic;

namespace GameDemo.Battle
{
    public static class SkillCatalog
    {
        private static readonly Dictionary<(string id, int level), Skill> _cache = new();

        private static bool _initialized;

        public static Skill Get(string id, int level = 1)
        {
            if (!_initialized) Init();

            var key = (id, level);
            if (!_cache.TryGetValue(key, out var skill))
            {
                skill = Build(id, level);
                _cache[key] = skill;
            }
            return skill;
        }

        private static void Init()
        {
            _initialized = true;
            Get("NormalAttack", 1);
            Get("NormalAttack", 2);
            Get("NormalAttack", 3);
            Get("Shield", 1);
            Get("Shield", 2);
            Get("Shield", 3);
            Get("EquivalentExchange", 1);
            Get("PenetrateArrow", 1);
            Get("Heal", 1);
            Get("Heal", 2);
            Get("Heal", 3);
            Get("SandStorm", 1);
            Get("FlashStrike", 1);
            Get("ManaDrain", 1);
            Get("ManaDrain", 2);
            Get("ManaDrain", 3);
        }

        private static Skill Build(string id, int level) => id switch
        {
            "NormalAttack"       => BuildNormalAttack(level),
            "Shield"             => BuildShield(level),
            "EquivalentExchange" => BuildEquivalentExchange(level),
            "PenetrateArrow"     => BuildPenetrateArrow(),
            "Heal"               => BuildHeal(level),
            "SandStorm"          => BuildSandStorm(),
            "FlashStrike"        => BuildFlashStrike(),
            "ManaDrain"          => BuildManaDrain(level),
            _ => throw new KeyNotFoundException($"未知技能 ID: {id}")
        };

        private static Skill BuildNormalAttack(int level)
        {
            float damageMultiplier = level switch
            {
                1 => 1.0f,
                2 => 1.5f,
                3 => 2.0f,
                _ => 1.0f
            };

            var skill = new Skill("NormalAttack", "普通攻击",
                SkillType.SingleAttack, TargetType.SingleEnemy, level);
            skill.Description = $"对敌方单体造成{damageMultiplier * 100:F0}%攻击力的伤害";

            skill.CanCastConditions.Add((caster, target) =>
                target.IsAlive);

            skill.ApplyActions.Add((caster, target) =>
            {
                if (target.IsAlive)
                    target.TakeDamage(caster.CurrentAttack * damageMultiplier);
            });

            return skill;
        }

        private static Skill BuildShield(int level)
        {
            int requiredMana = level switch
            {
                1 => 3,
                2 => 4,
                3 => 5,
                _ => 3
            };

            float shieldRatio = level switch
            {
                1 => 0.10f,
                2 => 0.15f,
                3 => 0.20f,
                _ => 0.10f
            };

            var skill = new Skill("Shield", "护盾",
                SkillType.Defense, TargetType.SingleAlly, level);
            skill.Description = $"消耗{requiredMana}点法力，为友方单体附加{shieldRatio * 100:F0}%最大生命值的护盾";

            skill.CanCastConditions.Add((caster, target) =>
                target.IsAlive && caster.CurrentMana >= requiredMana);

            skill.ApplyActions.Add((caster, target) =>
            {
                caster.CurrentMana -= requiredMana;
                if (target.IsAlive)
                    target.Shield += target.MaxHP * shieldRatio;
            });

            return skill;
        }

        private static Skill BuildEquivalentExchange(int level)
        {
            var skill = new Skill("EquivalentExchange", "等价交换",
                SkillType.Support, TargetType.SingleSelf, level);
            skill.Description = "交换自身当前生命值与法力值的比例，然后扣除10%最大法力值的法力";

            skill.CanCastConditions.Add((caster, target) =>
                target.IsAlive);

            skill.ApplyActions.Add((caster, target) =>
            {
                float hpRatio = caster.CurrentHP / caster.MaxHP;
                float manaRatio = caster.CurrentMana / caster.MaxMana;

                caster.CurrentHP = manaRatio * caster.MaxHP;
                caster.CurrentMana = hpRatio * caster.MaxMana;

                float deduction = caster.MaxMana * 0.10f;
                caster.CurrentMana = System.MathF.Max(0f, caster.CurrentMana - deduction);
            });

            return skill;
        }

        private static Skill BuildPenetrateArrow()
        {
            const int level = 1;
            const int manaCost = 25;

            var skill = new Skill("PenetrateArrow", "贯穿之箭",
                SkillType.Spread, TargetType.SingleEnemy, level);
            skill.Description = "消耗25点法力，对与施法者同列的所有敌方单位造成250%攻击力+10%目标最大生命值的伤害";

            skill.CanCastConditions.Add((caster, target) =>
                target.IsAlive && caster.CurrentMana >= manaCost);

            skill.CastActions.Add(caster =>
            {
                caster.CurrentMana -= manaCost;
            });

            skill.ApplyActions.Add((caster, target) =>
            {
                if (target.IsAlive)
                    target.TakeDamage(caster.CurrentAttack * 2.5f + target.MaxHP * 0.10f);
            });

            return skill;
        }

        private static Skill BuildHeal(int level)
        {
            int requiredMana = level switch
            {
                1 => 3,
                2 => 4,
                3 => 5,
                _ => 3
            };

            float healRatio = level switch
            {
                1 => 0.15f,
                2 => 0.20f,
                3 => 0.25f,
                _ => 0.15f
            };

            var skill = new Skill("Heal", "治疗",
                SkillType.Healing, TargetType.SingleAlly, level);
            skill.Description = $"消耗{requiredMana}点法力，恢复友方单体{healRatio * 100:F0}%最大生命值";

            skill.CanCastConditions.Add((caster, target) =>
                target.IsAlive && caster.CurrentMana >= requiredMana);

            skill.ApplyActions.Add((caster, target) =>
            {
                caster.CurrentMana -= requiredMana;
                if (target.IsAlive)
                {
                    float amount = target.MaxHP * healRatio;
                    target.CurrentHP = System.MathF.Min(target.CurrentHP + amount, target.MaxHP);
                }
            });

            return skill;
        }

        private static Skill BuildSandStorm()
        {
            const int level = 1;
            const int manaCost = 25;

            var skill = new Skill("SandStorm", "沙暴",
                SkillType.AoE, TargetType.AllEnemies, level);
            skill.Description = "消耗25点法力，对敌方全体造成200%攻击力的伤害";

            skill.CanCastConditions.Add((caster, target) =>
                caster.CurrentMana >= manaCost);

            skill.CastActions.Add(caster =>
            {
                caster.CurrentMana -= manaCost;
            });

            skill.ApplyActions.Add((caster, target) =>
            {
                if (target.IsAlive)
                    target.TakeDamage(caster.CurrentAttack * 2.0f);
            });

            return skill;
        }

        private static Skill BuildFlashStrike()
        {
            const int level = 1;
            const int manaCost = 10;

            var skill = new Skill("FlashStrike", "闪击",
                SkillType.Support, TargetType.SingleAlly, level);
            skill.Description = "消耗10点法力，清除友方单体所有负面效果，重置其行动代价为0";

            skill.CanCastConditions.Add((caster, target) =>
                target.IsAlive && caster.CurrentMana >= manaCost);

            skill.ApplyActions.Add((caster, target) =>
            {
                caster.CurrentMana -= manaCost;
                if (!target.IsAlive) return;

                for (int i = target.Effects.Count - 1; i >= 0; i--)
                    if (target.Effects[i].Template.EffectType == BattleEffectType.Negative)
                        target.Effects.RemoveAt(i);

                target.RecalculateStats();
                target.RemainingCost = 0f;
            });

            return skill;
        }

        private static Skill BuildManaDrain(int level)
        {
            float drainRatio = level switch
            {
                1 => 0.10f,
                2 => 0.15f,
                3 => 0.20f,
                _ => 0.10f
            };

            var skill = new Skill("ManaDrain", "吸魔",
                SkillType.SingleAttack, TargetType.SingleEnemy, level);
            skill.Description = $"吸取敌方单体{drainRatio * 100:F0}%最大法力值，为自己回复等量法力";

            skill.CanCastConditions.Add((caster, target) =>
                target.IsAlive);

            skill.ApplyActions.Add((caster, target) =>
            {
                if (!target.IsAlive) return;
                float drain = target.MaxMana * drainRatio;
                float actualDrain = System.MathF.Min(drain, target.CurrentMana);
                target.CurrentMana -= actualDrain;
                caster.CurrentMana = System.MathF.Min(caster.CurrentMana + actualDrain, caster.MaxMana);
            });

            return skill;
        }
    }
}

using System.Collections.Generic;

namespace GameDemo.Battle
{
    public static class SkillCatalog
    {
        /// <summary>AI 评估技能优先级时的当前施法者。评估前设置，评估后清除。</summary>
        public static BattleUnitInstance? EvaluatingUnit { get; set; }

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
            Get("AtkStrongUp", 1);
            Get("ThornsWrap", 1);
            Get("ThornsWrap", 2);
            Get("ThornsWrap", 3);
            Get("TheLastStand", 1);
            Get("Armageddon", 1);
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
            "AtkStrongUp"        => BuildAtkStrongUp(),
            "ThornsWrap"         => BuildThornsWrap(level),
            "TheLastStand"       => BuildTheLastStand(),
            "Armageddon"         => BuildArmageddon(),
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

            skill.Priority = target =>
            {
                if (!target.IsAlive || target.MaxHP <= 0f) return 0f;
                float ratio = target.CurrentHP / target.MaxHP;
                return 50f + (1f - ratio) * 30f;
            };

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

            skill.Priority = target =>
            {
                if (!target.IsAlive || target.MaxHP <= 0f) return 0f;
                float shieldNorm = target.Shield / target.MaxHP;
                float hpRatio = target.CurrentHP / target.MaxHP;
                float shieldNeed = 1f - System.MathF.Min(shieldNorm, 1f);
                float hpNeed = 1f - hpRatio;
                return shieldNeed * 50f + hpNeed * 30f;
            };

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

            skill.Priority = target =>
            {
                if (target.MaxHP <= 0f || target.MaxMana <= 0f) return 0f;
                float mp = target.CurrentMana / target.MaxMana;
                float hp = target.CurrentHP / target.MaxHP;
                float diff = mp - hp;
                if (diff <= 0f) return 5f;
                return System.MathF.Min(diff * 85f, 80f);
            };

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

            skill.ApplyActions.Add((caster, pickedTarget) =>
            {
                var bm = BattleManager.Instance;
                if (bm == null) return;
                var enemies = bm.IsPlayerUnit(caster) ? bm.EnemyFormation : bm.PlayerFormation;
                foreach (var u in enemies.Units)
                    if (u.IsAlive && u.Col == pickedTarget.Col)
                        u.TakeDamage(caster.CurrentAttack * 2.5f + u.MaxHP * 0.10f);
            });

            skill.Priority = target =>
            {
                int colCount = CountAliveEnemiesInColumn();
                int enemyCount = CountAliveEnemies();
                float bonus = enemyCount > 0 ? (float)colCount / enemyCount * 30f : 0f;
                if (!target.IsAlive || target.MaxHP <= 0f) return 30f + bonus;
                float ratio = target.CurrentHP / target.MaxHP;
                return 30f + bonus + ratio * 25f;
            };

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

            skill.Priority = target =>
            {
                if (!target.IsAlive || target.MaxHP <= 0f) return 5f;
                float ratio = target.CurrentHP / target.MaxHP;
                if (ratio >= 0.9f) return 5f;
                return (1f - ratio) * 85f;
            };

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

            skill.ApplyActions.Add((caster, _) =>
            {
                var bm = BattleManager.Instance;
                if (bm == null) return;
                var enemies = bm.IsPlayerUnit(caster) ? bm.EnemyFormation : bm.PlayerFormation;
                foreach (var u in enemies.Units)
                    if (u.IsAlive)
                        u.TakeDamage(caster.CurrentAttack * 2.0f);
            });

            skill.Priority = _ =>
            {
                int alive = CountAliveEnemies();
                if (alive <= 1) return 20f;
                return System.MathF.Min(alive * 22f, 85f);
            };

            return skill;
        }

        private static Skill BuildFlashStrike()
        {
            const int level = 1;
            const int manaCost = 10;

            var skill = new Skill("FlashStrike", "闪击",
                SkillType.Support, TargetType.SingleAlly, level);
            skill.Description = "消耗10点法力，驱散友方单体负面效果，重置其行动代价为0";

            skill.CanCastConditions.Add((caster, target) =>
                target.IsAlive && caster.CurrentMana >= manaCost);

            skill.ApplyActions.Add((caster, target) =>
            {
                caster.CurrentMana -= manaCost;
                if (!target.IsAlive) return;
                target.Dispel(BattleEffectType.Negative);
                target.RemainingCost = 0f;
            });

            skill.Priority = target =>
            {
                if (!target.IsAlive) return 0f;
                int negCount = CountNegativeEffects(target);
                if (negCount == 0) return 10f;
                return System.MathF.Min(negCount * 25f, 80f);
            };

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

            skill.Priority = _ =>
            {
                var caster = EvaluatingUnit;
                if (caster == null || caster.MaxMana <= 0f) return 0f;
                float mpRatio = caster.CurrentMana / caster.MaxMana;
                if (mpRatio >= 0.8f) return 15f;
                return 15f + (1f - mpRatio) * 70f;
            };

            return skill;
        }

        private static Skill BuildAtkStrongUp()
        {
            const int level = 1;
            const int manaCost = 10;

            var skill = new Skill("AtkStrongUp", "你被强化了！快上！",
                SkillType.Support, TargetType.SingleAlly, level);
            skill.Description = $"消耗{manaCost}点法力，使友方单位攻击力提高200%，持续1回合";

            skill.CanCastConditions.Add((caster, target) =>
                target.IsAlive && caster.CurrentMana >= manaCost);

            skill.ApplyActions.Add((caster, target) =>
            {
                caster.CurrentMana -= manaCost;
                if (target.IsAlive)
                    target.AddEffect(BattleEffectCatalog.Create("AtkMultUp", caster,
                        stackCount: 1, turns: 1, 200));
            });

            skill.Priority = _ => 65f;

            return skill;
        }

        private static Skill BuildThornsWrap(int level)
        {
            int manaCost = level switch
            {
                1 => 5,
                2 => 6,
                3 => 7,
                _ => 5
            };

            float damageRatio = level switch
            {
                1 => 0.50f,
                2 => 0.70f,
                3 => 0.90f,
                _ => 0.50f
            };

            var skill = new Skill("ThornsWrap", "荆棘缠绕",
                SkillType.SingleAttack, TargetType.SingleEnemy, level);
            skill.Description = $"消耗{manaCost}点法力，为目标施加2层荆棘效果（每回合受到{damageRatio * 100:F0}%攻击力的持续伤害），持续2回合";

            skill.CanCastConditions.Add((caster, target) =>
                target.IsAlive && caster.CurrentMana >= manaCost);

            skill.ApplyActions.Add((caster, target) =>
            {
                caster.CurrentMana -= manaCost;
                if (!target.IsAlive) return;
                int perStack = (int)(caster.CurrentAttack * damageRatio);
                target.AddEffect(BattleEffectCatalog.Create("Thorns", caster,
                    stackCount: 2, turns: 2, perStack));
            });

            skill.Priority = target =>
            {
                if (!target.IsAlive || target.MaxHP <= 0f) return 0f;
                float ratio = target.CurrentHP / target.MaxHP;
                if (ratio < 0.3f) return 10f;
                return 20f + ratio * 60f;
            };

            return skill;
        }

        private static Skill BuildTheLastStand()
        {
            const int level = 1;
            const int manaCost = 20;

            var skill = new Skill("TheLastStand", "背水一战",
                SkillType.Support, TargetType.AllAllies, level);
            skill.Description = $"消耗{manaCost}点法力，为全体友方添加伤害提高100%和受到伤害提高50%，持续3回合";

            skill.CanCastConditions.Add((caster, target) =>
                caster.CurrentMana >= manaCost);

            skill.CastActions.Add(caster =>
            {
                caster.CurrentMana -= manaCost;
            });

            skill.ApplyActions.Add((caster, _) =>
            {
                var bm = BattleManager.Instance;
                if (bm == null) return;
                var allies = bm.IsPlayerUnit(caster) ? bm.PlayerFormation : bm.EnemyFormation;
                foreach (var u in allies.Units)
                {
                    if (!u.IsAlive) continue;
                    u.AddEffect(BattleEffectCatalog.Create("DmgBonusUp", caster,
                        stackCount: 1, turns: 3, 100));
                    u.AddEffect(BattleEffectCatalog.Create("DmgReductionDown", caster,
                        stackCount: 1, turns: 3, 50));
                }
            });

            skill.Priority = _ =>
            {
                int allies = CountAliveAllies();
                if (allies <= 1) return 15f;
                return System.MathF.Min(allies * 20f, 80f);
            };

            return skill;
        }

        private static Skill BuildArmageddon()
        {
            const int level = 1;

            var skill = new Skill("Armageddon", "哈米吉多顿",
                SkillType.AoE, TargetType.AllEnemies, level);
            skill.Description = "消耗几乎所有的生命和法力值，对敌方全体造成特大伤害。我方仅剩一个单位时可释放。";

            skill.CanCastConditions.Add((caster, target) =>
            {
                var bm = BattleManager.Instance;
                if (bm == null) return false;
                int allyCount = bm.IsPlayerUnit(caster)
                    ? bm.AliveCountPlayer : bm.AliveCountEnemy;
                return allyCount == 1;
            });

            skill.CastActions.Add(caster =>
            {
                caster.CurrentHP = 1f;
                caster.CurrentMana = 1f;
            });

            skill.ApplyActions.Add((caster, _) =>
            {
                var bm = BattleManager.Instance;
                if (bm == null) return;
                var enemies = bm.IsPlayerUnit(caster) ? bm.EnemyFormation : bm.PlayerFormation;
                foreach (var u in enemies.Units)
                    if (u.IsAlive)
                        u.TakeDamage(u.MaxHP * 5f);
            });

            skill.Priority = _ => 95f;

            return skill;
        }

        // ================================================================
        // AI Priority Helpers — 通过 BattleManager.Instance / EvaluatingUnit 访问战场上下文
        // ================================================================

        private static int CountAliveAllies()
        {
            var bm = BattleManager.Instance;
            var caster = EvaluatingUnit;
            if (bm == null || caster == null) return 0;
            var allies = bm.IsPlayerUnit(caster) ? bm.PlayerFormation : bm.EnemyFormation;
            int count = 0;
            foreach (var u in allies.Units) if (u.IsAlive) count++;
            return count;
        }

        private static int CountAliveEnemies()
        {
            var bm = BattleManager.Instance;
            var caster = EvaluatingUnit;
            if (bm == null || caster == null) return 0;
            var enemies = bm.IsPlayerUnit(caster) ? bm.EnemyFormation : bm.PlayerFormation;
            int count = 0;
            foreach (var u in enemies.Units) if (u.IsAlive) count++;
            return count;
        }

        private static int CountAliveEnemiesInColumn()
        {
            var bm = BattleManager.Instance;
            var caster = EvaluatingUnit;
            if (bm == null || caster == null) return 0;
            var enemies = bm.IsPlayerUnit(caster) ? bm.EnemyFormation : bm.PlayerFormation;
            int count = 0;
            foreach (var u in enemies.Units)
                if (u.IsAlive && u.Col == caster.Col)
                    count++;
            return count;
        }

        private static int CountNegativeEffects(BattleUnitInstance unit)
        {
            int count = 0;
            foreach (var e in unit.Effects)
                if (e.Template.EffectType == BattleEffectType.Negative)
                    count++;
            return count;
        }
    }
}

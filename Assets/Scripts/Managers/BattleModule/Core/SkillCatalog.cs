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
            Get("DefenseBreak", 1);
            Get("DefenseBreak", 2);
            Get("DefenseBreak", 3);
            Get("Terminate", 1);
            Get("Terminate", 2);
            Get("Terminate", 3);
            Get("DarkCurse", 1);
            Get("DarkCurse", 2);
            Get("DarkCurse", 3);
            Get("Swamp", 1);
            Get("Swamp", 2);
            Get("Swamp", 3);
            Get("LightningChain", 1);
            Get("LightningChain", 2);
            Get("LightningChain", 3);
            Get("LightningChain", 4);
            Get("DiamondDust", 1);
            Get("Melt", 1);
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
            "DefenseBreak"       => BuildDefenseBreak(level),
            "Terminate"          => BuildTerminate(level),
            "DarkCurse"          => BuildDarkCurse(level),
            "Swamp"              => BuildSwamp(level),
            "LightningChain"     => BuildLightningChain(level),
            "DiamondDust"        => BuildDiamondDust(level),
            "Melt"               => BuildMelt(level),
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
                BattleSkillContext.RegisterSFX("NormalAttack");
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
                BattleSkillContext.RegisterSFX("Shield");
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
                BattleSkillContext.RegisterSFX("EquivalentExchange");
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
                var columnTargets = new List<BattleUnitInstance>();
                foreach (var u in enemies.Units)
                    if (u.IsAlive && u.Col == pickedTarget.Col)
                        columnTargets.Add(u);
                columnTargets.Sort((a, b) => a.Row.CompareTo(b.Row));

                float delay = 0f;
                foreach (var u in columnTargets)
                {
                    BattleSkillContext.RegisterSFX("PenetrateArrow", delay);
                    u.TakeDamage(caster.CurrentAttack * 2.5f + u.MaxHP * 0.10f);
                    BattleSkillContext.RegisterAnimTarget(u, delay);
                    delay += 0.2f;
                }
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
                BattleSkillContext.RegisterSFX("Heal");
                caster.CurrentMana -= requiredMana;
                if (target.IsAlive)
                {
                    float amount = target.MaxHP * healRatio;
                    target.TakeHeal(amount, caster);
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
                BattleSkillContext.RegisterSFX("SandStorm");
                foreach (var u in enemies.Units)
                    if (u.IsAlive)
                    {
                        u.TakeDamage(caster.CurrentAttack * 2.0f);
                        BattleSkillContext.RegisterAnimTarget(u);
                    }
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
                BattleSkillContext.RegisterSFX("FlashStrike");
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
                BattleSkillContext.RegisterSFX("ManaDrain");
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
                BattleSkillContext.RegisterSFX("AtkStrongUp");
                caster.CurrentMana -= manaCost;
                if (target.IsAlive)
                    target.AddEffect(BattleEffectCatalog.Create("AtkMultUp", caster,
                        stackCount: 1, turns: 1, 200));
            });

            skill.Priority = _ => 65f;

            return skill;
        }

        private static Skill BuildDefenseBreak(int level)
        {
            int manaCost = level switch { 1 => 12, 2 => 16, 3 => 20, _ => 12 };
            int defReduce = level switch { 1 => 400, 2 => 600, 3 => 800, _ => 400 };

            var skill = new Skill("DefenseBreak", "这就破防了？",
                SkillType.Support, TargetType.SingleEnemy, level);
            skill.Description = $"消耗{manaCost}点法力，使敌方单体防御力降低{defReduce}点，持续2回合";

            skill.CanCastConditions.Add((caster, target) =>
                target.IsAlive && caster.CurrentMana >= manaCost);

            skill.ApplyActions.Add((caster, target) =>
            {
                BattleSkillContext.RegisterSFX("DefenseBreak");
                caster.CurrentMana -= manaCost;
                if (target.IsAlive)
                    target.AddEffect(BattleEffectCatalog.Create("DefBonusDown", caster,
                        stackCount: 1, turns: 2, defReduce));
            });

            skill.Priority = target =>
            {
                if (!target.IsAlive) return 0f;
                float defNorm = target.CurrentDefense / System.MathF.Max(target.CurrentDefense, 200f);
                return 20f + defNorm * 50f;
            };

            return skill;
        }

        private static Skill BuildTerminate(int level)
        {
            float hpThreshold = level switch { 1 => 0.10f, 2 => 0.13f, 3 => 0.16f, _ => 0.10f };

            var skill = new Skill("Terminate", "终结",
                SkillType.SingleAttack, TargetType.SingleEnemy, level);
            skill.Description = $"对敌方单体造成目标100%最大生命值的真实伤害。仅当目标生命值低于{hpThreshold * 100:F0}%时可释放。";

            skill.CanCastConditions.Add((caster, target) =>
                target.IsAlive && target.CurrentHP / target.MaxHP < hpThreshold);

            skill.ApplyActions.Add((caster, target) =>
            {
                BattleSkillContext.RegisterSFX("Terminate");
                if (target.IsAlive)
                    target.TakeTrueDamage(target.MaxHP, caster);
            });

            skill.Priority = target =>
            {
                if (!target.IsAlive || target.MaxHP <= 0f) return 0f;
                float ratio = target.CurrentHP / target.MaxHP;
                if (ratio >= hpThreshold) return 0f;
                return 60f + target.MaxHP / 1000f * 20f;
            };

            return skill;
        }

        private static Skill BuildDarkCurse(int level)
        {
            int manaCost = level switch { 1 => 7, 2 => 9, 3 => 11, _ => 7 };

            var skill = new Skill("DarkCurse", "黑暗诅咒",
                SkillType.SingleAttack, TargetType.SingleEnemy, level);
            skill.Description = $"消耗{manaCost}点法力，为目标施加2层中毒效果（每回合受到2.5%最大生命值的真实伤害），持续2回合";

            skill.CanCastConditions.Add((caster, target) =>
                target.IsAlive && caster.CurrentMana >= manaCost);

            skill.ApplyActions.Add((caster, target) =>
            {
                BattleSkillContext.RegisterSFX("DarkCurse");
                caster.CurrentMana -= manaCost;
                if (!target.IsAlive) return;
                target.AddEffect(BattleEffectCatalog.Create("Poison", caster,
                    stackCount: 2, turns: 2));
            });

            skill.Priority = target =>
            {
                if (!target.IsAlive || target.MaxHP <= 0f) return 0f;
                float ratio = target.CurrentHP / target.MaxHP;
                if (ratio < 0.3f) return 10f;
                return 20f + ratio * 50f + target.MaxHP / 1000f * 10f;
            };

            return skill;
        }

        private static Skill BuildSwamp(int level)
        {
            int manaCost = level switch { 1 => 15, 2 => 20, 3 => 25, _ => 15 };
            int speedReduce = level switch { 1 => 15, 2 => 20, 3 => 25, _ => 15 };
            float delayRatio = level switch { 1 => 0.50f, 2 => 0.75f, 3 => 1.00f, _ => 0.50f };

            var skill = new Skill("Swamp", "沼泽",
                SkillType.Support, TargetType.AllEnemies, level);
            skill.Description = $"消耗{manaCost}点法力，使敌方全体速度降低{speedReduce}%持续2回合，行动延后{delayRatio * 100:F0}%";

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
                BattleSkillContext.RegisterSFX("Swamp");
                foreach (var u in enemies.Units)
                {
                    if (!u.IsAlive) continue;
                    u.AddEffect(BattleEffectCatalog.Create("SpdMultDown", caster,
                        stackCount: 1, turns: 2, speedReduce));
                    u.RemainingCost += u.InitialCost * delayRatio;
                    BattleSkillContext.RegisterAnimTarget(u);
                }
            });

            skill.Priority = _ =>
            {
                int alive = CountAliveEnemies();
                if (alive <= 1) return 25f;
                return System.MathF.Min(alive * 25f, 80f);
            };

            return skill;
        }

        private static Skill BuildLightningChain(int level)
        {
            int manaCost = level switch { 1 => 5, 2 => 6, 3 => 7, 4 => 8, _ => 5 };
            int bounceCount = level switch { 1 => 3, 2 => 4, 3 => 5, 4 => 6, _ => 3 };
            const float damageRatio = 1.50f;

            var skill = new Skill("LightningChain", "闪电链",
                SkillType.Spread, TargetType.SingleEnemy, level);
            skill.Description = $"消耗{manaCost}点法力，对目标造成{damageRatio * 100:F0}%攻击力伤害，并在周围3×3范围内的敌方间弹射{bounceCount}次";

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

                var current = pickedTarget;
                if (!current.IsAlive) return;

                float delay = 0f;
                const float bounceInterval = 0.2f;

                BattleSkillContext.RegisterSFX("LightningChain", delay);
                current.TakeDamage(caster.CurrentAttack * damageRatio, caster);
                BattleSkillContext.RegisterAnimTarget(current, delay);

                for (int i = 0; i < bounceCount; i++)
                {
                    var next = PickAdjacentEnemy(current, enemies);
                    if (next == null) break;
                    current = next;
                    delay += bounceInterval;
                    BattleSkillContext.RegisterSFX("LightningChain", delay);
                    current.TakeDamage(caster.CurrentAttack * damageRatio, caster);
                    BattleSkillContext.RegisterAnimTarget(current, delay);
                }
            });

            skill.Priority = target =>
            {
                if (!target.IsAlive) return 0f;
                int nearby = CountAdjacentEnemies(target);
                float nearbyBonus = System.MathF.Min(nearby * 15f, 45f);
                return 30f + nearbyBonus + bounceCount * 3f;
            };

            return skill;
        }

        /// <summary>在3×3范围内随机选择一个存活的敌方（排除自身）。</summary>
        private static BattleUnitInstance? PickAdjacentEnemy(BattleUnitInstance current, Formation enemies)
        {
            var candidates = new List<BattleUnitInstance>();
            foreach (var u in enemies.Units)
            {
                if (!u.IsAlive || u == current) continue;
                if (System.MathF.Abs(u.Row - current.Row) <= 1
                    && System.MathF.Abs(u.Col - current.Col) <= 1)
                    candidates.Add(u);
            }
            return candidates.Count > 0 ? candidates[UnityEngine.Random.Range(0, candidates.Count)] : null;
        }

        /// <summary>统计当前目标3×3范围内存活的敌方数量（排除自身）。</summary>
        private static int CountAdjacentEnemies(BattleUnitInstance current)
        {
            var bm = BattleManager.Instance;
            if (bm == null) return 0;
            var enemies = bm.IsPlayerUnit(current) ? bm.EnemyFormation : bm.PlayerFormation;
            int count = 0;
            foreach (var u in enemies.Units)
            {
                if (!u.IsAlive || u == current) continue;
                if (System.MathF.Abs(u.Row - current.Row) <= 1
                    && System.MathF.Abs(u.Col - current.Col) <= 1)
                    count++;
            }
            return count;
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
                BattleSkillContext.RegisterSFX("ThornsWrap");
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

        private static Skill BuildDiamondDust(int level)
        {
            const int manaCost = 15;

            var skill = new Skill("DiamondDust", "钻石星辰",
                SkillType.Spread, TargetType.SingleEnemy, level);
            skill.Description = $"消耗{manaCost}点法力，使目标所在行的所有敌方单位陷入冰冻状态2回合";

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
                BattleSkillContext.RegisterSFX("DiamondDust");
                foreach (var u in enemies.Units)
                {
                    if (u.IsAlive && u.Row == pickedTarget.Row)
                    {
                        u.AddEffect(BattleEffectCatalog.Create("Freeze", caster,
                            stackCount: 1, turns: 2));
                        BattleSkillContext.RegisterAnimTarget(u);
                    }
                }
            });

            skill.Priority = target =>
            {
                if (!target.IsAlive) return 0f;
                int rowCount = CountAliveEnemiesInRowOf(target);
                return 20f + System.MathF.Min(rowCount * 25f, 70f);
            };

            return skill;
        }

        /// <summary>统计与指定单位同排的存活的敌方数量。</summary>
        private static int CountAliveEnemiesInRowOf(BattleUnitInstance unit)
        {
            var bm = BattleManager.Instance;
            if (bm == null) return 0;
            var enemies = bm.IsPlayerUnit(unit) ? bm.EnemyFormation : bm.PlayerFormation;
            int count = 0;
            foreach (var u in enemies.Units)
                if (u.IsAlive && u.Row == unit.Row)
                    count++;
            return count;
        }

        private static Skill BuildMelt(int level)
        {
            const int manaCost = 15;
            const float hpRatio = 0.20f;
            const float atkMultiplier = 5.00f;

            var skill = new Skill("Melt", "熔化",
                SkillType.SingleAttack, TargetType.SingleEnemy, level);
            skill.Description = $"消耗{manaCost}点法力，对冰冻目标造成{hpRatio * 100:F0}%最大生命值+{atkMultiplier * 100:F0}%攻击力的真实伤害，并解除冰冻";

            skill.CanCastConditions.Add((caster, target) =>
                target.IsAlive && caster.CurrentMana >= manaCost && HasFreeze(target));

            skill.ApplyActions.Add((caster, target) =>
            {
                BattleSkillContext.RegisterSFX("Melt");
                caster.CurrentMana -= manaCost;
                if (!target.IsAlive) return;

                float damage = target.MaxHP * hpRatio + caster.CurrentAttack * atkMultiplier;
                target.TakeTrueDamage(damage, caster);

                // 驱散冰冻 → Dispel 内部触发 OnEffectRemoved → 表现层移除动画
                target.Dispel(BattleEffectType.Negative);
            });

            skill.Priority = target =>
            {
                if (!target.IsAlive) return 0f;
                if (!HasFreeze(target)) return 0f;
                float hpRatioTarget = target.CurrentHP / target.MaxHP;
                return 50f + (1f - hpRatioTarget) * 35f;
            };

            return skill;
        }

        private static bool HasFreeze(BattleUnitInstance unit)
        {
            foreach (var e in unit.Effects)
                if (e.Template.Id == "Freeze")
                    return true;
            return false;
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
                BattleSkillContext.RegisterSFX("TheLastStand");
                foreach (var u in allies.Units)
                {
                    if (!u.IsAlive) continue;
                    u.AddEffect(BattleEffectCatalog.Create("DmgBonusUp", caster,
                        stackCount: 1, turns: 3, 100));
                    u.AddEffect(BattleEffectCatalog.Create("DmgReductionDown", caster,
                        stackCount: 1, turns: 3, 50));
                    BattleSkillContext.RegisterAnimTarget(u);
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
            skill.Description = "消耗几乎所有的生命和法力值，对敌方全体造成目标500%最大生命值的真实伤害。我方仅剩一个单位时可释放。";

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
                BattleSkillContext.RegisterSFX("Armageddon");
                foreach (var u in enemies.Units)
                    if (u.IsAlive)
                    {
                        u.TakeTrueDamage(u.MaxHP * 5f, caster);
                        BattleSkillContext.RegisterAnimTarget(u);
                    }
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

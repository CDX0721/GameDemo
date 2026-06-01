using System;
using System.Collections.Generic;

namespace GameDemo.Battle
{
    public static class BattleEffectCatalog
    {
        private static readonly Dictionary<string, BattleEffect> _cache = new();

        public static BattleEffectInstance Create(string id, BattleUnitInstance? source,
            int stackCount, int turns = 1, params int[] values)
        {
            if (!_cache.TryGetValue(id, out var template))
            {
                template = Build(id, values);
                _cache[id] = template;
            }

            var instance = new BattleEffectInstance(template, source)
            {
                CurrentStackCount = stackCount,
                RemainingTurns = turns
            };
            return instance;
        }

        private static BattleEffect Build(string id, int[] v) => id switch
        {
            // ==================== 攻击 ====================
            "AtkMultUp"   => StatMod(id, "攻击乘数上升", BattleEffectType.Positive, v[0],
                                   (u, val) => u.AttackMultiplier += val),
            "AtkMultDown" => StatMod(id, "攻击乘数下降", BattleEffectType.Negative, v[0],
                                   (u, val) => u.AttackMultiplier -= val),
            "AtkBonusUp"   => StatMod(id, "攻击附加上升", BattleEffectType.Positive, v[0],
                                   (u, val) => u.BonusAttack += val),
            "AtkBonusDown" => StatMod(id, "攻击附加下降", BattleEffectType.Negative, v[0],
                                   (u, val) => u.BonusAttack -= val),

            // ==================== 防御 ====================
            "DefMultUp"   => StatMod(id, "防御乘数上升", BattleEffectType.Positive, v[0],
                                   (u, val) => u.DefenseMultiplier += val),
            "DefMultDown" => StatMod(id, "防御乘数下降", BattleEffectType.Negative, v[0],
                                   (u, val) => u.DefenseMultiplier -= val),
            "DefBonusUp"   => StatMod(id, "防御附加上升", BattleEffectType.Positive, v[0],
                                   (u, val) => u.BonusDefense += val),
            "DefBonusDown" => StatMod(id, "防御附加下降", BattleEffectType.Negative, v[0],
                                   (u, val) => u.BonusDefense -= val),

            // ==================== 生命 ====================
            "HpMultUp"   => StatMod(id, "生命乘数上升", BattleEffectType.Positive, v[0],
                                   (u, val) => u.MaxHPMultiplier += val),
            "HpMultDown" => StatMod(id, "生命乘数下降", BattleEffectType.Negative, v[0],
                                   (u, val) => u.MaxHPMultiplier -= val),
            "HpBonusUp"   => StatMod(id, "生命附加上升", BattleEffectType.Positive, v[0],
                                   (u, val) => u.BonusMaxHP += val),
            "HpBonusDown" => StatMod(id, "生命附加下降", BattleEffectType.Negative, v[0],
                                   (u, val) => u.BonusMaxHP -= val),

            // ==================== 速度 ====================
            "SpdMultUp"   => StatMod(id, "速度乘数上升", BattleEffectType.Positive, v[0],
                                   (u, val) => u.SpeedMultiplier += val),
            "SpdMultDown" => StatMod(id, "速度乘数下降", BattleEffectType.Negative, v[0],
                                   (u, val) => u.SpeedMultiplier -= val),
            "SpdBonusUp"   => StatMod(id, "速度附加上升", BattleEffectType.Positive, v[0],
                                   (u, val) => u.BonusSpeed += val),
            "SpdBonusDown" => StatMod(id, "速度附加下降", BattleEffectType.Negative, v[0],
                                   (u, val) => u.BonusSpeed -= val),

            // ==================== 法力 ====================
            "ManaMultUp"   => StatMod(id, "法力乘数上升", BattleEffectType.Positive, v[0],
                                   (u, val) => u.MaxManaMultiplier += val),
            "ManaMultDown" => StatMod(id, "法力乘数下降", BattleEffectType.Negative, v[0],
                                   (u, val) => u.MaxManaMultiplier -= val),
            "ManaBonusUp"   => StatMod(id, "法力附加上升", BattleEffectType.Positive, v[0],
                                   (u, val) => u.BonusMaxMana += val),
            "ManaBonusDown" => StatMod(id, "法力附加下降", BattleEffectType.Negative, v[0],
                                   (u, val) => u.BonusMaxMana -= val),

            // ==================== 伤害 / 受到伤害 ====================
            "DmgBonusUp"      => PercentMod(id, "伤害提高", BattleEffectType.Positive, v[0],
                                            (u, val) => u.DamageBonus += val),
            "DmgBonusDown"    => PercentMod(id, "伤害降低", BattleEffectType.Negative, v[0],
                                            (u, val) => u.DamageBonus -= val),
            "DmgReductionUp"   => PercentMod(id, "受到伤害降低", BattleEffectType.Positive, v[0],
                                            (u, val) => u.DamageReduction += val),
            "DmgReductionDown" => PercentMod(id, "受到伤害提高", BattleEffectType.Negative, v[0],
                                            (u, val) => u.DamageReduction -= val),

            "Thorns" => BuildThorns(v[0]),
            "Poison" => BuildPoison(),
            "Freeze" => BuildFreeze(),
            _ => throw new KeyNotFoundException($"未知效果 ID: {id}")
        };

        private static BattleEffect PercentMod(string id, string displayName, BattleEffectType effectType,
            int baseValue, Action<BattleUnitInstance, float> modifier)
        {
            var effect = new BattleEffect(id, displayName, effectType,
                BattleEffectStatusType.StatChange, maxStackCount: 1)
            {
                Description = $"{displayName}{baseValue}%",
                IsDispellable = effectType == BattleEffectType.Negative
            };

            effect.ApplyActions.Add((_, unit, stackCount) =>
            {
                float amount = baseValue / 100f * stackCount;
                modifier(unit, amount);
            });

            return effect;
        }

        private static BattleEffect BuildThorns(int damagePerStack)
        {
            var effect = new BattleEffect("Thorns", "荆棘",
                BattleEffectType.Negative, BattleEffectStatusType.Damage,
                maxStackCount: 5)
            {
                Description = "回合开始时，受到持续伤害",
                IsDispellable = true
            };

            effect.ApplyActions.Add((source, unit, stackCount) =>
            {
                unit.TakeDamage(damagePerStack * stackCount, source);
            });

            return effect;
        }

        private static BattleEffect BuildFreeze()
        {
            var effect = new BattleEffect("Freeze", "冰冻",
                BattleEffectType.Negative, BattleEffectStatusType.Control,
                maxStackCount: 1)
            {
                Description = "无法行动",
                IsDispellable = true
            };

            effect.ApplyActions.Add((_, unit, _) =>
            {
                unit.CanAct = false;
            });

            return effect;
        }

        private static BattleEffect BuildPoison()
        {
            var effect = new BattleEffect("Poison", "中毒",
                BattleEffectType.Negative, BattleEffectStatusType.Damage,
                maxStackCount: 5)
            {
                Description = "回合开始时，受到2.5%最大生命值×层数的真实伤害",
                IsDispellable = true
            };

            effect.ApplyActions.Add((source, unit, stackCount) =>
            {
                unit.TakeTrueDamage(unit.MaxHP * 0.025f * stackCount, source);
            });

            return effect;
        }

        private static BattleEffect StatMod(string id, string displayName, BattleEffectType effectType,
            int baseValue, Action<BattleUnitInstance, float> modifier)
        {
            bool isMult = id.Contains("Mult");

            var effect = new BattleEffect(id, displayName, effectType,
                BattleEffectStatusType.StatChange, maxStackCount: 1)
            {
                Description = MakeStatDescription(id, baseValue),
                IsDispellable = effectType == BattleEffectType.Negative
            };

            effect.ApplyActions.Add((_, unit, stackCount) =>
            {
                float amount = isMult ? baseValue / 100f * stackCount : baseValue * stackCount;
                modifier(unit, amount);
            });

            return effect;
        }

        private static string MakeStatDescription(string id, int value)
        {
            bool isMult = id.Contains("Mult");
            bool isUp = id.EndsWith("Up");

            // 取 ID 前缀作为属性键，去掉 Mult/Bonus + Up/Down
            int modIdx = isMult ? id.IndexOf("Mult") : id.IndexOf("Bonus");
            string statKey = modIdx > 0 ? id.Substring(0, modIdx) : id;

            string statName = statKey switch
            {
                "Atk" => "攻击力",
                "Def" => "防御力",
                "Hp"  => "生命值",
                "Spd" => "速度",
                "Mana"=> "法力值",
                _     => statKey
            };

            string verb = isUp ? "增加" : "降低";
            return isMult ? $"{statName}{verb}{value}%" : $"{statName}{verb}{value}";
        }
    }
}

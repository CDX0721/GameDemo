using System;
using System.Collections.Generic;

namespace GameDemo.Battle
{
    /// <summary>
    /// 战斗单位对局实例，持有模板引用和所有对局会话状态。
    /// 最终属性 = 基础属性 * 乘数 + 额外（暂不编码，由附加效果系统驱动）。
    /// </summary>
    public class BattleUnitInstance
    {
        // ==================== 模板引用 & 标识 ====================

        public BattleUnit Template { get; }
        public string Id => Template.Id;
        public string DisplayName { get; set; }

        /// <summary>阵形中的行坐标（未布阵时为 -1）。</summary>
        public int Row { get; set; } = -1;

        /// <summary>阵形中的列坐标（未布阵时为 -1）。</summary>
        public int Col { get; set; } = -1;

        // ==================== 基础属性（来自 Template，只读快照）====================

        public float BaseMaxHP { get; }
        public float BaseMaxMana { get; }
        public float BaseAttack { get; }
        public float BaseDefense { get; }
        public float BaseSpeed { get; }

        // ==================== 属性乘数（附加效果可修改）====================

        public float MaxHPMultiplier { get; set; }   = 1f;
        public float MaxManaMultiplier { get; set; } = 1f;
        public float AttackMultiplier { get; set; }  = 1f;
        public float DefenseMultiplier { get; set; } = 1f;
        public float SpeedMultiplier { get; set; }   = 1f;

        // ==================== 额外属性（附加效果可修改）====================

        public float BonusMaxHP { get; set; }
        public float BonusMaxMana { get; set; }
        public float BonusAttack { get; set; }
        public float BonusDefense { get; set; }
        public float BonusSpeed { get; set; }

        // ==================== 当前运行时值 ====================

        public float CurrentHP { get; set; }
        public float MaxHP { get; set; }
        public float CurrentMana { get; set; }
        public float MaxMana { get; set; }
        public float CurrentAttack { get; set; }
        public float CurrentDefense { get; set; }
        public float CurrentSpeed { get; set; }

        // ==================== 护盾 & 伤害修正 ====================

        public float Shield { get; set; }
        public float DamageBonus { get; set; }
        public float DamageReduction { get; set; }

        // ==================== 行动代价（对局时间状态）====================

        public float InitialCost { get; set; }
        public float RemainingCost { get; set; }

        // ==================== 技能 & 附加效果 ====================

        /// <summary>对局中可用的技能列表。</summary>
        public List<Skill> Skills { get; }

        /// <summary>当前身上的附加效果实例列表。</summary>
        public List<BattleEffectInstance> Effects { get; }

        /// <summary>附加效果被施加时触发。</summary>
        public event Action<BattleEffectInstance>? OnEffectAdded;

        /// <summary>附加效果被移除时触发（驱散 / 手动移除）。</summary>
        public event Action<BattleEffectInstance>? OnEffectRemoved;

        /// <summary>
        /// 驱散指定类型的效果。仅移除 IsDispellable == true 的效果。
        /// 若 filter 为 null，驱散所有可驱散效果。
        /// </summary>
        public void Dispel(BattleEffectType? filter = null)
        {
            for (int i = Effects.Count - 1; i >= 0; i--)
            {
                if (Effects[i].Template.IsDispellable
                    && (!filter.HasValue || Effects[i].Template.EffectType == filter.Value))
                {
                    var removed = Effects[i];
                    Effects.RemoveAt(i);
                    OnEffectRemoved?.Invoke(removed);
                }
            }
            RecalculateStats();
        }

        // ==================== 控制状态 ====================

        /// <summary>是否可行动（可被技能/附加效果修改）。</summary>
        public bool CanAct { get; set; } = true;

        // ==================== 计算属性 ====================

        public float ActionValue => CurrentSpeed > 0f ? RemainingCost / CurrentSpeed : float.MaxValue;
        public bool IsAlive => CurrentHP > 0f;

        // ==================== 构造 ====================

        public BattleUnitInstance(BattleUnit template, float initialCost)
        {
            Template = template;
            DisplayName = template.DisplayName;

            BaseMaxHP = template.HP;
            BaseMaxMana = template.Mana;
            BaseAttack = template.Attack;
            BaseDefense = template.Defense;
            BaseSpeed = template.Speed;

            MaxHP = BaseMaxHP;
            CurrentHP = MaxHP;
            MaxMana = BaseMaxMana;
            CurrentMana = MaxMana;
            CurrentAttack = BaseAttack;
            CurrentDefense = BaseDefense;
            CurrentSpeed = BaseSpeed;

            InitialCost = initialCost;
            RemainingCost = initialCost;

            Skills = new List<Skill>(template.InnateSkills);
            Effects = new List<BattleEffectInstance>();
        }

        /// <summary>
        /// 重置剩余行动代价为初始行动代价。
        /// </summary>
        public void ResetCost()
        {
            RemainingCost = InitialCost;
        }

        // ==================== 附加效果管理 ====================

        /// <summary>
        /// 施加附加效果实例。以 (Template.Id, Source) 为关键字去重：
        /// 相同关键字时替换为新模板值，叠层合并，刷新持续回合；不同关键字则新增。
        /// </summary>
        public void AddEffect(BattleEffectInstance newEffect)
        {
            for (int i = Effects.Count - 1; i >= 0; i--)
            {
                var inst = Effects[i];
                if (inst.Template.Id == newEffect.Template.Id && inst.Source == newEffect.Source)
                {
                    int total = inst.CurrentStackCount + newEffect.CurrentStackCount;
                    if (total > newEffect.Template.MaxStackCount)
                        total = newEffect.Template.MaxStackCount;
                    newEffect.CurrentStackCount = total;
                    Effects.RemoveAt(i);
                    break;
                }
            }
            Effects.Add(newEffect);
            OnEffectAdded?.Invoke(newEffect);
        }

        /// <summary>
        /// 施加附加效果（简易重载）。模板 + 来源，默认 1 层。
        /// </summary>
        public void AddEffect(BattleEffect template, BattleUnitInstance? source)
        {
            AddEffect(new BattleEffectInstance(template, source));
        }

        // ==================== 状态重置 & 重算 ====================

        /// <summary>
        /// 重置所有乘数、额外值、护盾和伤害修正为默认值。
        /// </summary>
        public void ResetModifiers()
        {
            MaxHPMultiplier = 1f;
            MaxManaMultiplier = 1f;
            AttackMultiplier = 1f;
            DefenseMultiplier = 1f;
            SpeedMultiplier = 1f;

            BonusMaxHP = 0f;
            BonusMaxMana = 0f;
            BonusAttack = 0f;
            BonusDefense = 0f;
            BonusSpeed = 0f;

            DamageBonus = 0f;
            DamageReduction = 0f;
            CanAct = true;
        }

        /// <summary>
        /// 根据 基础属性 * 乘数 + 额外 重新计算所有运行时属性。
        /// </summary>
        public void RecalculateStats()
        {
            MaxHP = BaseMaxHP * MaxHPMultiplier + BonusMaxHP;
            MaxMana = BaseMaxMana * MaxManaMultiplier + BonusMaxMana;
            CurrentAttack = BaseAttack * AttackMultiplier + BonusAttack;
            CurrentDefense = BaseDefense * DefenseMultiplier + BonusDefense;
            CurrentSpeed = BaseSpeed * SpeedMultiplier + BonusSpeed;

            if (CurrentHP > MaxHP) CurrentHP = MaxHP;
            if (CurrentMana > MaxMana) CurrentMana = MaxMana;
        }

        // ==================== 伤害 ====================

        private const float DefenseK = 1000f;

        /// <summary>单位受到伤害时触发（单位, 实际伤害值, 是否真实伤害, 伤害来源）。</summary>
        public event Action<BattleUnitInstance, float, bool, BattleUnitInstance?>? OnDamaged;

        /// <summary>
        /// 防御减伤比例。DEF&gt;=0 时为 1-e^(-DEF/K)，DEF&lt;0 时为 max(-4, -0.5*(DEF/K)^2+DEF/K)。
        /// 返回值可能为负数，表示受伤增加。
        /// </summary>
        private static float DefenseRate(float defense)
        {
            if (defense >= 0f)
                return 1f - MathF.Exp(-defense / DefenseK);

            float x = defense / DefenseK;
            return MathF.Max(-4f, -0.5f * x * x + x);
        }

        /// <summary>
        /// 受到伤害：扣减护盾 → 防御减伤 → DamageReduction → 扣减 HP。
        /// </summary>
        public float TakeDamage(float rawDamage, BattleUnitInstance? source = null)
        {
            if (!IsAlive) return 0f;

            float reduced = rawDamage * (1f - DamageReduction) * (1f - DefenseRate(CurrentDefense));
            if (reduced < 0f) reduced = 0f;

            float actualDamage = reduced;
            if (Shield > 0f)
            {
                if (Shield >= reduced)
                {
                    Shield -= reduced;
                    OnDamaged?.Invoke(this, reduced, false, source);
                    return reduced;
                }
                actualDamage = reduced - Shield;
                Shield = 0f;
            }

            if (actualDamage < 0f) actualDamage = 0f;
            CurrentHP -= actualDamage;
            if (CurrentHP < 0f) CurrentHP = 0f;
            OnDamaged?.Invoke(this, actualDamage, false, source);
            return actualDamage;
        }

        /// <summary>
        /// 真实伤害：无视护盾、防御减伤和伤害减少，直接扣减HP。
        /// </summary>
        public float TakeTrueDamage(float rawDamage, BattleUnitInstance? source = null)
        {
            if (!IsAlive) return 0f;

            float actualDamage = MathF.Max(rawDamage, 0f);
            if (actualDamage > CurrentHP) actualDamage = CurrentHP;
            CurrentHP -= actualDamage;
            if (CurrentHP < 0f) CurrentHP = 0f;
            OnDamaged?.Invoke(this, actualDamage, true, source);
            return actualDamage;
        }

        // ==================== 治疗 ====================

        /// <summary>单位受到治疗时触发（单位, 实际治疗量, 治疗来源）。</summary>
        public event Action<BattleUnitInstance, float, BattleUnitInstance?>? OnHealed;

        /// <summary>
        /// 受到治疗：回复 HP，不超过 MaxHP。
        /// </summary>
        public float TakeHeal(float amount, BattleUnitInstance? source = null)
        {
            if (!IsAlive) return 0f;

            float actualHeal = MathF.Min(amount, MaxHP - CurrentHP);
            if (actualHeal <= 0f) return 0f;

            CurrentHP += actualHeal;
            OnHealed?.Invoke(this, actualHeal, source);
            return actualHeal;
        }
    }
}

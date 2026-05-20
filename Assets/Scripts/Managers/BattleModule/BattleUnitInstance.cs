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

            Skills = new List<Skill>(template.InnateSkills.Count);
            for (int i = 0; i < template.InnateSkills.Count; i++)
            {
                Skills.Add(template.InnateSkills[i].Clone());
            }
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
        /// 施加附加效果。以 (Template.Id, Source) 为关键字去重：
        /// 相同关键字且未达最大层数时叠加并刷新持续回合；不同关键字则新增实例。
        /// </summary>
        public void AddEffect(BattleEffect template, BattleUnitInstance? source)
        {
            foreach (BattleEffectInstance inst in Effects)
            {
                if (inst.Template.Id == template.Id && inst.Source == source)
                {
                    if (inst.CurrentStackCount < template.MaxStackCount)
                        inst.CurrentStackCount++;
                    inst.RemainingTurns = template.InitialTurns;
                    OnEffectAdded?.Invoke(inst); // 刷新也通知
                    return;
                }
            }
            var newEffect = new BattleEffectInstance(template, source);
            Effects.Add(newEffect);
            OnEffectAdded?.Invoke(newEffect);
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

        public float TakeDamage(float rawDamage)
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
                    return reduced;
                }
                actualDamage = reduced - Shield;
                Shield = 0f;
            }

            if (actualDamage < 0f) actualDamage = 0f;
            CurrentHP -= actualDamage;
            if (CurrentHP < 0f) CurrentHP = 0f;
            return actualDamage;
        }
    }
}

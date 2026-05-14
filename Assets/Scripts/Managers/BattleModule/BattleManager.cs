using System;
using System.Collections.Generic;

namespace GameDemo.Battle
{
    /// <summary>
    /// 战斗管理器，持有双方阵形、行动队列和状态机，驱动对局流程。
    /// </summary>
    public class BattleManager
    {
        public Formation PlayerFormation { get; }
        public Formation EnemyFormation { get; }

        public ActionQueue ActionQueue { get; }
        public BattleStateMachine StateMachine { get; }

        /// <summary>当前被选中的行动单位。</summary>
        public BattleUnitInstance? SelectedUnit { get; private set; }

        /// <summary>当前被选中的技能。</summary>
        public Skill? SelectedSkill { get; private set; }

        /// <summary>玩家输入回调：传入可操控单位，返回选中的技能和目标。</summary>
        public Func<PlayableUnitInstance, (Skill? skill, BattleUnitInstance? target)> PlayerInputCallback { get; set; }

        /// <summary>自动行动回调：传入自动单位，返回选中的技能和目标。</summary>
        public Func<AutoUnitInstance, (Skill? skill, BattleUnitInstance? target)> AutoActionCallback { get; set; }

        // ==================== 外部事件（UI / 输入系统订阅）====================

        /// <summary>单位受到伤害 (unit, damage, source)。</summary>
        public event Action<BattleUnitInstance, float, BattleUnitInstance?>? OnUnitDamaged;

        /// <summary>单位阵亡，即将移出场地。</summary>
        public event Action<BattleUnitInstance>? OnUnitDied;

        /// <summary>单位被施加附加效果。</summary>
        public event Action<BattleUnitInstance, BattleEffectInstance>? OnEffectApplied;

        /// <summary>附加效果到期移除。</summary>
        public event Action<BattleUnitInstance, BattleEffectInstance>? OnEffectExpired;

        /// <summary>技能释放 (caster, skill, target)。</summary>
        public event Action<BattleUnitInstance, Skill, BattleUnitInstance>? OnSkillUsed;

        /// <summary>行动队列发生变化。</summary>
        public event Action? OnActionQueueChanged;

        /// <summary>战斗结束 (我方胜利)。</summary>
        public event Action<bool>? OnBattleEnded;

        // ==================== 外部查询 ====================

        /// <summary>当前是否等待玩家输入。</summary>
        public bool IsWaitingForPlayerInput =>
            StateMachine.IsWaitingAction && SelectedUnit is PlayableUnitInstance;

        /// <summary>当前是否等待 AI 决策。</summary>
        public bool IsWaitingForAutoAction =>
            StateMachine.IsWaitingAction && SelectedUnit is AutoUnitInstance;

        /// <summary>我方存活单位数。</summary>
        public int AliveCountPlayer => CountAlive(PlayerFormation);

        /// <summary>敌方存活单位数。</summary>
        public int AliveCountEnemy => CountAlive(EnemyFormation);

        /// <summary>判断单位所属阵形是否为我方。</summary>
        public bool IsPlayerUnit(BattleUnitInstance unit) =>
            PlayerFormation.FindUnit(unit).IsValid;

        /// <summary>获取指定单位当前可释放的技能及其所有合法目标列表。</summary>
        public List<(Skill skill, List<BattleUnitInstance> targets)> GetCastableSkills(BattleUnitInstance unit)
        {
            var result = new List<(Skill, List<BattleUnitInstance>)>();
            Formation enemyFormation = IsPlayerUnit(unit) ? EnemyFormation : PlayerFormation;

            foreach (Skill skill in unit.Skills)
            {
                var validTargets = new List<BattleUnitInstance>();
                foreach (BattleUnitInstance candidate in enemyFormation.Units)
                    if (candidate.IsAlive && (skill.CanCast == null || skill.CanCast(unit, candidate)))
                        validTargets.Add(candidate);

                if (validTargets.Count > 0)
                    result.Add((skill, validTargets));
            }
            return result;
        }

        // ==================== 构造 ====================

        public BattleManager()
        {
            PlayerFormation = new Formation();
            EnemyFormation = new Formation();
            ActionQueue = new ActionQueue();
            StateMachine = new BattleStateMachine();

            PlayerInputCallback = DefaultPlayerInput;
            AutoActionCallback = DefaultAutoAction;
        }

        // ==================== 状态入口 ====================

        public void StartBattle()
        {
            StateMachine.SetState(BattleState.BattleStart);
            SubscribeEffectEvents();
            ActionQueue.Rebuild(PlayerFormation, EnemyFormation);
            OnActionQueueChanged?.Invoke();
            StateMachine.SetState(BattleState.PreAction);
            EnterPreAction();
        }

        private void SubscribeEffectEvents()
        {
            foreach (BattleUnitInstance unit in PlayerFormation.Units)
            {
                var captured = unit;
                unit.OnEffectAdded += e => OnEffectApplied?.Invoke(captured, e);
            }
            foreach (BattleUnitInstance unit in EnemyFormation.Units)
            {
                var captured = unit;
                unit.OnEffectAdded += e => OnEffectApplied?.Invoke(captured, e);
            }
        }

        /// <summary>
        /// 单位行动前状态：重置修正 → 应用效果 → 重算属性 → 检查结束 → 检查可行动 → 进入等待。
        /// </summary>
        public void EnterPreAction()
        {
            while (true)
            {
                SelectedUnit = ActionQueue.Current;
                if (SelectedUnit == null)
                {
                    StateMachine.SetState(BattleState.BattleEnd);
                    return;
                }

                // Step 0: 推进时间
                ActionQueue.AdvanceTime();
                OnActionQueueChanged?.Invoke();

                // Step 0.5: 重置修正值
                SelectedUnit.ResetModifiers();

                // Step 1: 应用附加效果，若期间死亡则停止后续效果
                foreach (BattleEffectInstance effect in SelectedUnit.Effects)
                {
                    if (!SelectedUnit.IsAlive) break;
                    float hpBefore = SelectedUnit.CurrentHP;
                    effect.ApplyTo(SelectedUnit);
                    float damage = hpBefore - SelectedUnit.CurrentHP;
                    if (damage > 0f)
                        OnUnitDamaged?.Invoke(SelectedUnit, damage, effect.Source);
                }

                // Step 2: 重算运行时属性，清理死亡单位
                SelectedUnit.RecalculateStats();
                CleanupDeadUnits();

                // Step 3: 检查游戏结束条件
                if (CheckGameOver())
                {
                    StateMachine.SetState(BattleState.BattleEnd);
                    OnBattleEnded?.Invoke(AliveCountPlayer > 0);
                    return;
                }

                // Step 4: 已阵亡则跳过
                if (!SelectedUnit.IsAlive)
                {
                    StateMachine.SetState(BattleState.PostAction);
                    EnterPostAction();
                    return;
                }

                // Step 5: 不可行动则跳转到 PostAction
                if (!SelectedUnit.CanAct)
                {
                    StateMachine.SetState(BattleState.PostAction);
                    EnterPostAction();
                    return;
                }

                // Step 6: 进入等待行动
                StateMachine.SetState(BattleState.WaitingAction);
                return;
            }
        }

        public void EnterWaitingAction()
        {
            if (SelectedUnit == null) return;

            Skill? skill = null;
            BattleUnitInstance? target = null;

            if (SelectedUnit is PlayableUnitInstance playable)
            {
                (skill, target) = PlayerInputCallback(playable);
            }
            else if (SelectedUnit is AutoUnitInstance auto)
            {
                (skill, target) = AutoActionCallback(auto);
            }

            if (skill == null || target == null) return;

            SelectedSkill = skill;

            float hpBefore = target.CurrentHP;
            skill.Apply?.Invoke(SelectedUnit, target);
            float damage = hpBefore - target.CurrentHP;

            OnSkillUsed?.Invoke(SelectedUnit, skill, target);
            if (damage > 0f)
                OnUnitDamaged?.Invoke(target, damage, SelectedUnit);

            CleanupDeadUnits();
            StateMachine.SetState(BattleState.PostAction);
            EnterPostAction();
        }

        public void EnterPostAction()
        {
            if (SelectedUnit != null && SelectedUnit.IsAlive)
            {
                TickEffects(SelectedUnit);
            }

            CleanupDeadUnits();

            if (SelectedUnit != null)
            {
                SelectedUnit.ResetCost();
            }

            ActionQueue.Rebuild(PlayerFormation, EnemyFormation);
            OnActionQueueChanged?.Invoke();

            if (CheckGameOver())
            {
                StateMachine.SetState(BattleState.BattleEnd);
                OnBattleEnded?.Invoke(AliveCountPlayer > 0);
                return;
            }

            StateMachine.SetState(BattleState.PreAction);
            EnterPreAction();
        }

        // ==================== 效果回合 ====================

        private void TickEffects(BattleUnitInstance unit)
        {
            for (int i = unit.Effects.Count - 1; i >= 0; i--)
            {
                BattleEffectInstance effect = unit.Effects[i];
                effect.RemainingTurns--;
                if (effect.IsExpired)
                {
                    unit.Effects.RemoveAt(i);
                    OnEffectExpired?.Invoke(unit, effect);
                }
            }
            if (unit.Effects.Count > 0)
            {
                unit.ResetModifiers();
                foreach (BattleEffectInstance effect in unit.Effects)
                    if (effect.Template.StatusType != BattleEffectStatusType.Damage)
                        effect.ApplyTo(unit);
                unit.RecalculateStats();
            }
        }

        // ==================== 默认回调 ====================

        private (Skill? skill, BattleUnitInstance? target) DefaultPlayerInput(PlayableUnitInstance unit)
        {
            return FindFirstCastable(unit);
        }

        private (Skill? skill, BattleUnitInstance? target) DefaultAutoAction(AutoUnitInstance unit)
        {
            return FindFirstCastable(unit);
        }

        private (Skill? skill, BattleUnitInstance? target) FindFirstCastable(BattleUnitInstance unit)
        {
            foreach (Skill skill in unit.Skills)
            {
                BattleUnitInstance? target = FindTargetForSkill(unit, skill);
                if (target != null && (skill.CanCast == null || skill.CanCast(unit, target)))
                    return (skill, target);
            }
            return (null, null);
        }

        private BattleUnitInstance? FindTargetForSkill(BattleUnitInstance caster, Skill skill)
        {
            Formation enemyOfCaster = GetEnemyFormationOf(caster);

            switch (skill.TargetType)
            {
                case TargetType.SingleEnemy:
                case TargetType.AllEnemies:
                    return FindFirstAlive(enemyOfCaster);
                case TargetType.SingleAlly:
                case TargetType.AllAllies:
                    return caster;
                case TargetType.SingleBoth:
                case TargetType.AllBoth:
                    return FindFirstAlive(enemyOfCaster);
                default:
                    return null;
            }
        }

        private Formation GetEnemyFormationOf(BattleUnitInstance unit)
        {
            return PlayerFormation.FindUnit(unit).IsValid ? EnemyFormation : PlayerFormation;
        }

        private BattleUnitInstance? FindFirstAlive(Formation formation)
        {
            foreach (BattleUnitInstance unit in formation.Units)
                if (unit.IsAlive)
                    return unit;
            return null;
        }

        // ==================== 死亡清理 ====================

        private void CleanupDeadUnits()
        {
            CleanupDeadInFormation(PlayerFormation);
            CleanupDeadInFormation(EnemyFormation);
        }

        private void CleanupDeadInFormation(Formation formation)
        {
            foreach (BattleUnitInstance unit in formation.Units)
            {
                if (!unit.IsAlive)
                {
                    BattleSlot slot = formation.FindUnit(unit);
                    formation.RemoveUnit(slot);
                    OnUnitDied?.Invoke(unit);
                }
            }
        }

        // ==================== 游戏结束 ====================

        private bool CheckGameOver()
        {
            return CountAlive(PlayerFormation) == 0 || CountAlive(EnemyFormation) == 0;
        }

        private int CountAlive(Formation formation)
        {
            int count = 0;
            foreach (BattleUnitInstance unit in formation.Units)
                if (unit.IsAlive)
                    count++;
            return count;
        }
    }
}

using System;
using System.Collections.Generic;

namespace GameDemo.Battle
{
    public class BattleManager
    {
        public static BattleManager? Instance { get; private set; }

        public Formation PlayerFormation { get; }
        public Formation EnemyFormation { get; }

        public ActionQueue ActionQueue { get; }
        public BattleStateMachine StateMachine { get; }

        public BattleUnitInstance? SelectedUnit { get; private set; }
        public Skill? SelectedSkill { get; set; }

        private List<(Skill skill, BattleUnitInstance target)>? _pendingActions;

        /// <summary>当前待执行的行动列表（只读），供表现层协程读取。</summary>
        public IReadOnlyList<(Skill skill, BattleUnitInstance target)>? PendingActions => _pendingActions;

        // ==================== 玩家/AI 输入事件 ====================

        /// <summary>轮到玩家操作时触发。UI 应展示技能选择界面，玩家确认后调用 SubmitPlayerAction。</summary>
        public event Action<PlayableUnitInstance>? OnWaitingForPlayerInput;

        /// <summary>轮到自动单位行动时触发。AI 系统应处理并调用 SubmitAutoAction。</summary>
        public event Action<AutoUnitInstance>? OnWaitingForAutoAction;

        /// <summary>外部提交玩家选择的技能与单个目标，进入 Acting 阶段。扩散/群体目标由内部展开。</summary>
        public void SubmitPlayerAction(Skill skill, BattleUnitInstance target)
        {
            if (SelectedUnit is not PlayableUnitInstance) return;
            _pendingActions = new List<(Skill, BattleUnitInstance)> { (skill, target) };
            StateMachine.SetState(BattleState.Acting);
        }

        /// <summary>外部提交自动单位的行动列表（每项含单个目标），进入 Acting 阶段。</summary>
        public void SubmitAutoAction(List<(Skill, BattleUnitInstance target)> actions)
        {
            if (SelectedUnit is not AutoUnitInstance) return;
            _pendingActions = actions;
            StateMachine.SetState(BattleState.Acting);
        }

        // ==================== 外部事件（UI / 输入系统订阅）====================

        public event Action<BattleUnitInstance, float, BattleUnitInstance?, bool>? OnUnitDamaged;
        public event Action<BattleUnitInstance, float, BattleUnitInstance?>? OnUnitHealed;
        public event Action<BattleUnitInstance>? OnUnitDied;
        public event Action<BattleUnitInstance, BattleEffectInstance>? OnEffectApplied;
        public event Action<BattleUnitInstance, BattleEffectInstance>? OnEffectExpired;
        public event Action<BattleUnitInstance, Skill, List<BattleUnitInstance>>? OnSkillUsed;
        public event Action? OnActionQueueChanged;
        public event Action<bool>? OnBattleEnded;

        /// <summary>供表现层在协程中手动触发技能使用事件。</summary>
        public void RaiseSkillUsed(BattleUnitInstance caster, Skill skill, List<BattleUnitInstance> targets)
            => OnSkillUsed?.Invoke(caster, skill, targets);

        // ==================== 外部查询 ====================

        public bool IsWaitingForPlayerInput =>
            StateMachine.IsWaitingAction && SelectedUnit is PlayableUnitInstance;

        public bool IsWaitingForAutoAction =>
            StateMachine.IsWaitingAction && SelectedUnit is AutoUnitInstance;

        public int AliveCountPlayer => CountAlive(PlayerFormation);
        public int AliveCountEnemy => CountAlive(EnemyFormation);

        public bool IsPlayerUnit(BattleUnitInstance unit) =>
            PlayerFormation.FindUnit(unit).IsValid;

        /// <summary>返回所有可释放的 (技能, 单个候选目标) 对。群体技能的扩散由 ApplyAction 内部处理。</summary>
        public List<(Skill skill, BattleUnitInstance target)> GetCastableSkills(BattleUnitInstance unit)
        {
            var result = new List<(Skill, BattleUnitInstance)>();

            foreach (Skill skill in unit.Skills)
            {
                var candidates = FindCandidateTargetsForSkill(unit, skill);
                foreach (var target in candidates)
                {
                    if (skill.CanCast(unit, target))
                        result.Add((skill, target));
                }
            }
            return result;
        }

        // ==================== 构造 ====================

        public BattleManager()
        {
            Instance = this;
            PlayerFormation = new Formation();
            EnemyFormation = new Formation();
            ActionQueue = new ActionQueue();
            StateMachine = new BattleStateMachine();
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
                unit.OnEffectRemoved += e => OnEffectExpired?.Invoke(captured, e);
                unit.OnDamaged += (u, dmg, isTrue, src) => OnUnitDamaged?.Invoke(u, dmg, src, isTrue);
                unit.OnHealed += (u, heal, src) => OnUnitHealed?.Invoke(u, heal, src);
            }
            foreach (BattleUnitInstance unit in EnemyFormation.Units)
            {
                var captured = unit;
                unit.OnEffectAdded += e => OnEffectApplied?.Invoke(captured, e);
                unit.OnEffectRemoved += e => OnEffectExpired?.Invoke(captured, e);
                unit.OnDamaged += (u, dmg, isTrue, src) => OnUnitDamaged?.Invoke(u, dmg, src, isTrue);
                unit.OnHealed += (u, heal, src) => OnUnitHealed?.Invoke(u, heal, src);
            }
        }

        public void EnterPreAction()
        {
            while (true)
            {
                SelectedUnit = ActionQueue.Current;
                if (SelectedUnit == null)
                {
                    Instance = null;
                    StateMachine.SetState(BattleState.BattleEnd);
                    return;
                }

                // Step 0: 推进时间
                ActionQueue.AdvanceTime();
                OnActionQueueChanged?.Invoke();

                // Step 1: 刷新非持续伤害效果
                RefreshPersistentEffects(SelectedUnit);

                // Step 2: 单独应用持续伤害类效果（伤害事件由 TakeDamage 内部触发）
                foreach (BattleEffectInstance effect in SelectedUnit.Effects)
                {
                    if (!SelectedUnit.IsAlive) break;
                    if (effect.Template.StatusType != BattleEffectStatusType.Damage) continue;
                    effect.ApplyTo(SelectedUnit);
                }

                // Step 3: 重算运行时属性，清理死亡单位
                SelectedUnit.RecalculateStats();
                CleanupDeadUnits();

                // Step 3: 检查游戏结束条件
                if (CheckGameOver())
                {
                    Instance = null;
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
                EnterWaitingAction();
                return;
            }
        }

        public void EnterWaitingAction()
        {
            if (SelectedUnit == null) return;

            if (SelectedUnit is PlayableUnitInstance playable)
                OnWaitingForPlayerInput?.Invoke(playable);
            else if (SelectedUnit is AutoUnitInstance auto)
                OnWaitingForAutoAction?.Invoke(auto);
            // 不在此处进入 Acting——由 SubmitPlayerAction / SubmitAutoAction 接管
        }

        public void EnterActing()
        {
            if (SelectedUnit == null) return;
            if (_pendingActions == null) return;

            foreach (var (skill, pickedTarget) in _pendingActions)
            {
                if (!SelectedUnit.IsAlive) break;
                if (pickedTarget == null) continue;
                if (!skill.CanCast(SelectedUnit, pickedTarget)) continue;

                SelectedSkill = skill;
                skill.Cast(SelectedUnit);
                skill.Apply(SelectedUnit, pickedTarget);
                OnSkillUsed?.Invoke(SelectedUnit, skill,
                    new List<BattleUnitInstance> { pickedTarget });
                RefreshAllUnits();
                CleanupDeadUnits();
            }

            _pendingActions = null;
            StateMachine.SetState(BattleState.PostAction);
            EnterPostAction();
        }

        public void EnterPostAction()
        {
            if (SelectedUnit != null && SelectedUnit.IsAlive)
            {
                TickEffects(SelectedUnit);
                RefreshPersistentEffects(SelectedUnit);
            }

            CleanupDeadUnits();

            if (SelectedUnit != null)
                SelectedUnit.ResetCost();

            ActionQueue.Rebuild(PlayerFormation, EnemyFormation);
            OnActionQueueChanged?.Invoke();

            if (CheckGameOver())
            {
                Instance = null;
                StateMachine.SetState(BattleState.BattleEnd);
                OnBattleEnded?.Invoke(AliveCountPlayer > 0);
                return;
            }

            StateMachine.SetState(BattleState.PreAction);
            EnterPreAction();
        }

        // ==================== 效果管理 ====================

        /// <summary>
        /// 效果倒计时，仅减少剩余回合、移除过期效果。
        /// </summary>
        private void TickEffects(BattleUnitInstance unit)
        {
            for (int i = unit.Effects.Count - 1; i >= 0; i--)
            {
                BattleEffectInstance effect = unit.Effects[i];
                if (effect.RemainingTurns > 0)
                    effect.RemainingTurns--;
                if (effect.IsExpired)
                {
                    unit.Effects.RemoveAt(i);
                    OnEffectExpired?.Invoke(unit, effect);
                }
            }
        }

        /// <summary>
        /// 重置修正 → 应用所有非持续伤害效果 → 重算属性。
        /// </summary>
        private void RefreshPersistentEffects(BattleUnitInstance unit)
        {
            if (!unit.IsAlive) return;
            unit.ResetModifiers();
            foreach (BattleEffectInstance effect in unit.Effects)
                if (effect.Template.StatusType != BattleEffectStatusType.Damage)
                    effect.ApplyTo(unit);
            unit.RecalculateStats();
        }

        /// <summary>
        /// 刷新双方所有存活单位的非持续伤害效果。
        /// </summary>
        public void RefreshAllUnits()
        {
            foreach (BattleUnitInstance unit in PlayerFormation.Units)
                RefreshPersistentEffects(unit);
            foreach (BattleUnitInstance unit in EnemyFormation.Units)
                RefreshPersistentEffects(unit);
        }

        /// <summary>返回技能可选的所有单个候选目标，供 UI/AI 选择。群体技能的扩散由 ApplyAction 内部处理。</summary>
        private List<BattleUnitInstance> FindCandidateTargetsForSkill(BattleUnitInstance caster, Skill skill)
        {
            Formation enemyOfCaster = GetEnemyFormationOf(caster);
            Formation allyOfCaster = IsPlayerUnit(caster) ? PlayerFormation : EnemyFormation;

            switch (skill.TargetType)
            {
                case TargetType.SingleEnemy:
                case TargetType.AllEnemies:
                    return FindAllAlive(enemyOfCaster);
                case TargetType.SingleAlly:
                case TargetType.AllAllies:
                    return FindAllAlive(allyOfCaster);
                case TargetType.SingleBoth:
                    return FindAllAlive(enemyOfCaster);
                case TargetType.AllBoth:
                    var all = FindAllAlive(enemyOfCaster);
                    all.AddRange(FindAllAlive(allyOfCaster));
                    return all;
                case TargetType.SingleSelf:
                    return caster.IsAlive ? new List<BattleUnitInstance> { caster } : new List<BattleUnitInstance>();
                default:
                    return new List<BattleUnitInstance>();
            }
        }


        private Formation GetEnemyFormationOf(BattleUnitInstance unit)
        {
            return PlayerFormation.FindUnit(unit).IsValid ? EnemyFormation : PlayerFormation;
        }

        private List<BattleUnitInstance> FindAllAlive(Formation formation)
        {
            var result = new List<BattleUnitInstance>();
            foreach (BattleUnitInstance unit in formation.Units)
                if (unit.IsAlive)
                    result.Add(unit);
            return result;
        }

        // ==================== 死亡清理 ====================

        public void CleanupDeadUnits()
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

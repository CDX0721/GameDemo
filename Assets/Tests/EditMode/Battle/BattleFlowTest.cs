#if UNITY_INCLUDE_TESTS
using System;
using System.IO;
using System.Text;
using GameDemo.Battle;
using NUnit.Framework;
using UnityEngine;

namespace GameDemo.Tests.EditMode
{
    public class BattleFlowTest
    {
        private Formation _playerFormation = null!;
        private Formation _enemyFormation = null!;
        private ActionQueue _actionQueue = null!;
        private BattleStateMachine _stateMachine = null!;
        private BattleUnitInstance? _selectedUnit;
        private int _frame;
        private readonly StringBuilder _log = new StringBuilder();

        private void Log(string msg)
        {
            _log.AppendLine(msg);
            Debug.Log(msg);
        }

        [Test]
        public void RunFullBattle()
        {
            SetupBattle();

            const int maxFrames = 500;

            while (_frame < maxFrames)
            {
                _frame++;

                if (_stateMachine.CurrentState == BattleState.BattleEnd)
                    break;

                switch (_stateMachine.CurrentState)
                {
                    case BattleState.BattleStart:
                        DoBattleStart();
                        break;
                    case BattleState.PreAction:
                        DoPreAction();
                        break;
                    case BattleState.WaitingAction:
                        DoWaitingAction();
                        break;
                    case BattleState.PostAction:
                        DoPostAction();
                        break;
                }
            }

            if (_frame >= maxFrames)
                Log("[警告] 达到最大帧数上限，战斗可能未正常结束");

            Log($"[Frame {_frame}] ========== 战斗结束 ==========");
            Log($"  我方存活: {CountAlive(_playerFormation)}  敌方存活: {CountAlive(_enemyFormation)}");

            string logPath = Path.Combine(Application.dataPath, "Tests", "EditMode", "Battle", "BattleFlowTest.log");
            File.WriteAllText(logPath, _log.ToString());
            Debug.Log($"日志已保存到: {logPath}");

            Assert.Less(_frame, maxFrames, "战斗未在最大帧数内结束，可能存在死循环");
        }

        private void DoBattleStart()
        {
            Log($"[Frame {_frame}] >>> BattleStart");
            _actionQueue.Rebuild(_playerFormation, _enemyFormation);
            _stateMachine.SetState(BattleState.PreAction);
        }

        private void DoPreAction()
        {
            Log($"[Frame {_frame}] >>> PreAction");

            _selectedUnit = _actionQueue.Current;
            if (_selectedUnit == null)
            {
                _stateMachine.SetState(BattleState.BattleEnd);
                return;
            }

            LogActionQueue();

            float t = _selectedUnit.ActionValue;
            Log($"  选中: {_selectedUnit.DisplayName} ({_selectedUnit.Id})  HP={_selectedUnit.CurrentHP:F0}  ATK={_selectedUnit.CurrentAttack:F0}");

            _actionQueue.AdvanceTime();
            Log($"  [时间推进] t={t:F2}，所有单位 RemainingCost -= t * Speed");

            _selectedUnit.ResetModifiers();

            foreach (BattleEffectInstance effect in _selectedUnit.Effects)
            {
                if (!_selectedUnit.IsAlive) break;
                float hpBefore = _selectedUnit.CurrentHP;
                Log($"  [效果生效] [{effect.Template.DisplayName}] 作用于 {_selectedUnit.DisplayName}  (剩余 {effect.RemainingTurns} 回合  x{effect.CurrentStackCount})");
                effect.ApplyTo(_selectedUnit);
                float delta = hpBefore - _selectedUnit.CurrentHP;
                if (delta > 0f)
                    Log($"    → 造成 {delta:F0} 点伤害，HP: {hpBefore:F0} → {_selectedUnit.CurrentHP:F0}");
            }

            _selectedUnit.RecalculateStats();
            CleanupDeadUnits();

            if (CheckGameOver())
            {
                _stateMachine.SetState(BattleState.BattleEnd);
                return;
            }

            if (!_selectedUnit.IsAlive)
            {
                Log($"  {_selectedUnit.DisplayName} 已阵亡，跳过行动 → 进入 PostAction");
                _stateMachine.SetState(BattleState.PostAction);
                return;
            }

            if (!_selectedUnit.CanAct)
            {
                Log($"  {_selectedUnit.DisplayName} 不可行动（被控制），跳过行动 → 进入 PostAction");
                _stateMachine.SetState(BattleState.PostAction);
                return;
            }

            _stateMachine.SetState(BattleState.WaitingAction);
        }

        private void DoWaitingAction()
        {
            Log($"[Frame {_frame}] >>> WaitingAction");

            if (_selectedUnit == null) return;

            var (skill, target) = FindFirstCastable(_selectedUnit, GetEnemyFormationOf(_selectedUnit));

            if (skill == null || target == null)
            {
                Log($"  {_selectedUnit.DisplayName} 无可用技能，跳过");
                _stateMachine.SetState(BattleState.PostAction);
                return;
            }

            float hpBefore = target.CurrentHP;
            Log($"  {_selectedUnit.DisplayName} 对 {target.DisplayName} 使用 [{skill.DisplayName}]  (目标 HP: {hpBefore:F0})");

            skill.Apply?.Invoke(_selectedUnit, target);

            float hpAfter = target.CurrentHP;
            Log($"  → 伤害 {hpBefore - hpAfter:F0}，{target.DisplayName} HP: {hpAfter:F0}");

            CleanupDeadUnits();

            _stateMachine.SetState(BattleState.PostAction);
        }

        private void DoPostAction()
        {
            Log($"[Frame {_frame}] >>> PostAction");

            if (_selectedUnit != null && _selectedUnit.IsAlive)
                TickEffects(_selectedUnit);

            CleanupDeadUnits();

            _selectedUnit?.ResetCost();
            _actionQueue.Rebuild(_playerFormation, _enemyFormation);

            if (CheckGameOver())
            {
                _stateMachine.SetState(BattleState.BattleEnd);
                return;
            }

            _stateMachine.SetState(BattleState.PreAction);
        }

        private void TickEffects(BattleUnitInstance unit)
        {
            for (int i = unit.Effects.Count - 1; i >= 0; i--)
            {
                BattleEffectInstance effect = unit.Effects[i];
                effect.RemainingTurns--;
                Log($"  [效果回合] [{effect.Template.DisplayName}] 剩余 {effect.RemainingTurns} 回合");
                if (effect.IsExpired)
                {
                    unit.Effects.RemoveAt(i);
                    Log($"  [效果移除] [{effect.Template.DisplayName}] 从 {unit.DisplayName} 移除");
                }
            }
            if (unit.Effects.Count > 0)
            {
                unit.ResetModifiers();
                foreach (BattleEffectInstance effect in unit.Effects)
                {
                    if (effect.Template.StatusType == BattleEffectStatusType.Damage) continue;
                    Log($"  [效果重算] [{effect.Template.DisplayName}] 重新作用于 {unit.DisplayName}");
                    effect.ApplyTo(unit);
                }
                unit.RecalculateStats();
                Log($"  [效果重算结果] {unit.DisplayName} CanAct={unit.CanAct}  HP={unit.CurrentHP:F0}");
            }
        }

        private void LogActionQueue()
        {
            Log("  --- 当前行动队列 ---");
            for (int i = 0; i < _actionQueue.Count; i++)
            {
                BattleUnitInstance? u = _actionQueue[i];
                if (u != null)
                    Log($"  [{i}] {u.DisplayName} ({u.Id})  行动值={u.ActionValue:F2}  剩余代价={u.RemainingCost:F1}  速度={u.CurrentSpeed:F0}");
            }
        }

        private (Skill? skill, BattleUnitInstance? target) FindFirstCastable(
            BattleUnitInstance caster, Formation enemyFormation)
        {
            foreach (Skill skill in caster.Skills)
            {
                BattleUnitInstance? target = FindTarget(skill.TargetType, enemyFormation);
                if (target != null && (skill.CanCast == null || skill.CanCast(caster, target)))
                    return (skill, target);
            }
            return (null, null);
        }

        private BattleUnitInstance? FindTarget(TargetType targetType, Formation enemyFormation)
        {
            switch (targetType)
            {
                case TargetType.SingleEnemy:
                case TargetType.AllEnemies:
                    return FindFirstAlive(enemyFormation);
                case TargetType.SingleAlly:
                case TargetType.AllAllies:
                    return _selectedUnit;
                default:
                    return null;
            }
        }

        private BattleUnitInstance? FindFirstAlive(Formation formation)
        {
            foreach (BattleUnitInstance unit in formation.Units)
                if (unit.IsAlive)
                    return unit;
            return null;
        }

        private Formation GetEnemyFormationOf(BattleUnitInstance unit)
        {
            return _playerFormation.FindUnit(unit).IsValid ? _enemyFormation : _playerFormation;
        }

        private void CleanupDeadUnits()
        {
            CleanupDeadInFormation(_playerFormation);
            CleanupDeadInFormation(_enemyFormation);
        }

        private void CleanupDeadInFormation(Formation formation)
        {
            foreach (BattleUnitInstance unit in formation.Units)
            {
                if (!unit.IsAlive)
                {
                    BattleSlot slot = formation.FindUnit(unit);
                    formation.RemoveUnit(slot);
                    Log($"  [死亡移除] {unit.DisplayName} 阵亡，移出场地");
                }
            }
        }

        private bool CheckGameOver()
        {
            return CountAlive(_playerFormation) == 0 || CountAlive(_enemyFormation) == 0;
        }

        private int CountAlive(Formation formation)
        {
            int count = 0;
            foreach (BattleUnitInstance unit in formation.Units)
                if (unit.IsAlive) count++;
            return count;
        }

        private void SetupBattle()
        {
            Log("========== 初始化战斗 ==========");

            _playerFormation = new Formation();
            _enemyFormation = new Formation();
            _actionQueue = new ActionQueue();
            _stateMachine = new BattleStateMachine();
            _stateMachine.SetState(BattleState.BattleStart);

            var pt1 = new BattleUnit("p_warrior", "战士",   attack: 80f,  defense: 30f, hp: 500f, speed: 60f,  mana: 100f);
            var pt2 = new BattleUnit("p_mage",    "法师",   attack: 120f, defense: 15f, hp: 300f, speed: 40f,  mana: 200f);
            var pt3 = new BattleUnit("p_priest",  "牧师",   attack: 50f,  defense: 20f, hp: 350f, speed: 50f,  mana: 150f);

            var p1 = CreateUnitWithSkill(pt1, 100f, "attack", "攻击",
                (caster, target) => target.TakeDamage(caster.CurrentAttack));

            var p2 = CreateUnitWithSkill(pt2, 120f, "poison", "毒刃",
                (caster, target) =>
                {
                    var dot = new BattleEffect("dot", "中毒", BattleEffectType.Negative, BattleEffectStatusType.Damage,
                        (unit, stacks) => { unit.CurrentHP -= 20f * stacks; }, initialTurns: 2, maxStackCount: 3);
                    target.AddEffect(dot, caster);
                    Log($"  [附加效果] {target.DisplayName} 被施加 [{dot.DisplayName}]");
                });

            var p3 = CreateUnitWithSkill(pt3, 110f, "stun", "眩晕",
                (caster, target) =>
                {
                    var stun = new BattleEffect("stun", "眩晕", BattleEffectType.Negative, BattleEffectStatusType.Control,
                        (unit, stacks) => { unit.CanAct = false; }, initialTurns: 1, maxStackCount: 1);
                    target.AddEffect(stun, caster);
                    Log($"  [附加效果] {target.DisplayName} 被施加 [{stun.DisplayName}]");
                });

            _playerFormation.PlaceUnit(p1, new BattleSlot(1, 0));
            _playerFormation.PlaceUnit(p2, new BattleSlot(0, 1));
            _playerFormation.PlaceUnit(p3, new BattleSlot(2, 1));

            Log($"  我方: {p1.DisplayName}(HP:{p1.MaxHP:F0} ATK:{p1.CurrentAttack:F0}) " +
                $"{p2.DisplayName}(HP:{p2.MaxHP:F0} ATK:{p2.CurrentAttack:F0}) " +
                $"{p3.DisplayName}(HP:{p3.MaxHP:F0} ATK:{p3.CurrentAttack:F0})");

            var et1 = new BattleUnit("e_goblin", "哥布林",     attack: 60f,  defense: 10f, hp: 200f, speed: 70f,  mana: 50f);
            var et2 = new BattleUnit("e_archer", "哥布林射手", attack: 70f,  defense: 8f,  hp: 180f, speed: 80f,  mana: 50f);
            var et3 = new BattleUnit("e_troll",  "巨魔",       attack: 100f, defense: 40f, hp: 600f, speed: 30f,  mana: 80f);

            var e1 = CreateUnitWithSkill(et1, 100f, "attack", "攻击",
                (caster, target) => target.TakeDamage(caster.CurrentAttack));
            var e2 = CreateUnitWithSkill(et2, 100f, "attack", "攻击",
                (caster, target) => target.TakeDamage(caster.CurrentAttack));
            var e3 = CreateUnitWithSkill(et3, 130f, "attack", "攻击",
                (caster, target) => target.TakeDamage(caster.CurrentAttack));

            _enemyFormation.PlaceUnit(e1, new BattleSlot(1, 1));
            _enemyFormation.PlaceUnit(e2, new BattleSlot(0, 2));
            _enemyFormation.PlaceUnit(e3, new BattleSlot(1, 2));

            Log($"  敌方: {e1.DisplayName}(HP:{e1.MaxHP:F0} ATK:{e1.CurrentAttack:F0}) " +
                $"{e2.DisplayName}(HP:{e2.MaxHP:F0} ATK:{e2.CurrentAttack:F0}) " +
                $"{e3.DisplayName}(HP:{e3.MaxHP:F0} ATK:{e3.CurrentAttack:F0})");

            Log("========== 战斗开始 ==========\n");
        }

        private AutoUnitInstance CreateUnitWithSkill(BattleUnit template, float initialCost,
            string skillId, string skillName, Action<BattleUnitInstance, BattleUnitInstance> apply)
        {
            var unit = new AutoUnitInstance(template, initialCost);
            unit.Skills.Add(new Skill(skillId, skillName, SkillType.SingleAttack, TargetType.SingleEnemy)
            {
                CanCast = null,
                Apply = apply
            });
            return unit;
        }
    }
}
#endif

using System.Collections;
using System.Collections.Generic;
using GameDemo.Battle;
using UnityEngine;

/// <summary>
/// BattleManager 的 MonoBehaviour 驱动器。
/// 协程负责动画编排，Update 负责状态机推进和输入暂停。
/// </summary>
public class BattleDriver : MonoBehaviour
{
    public BattleManager Manager { get; private set; } = null!;
    private BattleSceneBootstrapper _bootstrapper = null!;

    private bool _isAnimating;

    // ==================== Update ====================

    void Update()
    {
        if (Manager == null) return;
        if (Manager.StateMachine.IsBattleEnd) return;
        if (_isAnimating) return;

        switch (Manager.StateMachine.CurrentState)
        {
            case BattleState.BattleStart:   break;
            case BattleState.PreAction:     Manager.EnterPreAction(); break;
            case BattleState.WaitingAction: Manager.EnterWaitingAction(); break;
            case BattleState.Acting:        StartCoroutine(DoActingWithAnimations()); break;
            case BattleState.PostAction:    Manager.EnterPostAction(); break;
        }
    }

    /// <summary>带表现层的战斗初始化。仅创建实例+布阵，不绑定 UnitView。</summary>
    public void Setup(BattleFieldDef fieldDef, Dictionary<string, BattleUnitDef> unitDefs,
        BattleSceneBootstrapper bootstrapper)
    {
        _bootstrapper = bootstrapper;

        Manager = new BattleManager();
        Manager.OnWaitingForPlayerInput += OnWaitingForPlayerInputEvent;
        Manager.OnWaitingForAutoAction += OnWaitingForAutoActionEvent;
        Manager.OnBattleEnded += OnBattleEnded;
        Manager.OnSkillUsed += OnSkillUsed;
        Manager.OnUnitDamaged += OnUnitDamaged;
        Manager.OnUnitDied += OnUnitDied;

        foreach (var p in fieldDef.PlayerUnits)
            if (unitDefs.TryGetValue(p.id, out var def))
                PlaceUnit(def, p, Manager.PlayerFormation);
        foreach (var p in fieldDef.EnemyUnits)
            if (unitDefs.TryGetValue(p.id, out var def))
                PlaceUnit(def, p, Manager.EnemyFormation);

        // StartBattle() must be called separately after UI is ready
    }

    public void StartBattle()
    {
        Manager.StartBattle();
    }

    // ==================== 输入事件处理 ====================

    private void OnWaitingForPlayerInputEvent(PlayableUnitInstance unit)
    {
        StartCoroutine(AutoResolveDelay(0.5f, () =>
        {
            var skills = Manager.GetCastableSkills(unit);
            if (skills.Count > 0)
                Manager.SubmitPlayerAction(skills[0].skill, skills[0].target);
        }));
    }

    private void OnWaitingForAutoActionEvent(AutoUnitInstance unit)
    {
        StartCoroutine(AutoResolveDelay(0.8f, () =>
        {
            var skills = Manager.GetCastableSkills(unit);
            if (skills.Count > 0)
            {
                SkillCatalog.EvaluatingUnit = unit;
                var best = skills[0];
                float bestPrio = best.skill.Priority?.Invoke(best.target) ?? 0f;
                for (int i = 1; i < skills.Count; i++)
                {
                    float prio = skills[i].skill.Priority?.Invoke(skills[i].target) ?? 0f;
                    if (prio > bestPrio) { best = skills[i]; bestPrio = prio; }
                }
                SkillCatalog.EvaluatingUnit = null;
                Manager.SubmitAutoAction(new List<(Skill, BattleUnitInstance)> { best });
            }
        }));
    }

    private IEnumerator AutoResolveDelay(float delay, System.Action action)
    {
        yield return new WaitForSeconds(delay);
        action();
    }

    // ==================== Acting 协程（动画编排） ====================

    private IEnumerator DoActingWithAnimations()
    {
        _isAnimating = true;

        var pending = Manager.PendingActions;
        if (pending == null || pending.Count == 0)
        {
            FinishActing();
            yield break;
        }

        var caster = Manager.SelectedUnit;
        if (caster == null)
        {
            FinishActing();
            yield break;
        }

        _bootstrapper.UnitViews.TryGetValue(caster, out var casterView);

        foreach (var (skill, pickedTarget) in pending)
        {
            if (!caster.IsAlive) break;
            if (pickedTarget == null) continue;
            if (!skill.CanCast(caster, pickedTarget)) continue;

            // 1. 播放攻击动画 + 技能特效
            bool animationDone = false;
            var effectFrames = GetSkillFxFrames(skill);

            if (casterView != null)
            {
                casterView.PlayAttackWithEffect(effectFrames, () => animationDone = true);
            }
            else
            {
                animationDone = true;
            }

            // 2. 等待动画完成（兜底 1.2s）
            float timer = 0f;
            while (!animationDone && timer < 1.2f)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            // 3. 执行技能效果（Apply 内部处理目标展开）
            Manager.SelectedSkill = skill;
            var allUnits = GetAllLivingUnits();
            var hpBefore = new Dictionary<BattleUnitInstance, float>();
            foreach (var u in allUnits) hpBefore[u] = u.CurrentHP;

            skill.Cast(caster);
            skill.Apply(caster, pickedTarget);
            Manager.RaiseSkillUsed(caster, skill,
                new List<BattleUnitInstance> { pickedTarget });
            Manager.RefreshAllUnits();
            Manager.CleanupDeadUnits();

            // 逐单位触发伤害事件（驱动血条更新 + 伤害数字）
            foreach (var u in allUnits)
            {
                float damage = hpBefore.TryGetValue(u, out float before) ? before - u.CurrentHP : 0f;
                if (damage > 0f)
                    Manager.RaiseUnitDamaged(u, damage, caster);
                if (_bootstrapper.UnitViews.TryGetValue(u, out var view))
                {
                    if (damage > 0f || !u.IsAlive)
                        view.PlayHitFlash();
                }
            }
            yield return new WaitForSeconds(0.3f);

            // 5. 死亡单位隐藏
            foreach (var u in allUnits)
            {
                if (!u.IsAlive && _bootstrapper.UnitViews.TryGetValue(u, out var deadView))
                    deadView.gameObject.SetActive(false);
            }
        }

        FinishActing();
    }

    private void FinishActing()
    {
        Manager.StateMachine.SetState(BattleState.PostAction);
        _isAnimating = false;
    }

    // ==================== 技能特效查找 ====================

    private Sprite[] GetSkillFxFrames(Skill skill)
    {
        // 技能/效果动画暂不接入
        return System.Array.Empty<Sprite>();
    }

    // ==================== 事件响应 ====================

    private void OnBattleEnded(bool playerWin)
        => Debug.Log($"战斗结束，玩家{(playerWin ? "胜利" : "败北")}");

    private void OnSkillUsed(BattleUnitInstance caster, Skill skill,
        List<BattleUnitInstance> targets)
        => Debug.Log($"{caster.DisplayName} 使用了 [{skill.DisplayName}]");

    private void OnUnitDamaged(BattleUnitInstance unit, float damage, BattleUnitInstance? source)
    {
        Debug.Log($"{unit.DisplayName} 受到 {damage:F0} 伤害，HP: {unit.CurrentHP:F0}/{unit.MaxHP:F0}");

        // 刷新血条
        if (_bootstrapper.HPBars.TryGetValue(unit, out var hpBar))
            hpBar.SetHP(unit.CurrentHP, unit.MaxHP);

        // 弹出伤害数字（不阻塞，独立协程）
        if (_bootstrapper.UnitViews.TryGetValue(unit, out var view) && _bootstrapper.DamageSpawner != null)
        {
            Vector3 pos = view.transform.position + Vector3.up * 0.5f;
            _bootstrapper.DamageSpawner.SpawnDamage(pos, Mathf.RoundToInt(damage));
        }
    }

    private void OnUnitDied(BattleUnitInstance unit)
    {
        Debug.Log($"{unit.DisplayName} 阵亡");
        // 死亡时血条归零
        if (_bootstrapper.HPBars.TryGetValue(unit, out var hpBar))
            hpBar.SetHP(0f, unit.MaxHP);
    }

    void OnDestroy()
    {
        if (Manager == null) return;
        Manager.OnWaitingForPlayerInput -= OnWaitingForPlayerInputEvent;
        Manager.OnWaitingForAutoAction -= OnWaitingForAutoActionEvent;
        Manager.OnBattleEnded -= OnBattleEnded;
        Manager.OnSkillUsed -= OnSkillUsed;
        Manager.OnUnitDamaged -= OnUnitDamaged;
        Manager.OnUnitDied -= OnUnitDied;
    }

    // ==================== 辅助 ====================

    private List<BattleUnitInstance> GetAllLivingUnits()
    {
        var result = new List<BattleUnitInstance>();
        foreach (var u in Manager.PlayerFormation.Units)
            if (u.IsAlive) result.Add(u);
        foreach (var u in Manager.EnemyFormation.Units)
            if (u.IsAlive) result.Add(u);
        return result;
    }

    private void PlaceUnit(BattleUnitDef def, UnitPlacementDef placement, Formation formation)
    {
        var template = new BattleUnit(placement.id, def.DisplayName,
            def.Attack, def.Defense, def.HP, def.Speed, def.Mana);
        foreach (var skillId in def.InnateSills)
            template.InnateSkills.Add(SkillCatalog.Get(skillId));

        var unit = new AutoUnitInstance(template, placement.initialCost);
        formation.PlaceUnit(unit, new BattleSlot(placement.row - 1, placement.col - 1));
    }

}

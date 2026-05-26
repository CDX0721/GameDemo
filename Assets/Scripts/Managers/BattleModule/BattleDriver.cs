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
    public void Setup(BattleUnitConfig[] playerConfigs, BattleUnitConfig[] enemyConfigs,
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

        foreach (var cfg in playerConfigs)
            PlaceUnit(cfg, Manager.PlayerFormation);
        foreach (var cfg in enemyConfigs)
            PlaceUnit(cfg, Manager.EnemyFormation);

        // StartBattle() must be called separately after UI is ready
    }

    public void StartBattle()
    {
        Manager.StartBattle();
    }

    // ==================== 输入事件处理 ====================

    private void OnWaitingForPlayerInputEvent(PlayableUnitInstance unit)
    {
        // 玩家输入事件：UI 层通过 BattlePanel 监听并展示技能选择。
        // 此处提供兜底自动解决（无 UI 时也能运行）。
        StartCoroutine(AutoResolveDelay(0.5f, () =>
        {
            var skills = Manager.GetCastableSkills(unit);
            if (skills.Count > 0)
                Manager.SubmitPlayerAction(skills[0].skill, skills[0].targets);
        }));
    }

    private void OnWaitingForAutoActionEvent(AutoUnitInstance unit)
    {
        // AI 自动行动：收集所有可释放技能并提交。
        StartCoroutine(AutoResolveDelay(0.8f, () =>
        {
            var skills = Manager.GetCastableSkills(unit);
            if (skills.Count > 0)
                Manager.SubmitAutoAction(skills);
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

        foreach (var (skill, targets) in pending)
        {
            if (!caster.IsAlive) break;
            if (targets.Count == 0) continue;
            if (!skill.CanCast(targets)) continue;

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

            // 3. 执行技能效果（记录每目标受伤前后的 HP）
            Manager.SelectedSkill = skill;
            var hpBefore = new Dictionary<BattleUnitInstance, float>();
            foreach (var t in targets) hpBefore[t] = t.CurrentHP;

            skill.Apply(targets);
            Manager.RaiseSkillUsed(caster, skill, targets);
            Manager.RefreshAllUnits();
            Manager.CleanupDeadUnits();

            // 逐目标触发伤害事件（驱动血条更新 + 伤害数字）
            foreach (var t in targets)
            {
                float damage = hpBefore[t] - t.CurrentHP;
                if (damage > 0f)
                    Manager.RaiseUnitDamaged(t, damage, caster);
            }

            // 4. 目标受击
            foreach (var target in targets)
            {
                if (_bootstrapper.UnitViews.TryGetValue(target, out var targetView))
                    targetView.PlayHitFlash();
            }
            yield return new WaitForSeconds(0.3f);

            // 5. 死亡单位隐藏
            foreach (var target in targets)
            {
                if (!target.IsAlive && _bootstrapper.UnitViews.TryGetValue(target, out var deadView))
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
        if (_bootstrapper == null) return System.Array.Empty<Sprite>();

        var caster = Manager.SelectedUnit;
        if (caster == null) return System.Array.Empty<Sprite>();

        foreach (var cfg in _bootstrapper.AllUnitConfigs)
        {
            if (cfg.Id != caster.Id || cfg.Skills == null) continue;
            foreach (var sc in cfg.Skills)
                if (sc.Id == skill.Id)
                    return _bootstrapper.GetSkillEffectFrames(sc.PerformanceFxId);
        }
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

    private void PlaceUnit(BattleUnitConfig config, Formation formation)
    {
        var template = config.CreateTemplate();
        var unit = new AutoUnitInstance(template, initialCost: config.InitialCost);
        // 构造函数已复制 template.InnateSkills → unit.Skills，只需附加行为逻辑
        foreach (var skill in unit.Skills)
            AttachDefaultBehavior(skill, unit);

        formation.PlaceUnit(unit, new BattleSlot(config.Row, config.Col));
    }

    /// <summary>为 SkillConfig 创建的空壳技能挂载默认伤害/治疗逻辑。</summary>
    private static void AttachDefaultBehavior(Skill skill, BattleUnitInstance caster)
    {
        switch (skill.SkillType)
        {
            case SkillType.SingleAttack:
            case SkillType.Spread:
            case SkillType.AoE:
                skill.ApplyActions.Add(targets =>
                {
                    foreach (var t in targets)
                        if (t.IsAlive)
                            t.TakeDamage(caster.CurrentAttack);
                });
                break;

            case SkillType.Healing:
                skill.ApplyActions.Add(targets =>
                {
                    foreach (var t in targets)
                    {
                        if (!t.IsAlive) continue;
                        float healed = caster.CurrentAttack * 0.5f;
                        t.CurrentHP = Mathf.Min(t.CurrentHP + healed, t.MaxHP);
                    }
                });
                break;
        }
    }

}

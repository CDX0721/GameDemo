using System;
using System.Collections;
using System.Collections.Generic;
using GameDemo.Audio;
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

    /// <summary>设为 true 时所有单位自动战斗（包括 Player 单位）。</summary>
    public bool ForceAutoAll { get; set; }

    // BGM
    private string _bgmClipPath = null!;
    private float _bgmLoopIn, _bgmLoopOut, _bgmVolumeDb;
    private bool _hasBgm;

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
            case BattleState.WaitingAction: break; // event fires once from EnterPreAction; wait for submit
            case BattleState.Acting:        StartCoroutine(DoActingWithAnimations()); break;
            case BattleState.PostAction:    Manager.EnterPostAction(); break;
        }
    }

    /// <summary>带表现层的战斗初始化。仅创建实例+布阵，不绑定 UnitView。</summary>
    public void Setup(BattleFieldDef fieldDef, List<UnitPlacementDef> playerUnits,
        Dictionary<string, BattleUnitDef> unitDefs, BattleSceneBootstrapper bootstrapper)
    {
        _bootstrapper = bootstrapper;
        _bgmStarted = false;

        Manager = new BattleManager();
        Manager.OnWaitingForPlayerInput += OnWaitingForPlayerInputEvent;
        Manager.OnWaitingForAutoAction += OnWaitingForAutoActionEvent;
        Manager.OnBattleEnded += OnBattleEnded;
        Manager.OnSkillUsed += OnSkillUsed;
        Manager.OnUnitDamaged += OnUnitDamaged;
        Manager.OnUnitHealed += OnUnitHealed;
        Manager.OnUnitDied += OnUnitDied;
        Manager.OnEffectApplied += OnEffect;
        Manager.OnEffectExpired += OnEffect;
        Manager.StateMachine.OnStateChanged += OnBattleStateChanged;

        foreach (var p in playerUnits)
            if (unitDefs.TryGetValue(p.id, out var def))
                PlaceUnit(def, p, Manager.PlayerFormation);
        foreach (var p in fieldDef.EnemyUnits)
            if (unitDefs.TryGetValue(p.id, out var def))
                PlaceUnit(def, p, Manager.EnemyFormation);

        // StartBattle() must be called separately after UI is ready
    }

    /// <summary>设置 BGM。Bootstrapper 在 Setup 后调用。</summary>
    public void SetBGM(string clipPath, float loopIn, float loopOut, float volumeDb)
    {
        _bgmClipPath = clipPath;
        _bgmLoopIn = loopIn;
        _bgmLoopOut = loopOut;
        _bgmVolumeDb = volumeDb;
        _hasBgm = true;
    }

    private bool _bgmStarted;

    public void StartBattle()
    {
        Manager.StartBattle();
    }

    private void OnBattleStateChanged(BattleState prev, BattleState next)
    {
        if (!_bgmStarted && _hasBgm && next == BattleState.PreAction)
        {
            _bgmStarted = true;
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayBGM(_bgmClipPath, _bgmLoopIn, _bgmLoopOut, _bgmVolumeDb);
        }
    }

    // ==================== 输入事件处理 ====================

    private void OnWaitingForPlayerInputEvent(PlayableUnitInstance unit)
    {
        // Player input is handled by BattlePanel UI (BuildSkills → SelectSkill → OnConfirm).
        // Do NOT auto-submit here.
    }

    private void OnWaitingForAutoActionEvent(AutoUnitInstance unit)
    {
        StartCoroutine(AutoResolveDelay(0.8f, () =>
        {
            var skills = Manager.GetCastableSkills(unit);
            if (skills.Count == 0)
            {
                Debug.Log($"[AutoAction] {unit.DisplayName} 无可用技能，跳过行动");
                Manager.SubmitAutoAction(new List<(Skill, BattleUnitInstance)>());
                return;
            }

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
        if (pending == null || pending.Count == 0) { FinishActing(); yield break; }

        var caster = Manager.SelectedUnit;
        if (caster == null) { FinishActing(); yield break; }

        _bootstrapper.UnitViews.TryGetValue(caster, out var casterView);

        foreach (var (skill, pickedTarget) in pending)
        {
            if (!caster.IsAlive) break;
            if (pickedTarget == null) continue;
            if (!skill.CanCast(caster, pickedTarget)) continue;

            int animRunning = 0;
            var effectFrames = GetSkillFxFrames(skill);

            // 1. 施法者播放攻击动画
            if (casterView != null)
            {
                animRunning++;
                casterView.PlayAttack(() => animRunning--);
            }

            // 2. 攻击动画 0.6s 后执行技能效果
            yield return new WaitForSeconds(0.6f);

            BattleSkillContext.PendingSkillAnimations = null;
            BattleSkillContext.PendingSFX = null;
            Manager.SelectedSkill = skill;
            var allUnits = GetAllLivingUnits();

            skill.Cast(caster);
            skill.Apply(caster, pickedTarget);
            Manager.RaiseSkillUsed(caster, skill,
                new List<BattleUnitInstance> { pickedTarget });
            Manager.RefreshAllUnits();
            Manager.CleanupDeadUnits();

            // 3. 播放技能特效动画（按 ApplyActions 注册的目标和延迟）
            var entries = BattleSkillContext.PendingSkillAnimations;
            var sfxEntries = BattleSkillContext.PendingSFX;
            if (entries != null && entries.Count > 0)
            {
                entries.Sort((a, b) => a.Delay.CompareTo(b.Delay));
                float elapsed = 0f;
                foreach (var e in entries)
                {
                    float stepDelay = e.Delay - elapsed;
                    if (stepDelay > 0f) yield return new WaitForSeconds(stepDelay);
                    elapsed = e.Delay;

                    _bootstrapper.UnitViews.TryGetValue(e.Target, out var tv);
                    if (tv != null && effectFrames.Length > 0)
                    {
                        animRunning++;
                        tv.PlayEffect(effectFrames, () => animRunning--);
                    }

                    PlayDedupedSFX(sfxEntries, e.Delay);
                }
            }
            // 未注册动画目标时回退到 pickedTarget（单体技能兼容）
            else if (pickedTarget.IsAlive)
            {
                _bootstrapper.UnitViews.TryGetValue(pickedTarget, out var tv);
                if (tv != null && effectFrames.Length > 0)
                {
                    animRunning++;
                    tv.PlayEffect(effectFrames, () => animRunning--);
                }

                if (sfxEntries != null && sfxEntries.Count > 0)
                    PlayDedupedSFX(sfxEntries, 0f);
                else
                    AudioManager.Instance?.PlaySFX($"Audio/SFX/{skill.Id}");
            }

            // 4. 等待所有动画完成才能进入 PostAction（超时兜底 5s）
            float waitTimeout = 5f;
            while (animRunning > 0 && waitTimeout > 0f)
            {
                waitTimeout -= Time.deltaTime;
                yield return null;
            }
            if (animRunning > 0)
                Debug.LogWarning($"[BattleDriver] 动画等待超时 ({skill.DisplayName})，强制进入 PostAction");

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

    // ==================== SFX ====================

    /// <summary>播放指定 delay 处已去重的音效（同 delay 同 skillId 仅播一次）。</summary>
    private static void PlayDedupedSFX(List<SkillSFXEntry>? sfxEntries, float delay)
    {
        if (sfxEntries == null || AudioManager.Instance == null) return;
        var played = new HashSet<string>();
        foreach (var s in sfxEntries)
        {
            if (Mathf.Abs(s.Delay - delay) < 0.01f && played.Add(s.SkillId))
                AudioManager.Instance.PlaySFX($"Audio/SFX/{s.SkillId}");
        }
    }

    // ==================== 技能特效查找 ====================

    private Sprite[] GetSkillFxFrames(Skill skill)
    {
        return _bootstrapper.GetSkillEffectFrames(skill.Id + "_Play");
    }

    // ==================== 事件响应 ====================

    private void OnBattleEnded(bool playerWin)
    {
        Debug.Log($"战斗结束，玩家{(playerWin ? "胜利" : "败北")}");
        if (AudioManager.Instance != null)
            AudioManager.Instance.StopBGM();
    }

    private void OnSkillUsed(BattleUnitInstance caster, Skill skill,
        List<BattleUnitInstance> targets)
        => Debug.Log($"{caster.DisplayName} 使用了 [{skill.DisplayName}]");

    private void OnEffect(BattleUnitInstance unit, BattleEffectInstance effect)
    {
        if (_bootstrapper.UnitViews.TryGetValue(unit, out var view))
            view.SyncEffectLayers(id => _bootstrapper.GetEffectFrames(id));
    }

    private void OnUnitDamaged(BattleUnitInstance unit, float damage, BattleUnitInstance? source, bool isTrueDamage)
    {
        Debug.Log($"{unit.DisplayName} 受到 {damage:F0} 伤害，HP: {unit.CurrentHP:F0}/{unit.MaxHP:F0}");

        // 刷新血条（立即）
        if (_bootstrapper.HPBars.TryGetValue(unit, out var hpBar))
            hpBar.SetHP(unit.CurrentHP, unit.MaxHP);

        // 受击闪烁 + 伤害数字延迟 0.9s
        _bootstrapper.UnitViews.TryGetValue(unit, out var view);
        StartCoroutine(DelayedDamageVisual(0.9f, () =>
        {
            if (view != null) view.PlayHitFlash();
            if (_bootstrapper.DamageSpawner != null && view != null)
            {
                Vector3 pos = view.transform.position + Vector3.up * 0.5f;
                int dmg = Mathf.RoundToInt(damage);
                if (isTrueDamage)
                    _bootstrapper.DamageSpawner.SpawnTrueDamage(pos, dmg);
                else
                    _bootstrapper.DamageSpawner.SpawnDamage(pos, dmg);
            }
        }));
    }

    private void OnUnitHealed(BattleUnitInstance unit, float amount, BattleUnitInstance? source)
    {
        Debug.Log($"{unit.DisplayName} 回复 {amount:F0} HP，HP: {unit.CurrentHP:F0}/{unit.MaxHP:F0}");

        // 刷新血条（立即）
        if (_bootstrapper.HPBars.TryGetValue(unit, out var hpBar))
            hpBar.SetHP(unit.CurrentHP, unit.MaxHP);

        // 治疗数字延迟 0.9s
        _bootstrapper.UnitViews.TryGetValue(unit, out var view);
        StartCoroutine(DelayedHealVisual(0.9f, () =>
        {
            if (_bootstrapper.DamageSpawner != null && view != null)
            {
                Vector3 pos = view.transform.position + Vector3.up * 0.5f;
                _bootstrapper.DamageSpawner.SpawnHeal(pos, Mathf.RoundToInt(amount));
            }
        }));
    }

    private IEnumerator DelayedDamageVisual(float delay, Action action)
    {
        yield return new WaitForSeconds(delay);
        action();
    }

    private IEnumerator DelayedHealVisual(float delay, Action action)
    {
        yield return new WaitForSeconds(delay);
        action();
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
        Manager.OnUnitHealed -= OnUnitHealed;
        Manager.OnUnitDied -= OnUnitDied;
        Manager.OnEffectApplied -= OnEffect;
        Manager.OnEffectExpired -= OnEffect;
        Manager.StateMachine.OnStateChanged -= OnBattleStateChanged;
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
        foreach (var sd in def.InnateSills)
            template.InnateSkills.Add(SkillCatalog.Get(sd.id, sd.level));
        if (placement.attachSkills != null)
        {
            foreach (var sd in placement.attachSkills)
            {
                if (!string.IsNullOrEmpty(sd.id))
                    template.InnateSkills.Add(SkillCatalog.Get(sd.id, sd.level));
            }
        }

        BattleUnitInstance unit = def.ControlType == "Player"
            ? new PlayableUnitInstance(template, placement.initialCost)
            : new AutoUnitInstance(template, placement.initialCost);
        formation.PlaceUnit(unit, new BattleSlot(placement.row - 1, placement.col - 1));
    }

}

using System;
using GameDemo.Battle;
using UnityEngine;

/// <summary>
/// 单个战斗单位的视觉表现。包含身体动画（idle/attack）和技能特效叠加层。
/// </summary>
public class UnitView : MonoBehaviour
{
    [SerializeField] private SpriteAnimator _bodyAnimator = null!;
    [SerializeField] private SpriteAnimator _effectAnimator = null!;
    [SerializeField] private HPBar _hpBar = null!;

    public HPBar HPBarComponent => _hpBar;

    private Sprite[] _idleFrames = Array.Empty<Sprite>();
    private Sprite[] _attackFrames = Array.Empty<Sprite>();

    public BattleUnitInstance? Model { get; private set; }

    /// <summary>设置动画资源，并开始播放 idle。</summary>
    public void Setup(BattleUnitInstance model, Sprite[] idleFrames, Sprite[] attackFrames)
    {
        Model = model;
        _idleFrames = idleFrames;
        _attackFrames = attackFrames;
        _effectAnimator.SetVisible(false);
        PlayIdle();
    }

    /// <summary>播放 idle 循环动画。</summary>
    public void PlayIdle()
    {
        _bodyAnimator.Play(_idleFrames, totalDuration: 1f, looping: true);
    }

    /// <summary>
    /// 播放攻击动画 + 技能特效动画（各 1 秒）。
    /// 两个动画都完成后回调 onComplete。
    /// </summary>
    public void PlayAttackWithEffect(Sprite[] effectFrames, Action? onComplete)
    {
        int completed = 0;
        void OnOneDone()
        {
            completed++;
            if (completed >= 2)
                onComplete?.Invoke();
        }

        // 身体：攻击动画（一次性）→ 结束后回到 idle
        _bodyAnimator.Play(_attackFrames, totalDuration: 1f, looping: false, onComplete: () =>
        {
            PlayIdle();
            OnOneDone();
        });

        // 特效：显示 → 播放技能特效 → 隐藏
        _effectAnimator.SetVisible(true);
        _effectAnimator.Play(effectFrames, totalDuration: 1f, looping: false, onComplete: () =>
        {
            _effectAnimator.SetVisible(false);
            OnOneDone();
        });
    }

    /// <summary>播放受击闪烁（简单实现：短暂关闭再开启 renderer）。</summary>
    public void PlayHitFlash()
    {
        if (_bodyAnimator?.Renderer != null)
            StartCoroutine(HitFlashRoutine());

        System.Collections.IEnumerator HitFlashRoutine()
        {
            var sr = _bodyAnimator.Renderer;
            for (int i = 0; i < 3; i++)
            {
                sr.enabled = false;
                yield return new WaitForSeconds(0.08f);
                sr.enabled = true;
                yield return new WaitForSeconds(0.08f);
            }
        }
    }
}

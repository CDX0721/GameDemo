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

    /// <summary>按列号设置渲染层级，列号越大越靠前遮挡上方单位。</summary>
    public void SetSortingOrder(int col)
    {
        int baseOrder = col * 100;
        if (_bodyAnimator.Renderer != null)
            _bodyAnimator.Renderer.sortingOrder = baseOrder;
        if (_effectAnimator.Renderer != null)
            _effectAnimator.Renderer.sortingOrder = baseOrder + 1;
    }

    /// <summary>设置单位朝向。true = 朝右（翻转 X），false = 朝左。</summary>
    public void SetFacingRight(bool right)
    {
        if (_bodyAnimator.Renderer != null)
            _bodyAnimator.Renderer.flipX = right;
    }

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
        _bodyAnimator.Play(_idleFrames, frameDuration: 0.2f, looping: true);
    }

    /// <summary>仅播放攻击动画（无特效），完成后回调。</summary>
    public void PlayAttack(Action? onComplete)
    {
        _bodyAnimator.Play(_attackFrames, frameDuration: 0.2f, looping: false, onComplete: () =>
        {
            PlayIdle();
            onComplete?.Invoke();
        });
    }

    /// <summary>仅在目标身上播放技能特效动画，完成后回调并隐藏特效层。</summary>
    public void PlayEffect(Sprite[] effectFrames, Action? onComplete)
    {
        if (effectFrames == null || effectFrames.Length == 0)
        {
            onComplete?.Invoke();
            return;
        }
        _effectAnimator.transform.localScale = Vector3.one * 2f;
        _effectAnimator.SetVisible(true);
        _effectAnimator.Play(effectFrames, frameDuration: 0.2f, looping: false, onComplete: () =>
        {
            _effectAnimator.SetVisible(false);
            _effectAnimator.transform.localScale = Vector3.one;
            onComplete?.Invoke();
        });
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
        _bodyAnimator.Play(_attackFrames, frameDuration: 0.2f, looping: false, onComplete: () =>
        {
            PlayIdle();
            OnOneDone();
        });

        // 特效：显示 → 播放技能特效 → 隐藏
        _effectAnimator.SetVisible(true);
        _effectAnimator.Play(effectFrames, frameDuration: 0.2f, looping: false, onComplete: () =>
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

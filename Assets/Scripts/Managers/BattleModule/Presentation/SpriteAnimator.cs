using System;
using UnityEngine;

/// <summary>
/// 精灵帧动画播放器。不支持同时播放多个动画——新 Play 会中断旧动画。
/// </summary>
public class SpriteAnimator : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _renderer = null!;

    private Sprite[] _frames = Array.Empty<Sprite>();

    void Awake()
    {
        if (_renderer == null)
            _renderer = GetComponent<SpriteRenderer>();
    }
    private int _frameIndex;
    private float _frameDuration;
    private float _timer;
    private bool _looping;
    private bool _playing;
    private Action? _onComplete;

    public bool IsPlaying => _playing;
    public SpriteRenderer Renderer => _renderer;

    /// <summary>
    /// 播放动画。
    /// </summary>
    /// <param name="frames">帧序列</param>
    /// <param name="frameDuration">每帧持续时长（秒）</param>
    /// <param name="looping">是否循环</param>
    /// <param name="onComplete">播放完毕回调（仅非循环时触发）</param>
    public void Play(Sprite[] frames, float frameDuration, bool looping, Action? onComplete = null)
    {
        if (frames == null || frames.Length == 0)
        {
            onComplete?.Invoke();
            return;
        }

        _frames = frames;
        _frameDuration = frameDuration;
        _looping = looping;
        _onComplete = onComplete;
        _frameIndex = 0;
        _timer = 0f;
        _playing = true;
        ApplyFrame();
    }

    public void Stop()
    {
        _playing = false;
        _frames = Array.Empty<Sprite>();
    }

    public void SetVisible(bool visible)
    {
        if (_renderer != null)
            _renderer.enabled = visible;
    }

    void Update()
    {
        if (!_playing || _frames.Length == 0) return;

        _timer += Time.deltaTime;
        while (_timer >= _frameDuration)
        {
            _timer -= _frameDuration;
            _frameIndex++;

            if (_frameIndex >= _frames.Length)
            {
                if (_looping)
                {
                    _frameIndex = 0;
                    ApplyFrame();
                }
                else
                {
                    _playing = false;
                    _onComplete?.Invoke();
                    return;
                }
            }
        }
        ApplyFrame();
    }

    private void ApplyFrame()
    {
        if (_renderer != null && _frameIndex < _frames.Length)
            _renderer.sprite = _frames[_frameIndex];
    }
}

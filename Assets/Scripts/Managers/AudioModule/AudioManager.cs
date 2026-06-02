using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameDemo.Audio
{
    /// <summary>
    /// 音频管理器。接管所有 BGM / SFX 请求。
    /// BGM 支持循环出入点（loop in / loop out）和响度偏移（dB）。
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; } = null!;

        [SerializeField] private int _sfxPoolSize = 8;

        private AudioSource _bgmSource = null!;
        private float _loopInSeconds;
        private float _loopOutSeconds;
        private bool _hasLoop;
        private float _bgmVolumeDb;
        private float _masterBgmVolume = 1f;
        private float _masterSfxVolume = 1f;

        private readonly Queue<AudioSource> _sfxPool = new();
        private readonly List<AudioSource> _activeSfx = new();

        // ==================== 生命周期 ====================

        void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (FindFirstObjectByType<AudioListener>() == null)
            {
                var cam = Camera.main;
                if (cam != null)
                    cam.gameObject.AddComponent<AudioListener>();
                else
                    gameObject.AddComponent<AudioListener>();
            }

            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.loop = false;
            _bgmSource.playOnAwake = false;

            for (int i = 0; i < _sfxPoolSize; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.loop = false;
                src.playOnAwake = false;
                _sfxPool.Enqueue(src);
            }
        }

        void Update()
        {
            if (!_hasLoop || _bgmSource == null || !_bgmSource.isPlaying || _bgmSource.clip == null)
                return;

            if (_bgmSource.time >= _loopOutSeconds)
                _bgmSource.time = _loopInSeconds;

            // 回收播放完毕的 SFX
            for (int i = _activeSfx.Count - 1; i >= 0; i--)
            {
                if (!_activeSfx[i].isPlaying)
                {
                    _sfxPool.Enqueue(_activeSfx[i]);
                    _activeSfx.RemoveAt(i);
                }
            }
        }

        // ==================== BGM ====================

        /// <summary>播放 BGM，从头开始。支持循环出入点和响度偏移。</summary>
        public void PlayBGM(string clipPath, float loopIn, float loopOut, float volumeOffsetDb = 0f)
        {
            var clip = AssetManager.Instance.Load<AudioClip>(clipPath);
            if (clip == null)
            {
                Debug.LogWarning($"[AudioManager] BGM clip not found: {clipPath}");
                return;
            }

            StopBGM();

            _bgmSource.clip = clip;
            _bgmVolumeDb = volumeOffsetDb;
            _bgmSource.volume = DecibelToLinear(volumeOffsetDb) * _masterBgmVolume;
            _loopInSeconds = loopIn;
            _loopOutSeconds = Mathf.Min(loopOut, clip.length);
            _hasLoop = loopOut > loopIn && loopIn >= 0f;

            Debug.Log($"[AudioManager] PlayBGM: clip={clip.name} length={clip.length:F1}s loop={_loopInSeconds:F2}→{_loopOutSeconds:F2} enabled={_hasLoop}");

            _bgmSource.time = 0f;
            _bgmSource.Play();
        }

        /// <summary>停止 BGM。</summary>
        public void StopBGM()
        {
            _hasLoop = false;
            if (_bgmSource != null)
                _bgmSource.Stop();
        }

        /// <summary>设置 BGM 响度偏移（dB）。正=增强，负=减弱。</summary>
        public void SetBGMVolumeOffset(float db)
        {
            _bgmVolumeDb = db;
            if (_bgmSource != null)
                _bgmSource.volume = DecibelToLinear(db) * _masterBgmVolume;
        }

        /// <summary>设置 BGM 主音量 (0~1)。</summary>
        public void SetMasterBgmVolume(float linear)
        {
            _masterBgmVolume = Mathf.Clamp01(linear);
            if (_bgmSource != null)
                _bgmSource.volume = DecibelToLinear(_bgmVolumeDb) * _masterBgmVolume;
        }

        /// <summary>设置 SFX 主音量 (0~1)。</summary>
        public void SetMasterSfxVolume(float linear)
        {
            _masterSfxVolume = Mathf.Clamp01(linear);
        }

        public float MasterBgmVolume => _masterBgmVolume;
        public float MasterSfxVolume => _masterSfxVolume;

        // ==================== SFX ====================

        /// <summary>播放一次性音效。</summary>
        public void PlaySFX(string clipPath, float volumeDb = 0f)
        {
            var clip = AssetManager.Instance.Load<AudioClip>(clipPath);
            if (clip == null)
            {
                Debug.LogWarning($"[AudioManager] SFX clip not found: {clipPath}");
                return;
            }

            AudioSource src;
            if (_sfxPool.Count > 0)
                src = _sfxPool.Dequeue();
            else
                src = gameObject.AddComponent<AudioSource>();

            src.clip = clip;
            src.volume = DecibelToLinear(volumeDb) * _masterSfxVolume;
            src.Play();
            _activeSfx.Add(src);
        }

        // ==================== 工具 ====================

        /// <summary>dB → 线性倍率 (0dB = 1.0)。</summary>
        public static float DecibelToLinear(float db) => Mathf.Pow(10f, db / 20f);

        /// <summary>
        /// 解析时间码字符串（HH:MM:SS 或 HH:MM:SS:FF）。
        /// 帧部分按指定帧率折算（默认 24fps）。
        /// </summary>
        public static float ParseTimecode(string tc, int frameRate = 24)
        {
            if (string.IsNullOrEmpty(tc)) return 0f;
            var parts = tc.Split(':');
            float result = 0f;
            if (parts.Length >= 1) result += int.Parse(parts[0]) * 3600f;
            if (parts.Length >= 2) result += int.Parse(parts[1]) * 60f;
            if (parts.Length >= 3) result += float.Parse(parts[2]);
            if (parts.Length >= 4 && frameRate > 0) result += float.Parse(parts[3]) / frameRate;
            return result;
        }
    }
}

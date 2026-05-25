using System.Collections.Generic;
using GameDemo;
using GameDemo.Battle;
using UnityEngine;

/// <summary>
/// 战斗场景启动器。通过 AssetManager 加载所有资源，构建 UnitView 并绑定 BattleUnitInstance。
/// </summary>
[RequireComponent(typeof(BattleDriver))]
public class BattleSceneBootstrapper : MonoBehaviour
{
    [Header("配置表")]
    [SerializeField] private BattleSceneConfig _config = null!;
    [SerializeField] private AnimationCache _animationCache = null!;

    [Header("背景")]
    [SerializeField] private SpriteRenderer _backgroundRenderer = null!;
    [SerializeField] private bool _fitBackgroundToCamera = true;

    [Header("UnitView")]
    [SerializeField] private Transform _unitParent = null!;
    [SerializeField] private GameObject _unitViewPrefab = null!;

    [Header("伤害数字")]
    [SerializeField] private DamageNumberSpawner _damageSpawner = null!;

    /// <summary>BattleUnitInstance → UnitView 一对一映射。</summary>
    public Dictionary<BattleUnitInstance, UnitView> UnitViews { get; } = new();
    /// <summary>BattleUnitInstance → HPBar 一对一映射。</summary>
    public Dictionary<BattleUnitInstance, HPBar> HPBars { get; } = new();
    public DamageNumberSpawner DamageSpawner => _damageSpawner;

    /// <summary>所有单位配置（供 BattleDriver 查技能 FxId）。</summary>
    internal BattleUnitConfig[] AllUnitConfigs =>
        Combine(_config?.PlayerUnits, _config?.EnemyUnits);

    // 运行时动画缓存：key → Sprite[]
    private Dictionary<string, Sprite[]> _spriteCache = new();
    private Dictionary<string, UnitView> _pendingViews = new();
    private BattleDriver _driver = null!;

    void Start()
    {
        _driver = GetComponent<BattleDriver>();

        if (_config == null || _animationCache == null)
        {
            Debug.LogError("BattleSceneConfig 或 AnimationCache 未配置");
            return;
        }

        LoadBackground();
        EnsureDamageCanvas();
        BuildPendingViews();
        _driver.Setup(_config.PlayerUnits, _config.EnemyUnits, this);
        BindInstancesToViews(_driver.Manager.PlayerFormation);
        BindInstancesToViews(_driver.Manager.EnemyFormation);
        _pendingViews.Clear();
    }

    // ==================== 动画帧加载 ====================

    /// <summary>通过 AssetManager 加载单位动画帧。</summary>
    public (Sprite[] idle, Sprite[] attack) GetUnitFrames(string unitId)
    {
        return (
            LoadFrames($"{unitId}_idle"),
            LoadFrames($"{unitId}_attack")
        );
    }

    /// <summary>通过 AssetManager 加载技能特效帧。</summary>
    public Sprite[] GetSkillEffectFrames(string fxId)
    {
        return LoadFrames(fxId);
    }

    private Sprite[] LoadFrames(string key)
    {
        if (_spriteCache.TryGetValue(key, out var cached))
            return cached;

        string path = _animationCache.GetPath(key);
        if (path == null)
            return System.Array.Empty<Sprite>();

        var all = AssetManager.Instance.LoadAll<Sprite>(path);
        if (all == null || all.Length == 0)
            return System.Array.Empty<Sprite>();

        // Resources.LoadAll 返回的首个元素可能是主纹理（名称与文件同名），过滤掉
        string baseName = System.IO.Path.GetFileNameWithoutExtension(path);
        var frames = new List<Sprite>();
        foreach (var s in all)
        {
            if (s != null && s.name.StartsWith(baseName + "_"))
                frames.Add(s);
        }
        frames.Sort((a, b) =>
        {
            int ua = a.name.LastIndexOf('_'), ub = b.name.LastIndexOf('_');
            int na = ua >= 0 && int.TryParse(a.name.Substring(ua + 1), out int x) ? x : 0;
            int nb = ub >= 0 && int.TryParse(b.name.Substring(ub + 1), out int y) ? y : 0;
            return na.CompareTo(nb);
        });

        var result = frames.ToArray();
        _spriteCache[key] = result;
        return result;
    }

    // ==================== 背景加载 ====================

    private void LoadBackground()
    {
        if (_backgroundRenderer == null || string.IsNullOrEmpty(_config.BackgroundPath))
            return;

        var bg = AssetManager.Instance.Load<Sprite>(_config.BackgroundPath);
        if (bg == null) return;

        _backgroundRenderer.sprite = bg;
        if (_fitBackgroundToCamera) FitBackgroundToOrthoCamera(bg);
    }

    private void FitBackgroundToOrthoCamera(Sprite bg)
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return;
        float scale = cam.orthographicSize * 2f / bg.bounds.size.y;
        _backgroundRenderer.transform.localScale = new Vector3(scale, scale, 1f);
    }

    // ==================== UnitView 构建 ====================

    private void BuildPendingViews()
    {
        if (_unitParent == null)
        {
            var go = new GameObject("BattleField");
            _unitParent = go.transform;
        }
        CreatePendingFor(_config.PlayerUnits, isPlayer: true);
        CreatePendingFor(_config.EnemyUnits, isPlayer: false);
    }

    private void CreatePendingFor(BattleUnitConfig[] configs, bool isPlayer)
    {
        foreach (var cfg in configs)
        {
            GameObject go = (_unitViewPrefab != null)
                ? Instantiate(_unitViewPrefab, _unitParent)
                : CreateDefaultUnitViewGo(cfg.Id);
            go.name = $"UnitView_{cfg.Id}";

            var unitView = go.GetComponent<UnitView>();
            if (unitView == null) unitView = go.AddComponent<UnitView>();

            float x = isPlayer ? -4f + cfg.Col * 1.5f : 4f - (2 - cfg.Col) * 1.5f;
            float y = 2f - cfg.Row * 1.8f;
            go.transform.position = new Vector3(x, y, 0);

            _pendingViews[cfg.Id] = unitView;
        }
    }

    private void BindInstancesToViews(Formation formation)
    {
        foreach (var instance in formation.Units)
        {
            if (!_pendingViews.TryGetValue(instance.Id, out var view))
            {
                Debug.LogWarning($"未找到 UnitView for {instance.Id}");
                continue;
            }

            var (idle, attack) = GetUnitFrames(instance.Id);
            Debug.Log($"[Boot] 绑定 {instance.Id}: idle={idle.Length}帧 attack={attack.Length}帧 HP={instance.CurrentHP:F0}/{instance.MaxHP:F0}");
            view.Setup(instance, idle, attack);
            UnitViews[instance] = view;

            var hpBar = view.GetComponentInChildren<HPBar>();
            if (hpBar != null)
            {
                hpBar.Setup(instance.MaxHP);
                hpBar.SetHP(instance.CurrentHP, instance.MaxHP);
                HPBars[instance] = hpBar;
            }
        }
    }

    private GameObject CreateDefaultUnitViewGo(string unitId)
    {
        var go = new GameObject($"UnitView_{unitId}");

        var bodyGo = new GameObject("Body");
        bodyGo.transform.SetParent(go.transform, false);
        bodyGo.AddComponent<SpriteRenderer>().sortingOrder = 1;
        var bodyAnim = bodyGo.AddComponent<SpriteAnimator>();

        var fxGo = new GameObject("Effect");
        fxGo.transform.SetParent(go.transform, false);
        fxGo.AddComponent<SpriteRenderer>().sortingOrder = 2;
        var fxAnim = fxGo.AddComponent<SpriteAnimator>();

        var hpBarGo = new GameObject("HPBar");
        hpBarGo.transform.SetParent(go.transform, false);
        var hpBar = hpBarGo.AddComponent<HPBar>();

        var uv = go.AddComponent<UnitView>();
        SetPrivateField(uv, "_bodyAnimator", bodyAnim);
        SetPrivateField(uv, "_effectAnimator", fxAnim);
        SetPrivateField(uv, "_hpBar", hpBar);

        return go;
    }

    // ==================== 辅助 ====================

    private void EnsureDamageCanvas()
    {
        if (_damageSpawner != null) return;
        var canvasGo = new GameObject("DamageCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 999;
        canvasGo.transform.position = Vector3.zero;
        _damageSpawner = canvasGo.AddComponent<DamageNumberSpawner>();
        SetPrivateField(_damageSpawner, "_damageNumberPrefab", DamageNumber.CreateDefaultPrefab());
    }

    private static T[] Combine<T>(T[] a, T[] b)
    {
        var r = new T[(a?.Length ?? 0) + (b?.Length ?? 0)];
        int i = 0;
        if (a != null) foreach (var x in a) r[i++] = x;
        if (b != null) foreach (var x in b) r[i++] = x;
        return r;
    }

    internal static void SetPrivateField(object target, string fieldName, object value)
    {
        target.GetType()
            .GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(target, value);
    }
}

using System.Collections.Generic;
using GameDemo;
using GameDemo.Battle;
using GameDemo.UI;
using GameDemo.UI.Panels;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 战斗场景启动器。从 JSON 配置加载战斗信息，构建 UnitView 并绑定 BattleUnitInstance。
/// </summary>
[RequireComponent(typeof(BattleDriver))]
public class BattleSceneBootstrapper : MonoBehaviour
{
    [Header("背景")]
    [SerializeField] private SpriteRenderer _backgroundRenderer = null!;

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

    private Dictionary<string, Sprite[]> _spriteCache = new();
    private Dictionary<string, UnitView> _pendingViews = new();
    private BattleDriver _driver = null!;
    private Dictionary<string, BattleUnitDef> _unitDefs = null!;

    void Start()
    {
        _driver = GetComponent<BattleDriver>();

        var (fieldDef, unitDefs) = BattleConfigLoader.Load("TestBattleFiled");
        if (fieldDef == null || unitDefs == null)
        {
            Debug.LogError("[Bootstrapper] Failed to load battle configs.");
            return;
        }
        _unitDefs = unitDefs;

        if (!string.IsNullOrEmpty(fieldDef.BackGround))
            LoadBackground(fieldDef.BackGround);
        EnsureDamageCanvas();
        BuildUnitViews(fieldDef.PlayerUnits, isPlayer: true);
        BuildUnitViews(fieldDef.EnemyUnits, isPlayer: false);
        _driver.Setup(fieldDef, unitDefs, this);
        BindInstancesToViews(_driver.Manager.PlayerFormation, isPlayer: true);
        BindInstancesToViews(_driver.Manager.EnemyFormation, isPlayer: false);
        _pendingViews.Clear();

        InitializeUI();
    }

    private void InitializeUI()
    {
        var uiCanvasGO = new GameObject("UICanvas");
        Canvas canvas = uiCanvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = uiCanvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        uiCanvasGO.AddComponent<GraphicRaycaster>();
        uiCanvasGO.AddComponent<UIRoot>();

        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
        }

        var menuGO = new GameObject("MainMenuPanel", typeof(RectTransform));
        menuGO.transform.SetParent(canvas.transform, false);
        Stretch(menuGO.GetComponent<RectTransform>());
        menuGO.AddComponent<MainMenuPanel>();

        var bpPrefab = Resources.Load<GameObject>("UI/BattlePanel");
        if (bpPrefab != null)
        {
            var bpGO = Instantiate(bpPrefab, canvas.transform);
            bpGO.name = "BattlePanel";
            var bp = bpGO.GetComponent<BattlePanel>();
            if (bp == null) bp = bpGO.AddComponent<BattlePanel>();
            bp.BindBattleManager(_driver.Manager);
        }

        UIManager.Instance.Show<MainMenuPanel>();
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    // ==================== 动画帧加载 ====================

    /// <summary>通过 BattleUnitDef 加载单位动画帧（4×2 精灵表，0.2s/帧）。</summary>
    public (Sprite[] idle, Sprite[] attack) GetUnitFrames(string unitId)
    {
        if (_unitDefs == null || !_unitDefs.TryGetValue(unitId, out var def))
            return (System.Array.Empty<Sprite>(), System.Array.Empty<Sprite>());

        string basePath = "Art/Sprites/Units/";
        return (
            LoadFramesDirect(basePath + def.IdleAnimation),
            LoadFramesDirect(basePath + def.AttackAnimation)
        );
    }

    /// <summary>技能特效帧（暂不接入）。</summary>
    public Sprite[] GetSkillEffectFrames(string fxId)
    {
        return System.Array.Empty<Sprite>();
    }

    private Sprite[] LoadFramesDirect(string resourcePath)
    {
        string key = resourcePath;
        if (_spriteCache.TryGetValue(key, out var cached))
            return cached;

        string pathNoExt = resourcePath;
        int dotIdx = pathNoExt.LastIndexOf('.');
        if (dotIdx >= 0) pathNoExt = pathNoExt.Substring(0, dotIdx);

        var all = AssetManager.Instance.LoadAll<Sprite>(pathNoExt);
        if (all == null || all.Length == 0)
            return System.Array.Empty<Sprite>();

        string baseName = System.IO.Path.GetFileNameWithoutExtension(resourcePath);
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

        if (frames.Count != 8)
            Debug.LogError($"[Bootstrapper] {baseName}: 期望 8 帧，实际 {frames.Count} 帧。请在 Unity 菜单运行 GameDemo > Step 2 重新切片精灵表。");

        var result = frames.ToArray();
        _spriteCache[key] = result;
        return result;
    }

    // ==================== 背景 ====================

    private void LoadBackground(string fileName)
    {
        string pathNoExt = fileName;
        int dotIdx = pathNoExt.LastIndexOf('.');
        if (dotIdx >= 0) pathNoExt = pathNoExt.Substring(0, dotIdx);

        string fullPath = "Art/Backgrounds/" + pathNoExt;
        var bg = AssetManager.Instance.Load<Sprite>(fullPath);
        if (bg == null)
        {
            Debug.LogWarning($"[Bootstrapper] Background not found: {fullPath}");
            return;
        }

        if (_backgroundRenderer == null)
        {
            var bgGo = new GameObject("Background");
            _backgroundRenderer = bgGo.AddComponent<SpriteRenderer>();
            _backgroundRenderer.sortingOrder = -1;
        }

        _backgroundRenderer.sprite = bg;

        Camera cam = Camera.main;
        if (cam != null && cam.orthographic)
        {
            float camH = cam.orthographicSize * 2f;
            float camW = camH * cam.aspect;

            float spriteW = bg.bounds.size.x;
            float spriteH = bg.bounds.size.y;

            // 等比例缩放至铺满屏幕（取较大比例，无黑边）
            float scale = Mathf.Max(camW / spriteW, camH / spriteH);
            _backgroundRenderer.transform.localScale = new Vector3(scale, scale, 1f);

            // 居中
            _backgroundRenderer.transform.position =
                cam.transform.position + Vector3.forward;
        }
    }

    // ==================== UnitView 构建 ====================

    private void BuildUnitViews(List<UnitPlacementDef> placements, bool isPlayer)
    {
        if (_unitParent == null)
        {
            var go = new GameObject("BattleField");
            _unitParent = go.transform;
        }

        foreach (var p in placements)
        {
            GameObject go = (_unitViewPrefab != null)
                ? Instantiate(_unitViewPrefab, _unitParent)
                : CreateDefaultUnitViewGo(p.id);
            go.name = $"UnitView_{p.id}";

            var unitView = go.GetComponent<UnitView>();
            if (unitView == null) unitView = go.AddComponent<UnitView>();

            // 玩家在左侧，敌方在右侧
            float x = isPlayer ? -4f + p.col * 1.5f : 4f - (3 - p.col) * 1.5f;
            float y = 2f - p.row * 1.8f;
            go.transform.position = new Vector3(x, y, 0);

            _pendingViews[p.id] = unitView;
        }
    }

    private void BindInstancesToViews(Formation formation, bool isPlayer)
    {
        foreach (var instance in formation.Units)
        {
            if (!_pendingViews.TryGetValue(instance.Id, out var view))
            {
                Debug.LogWarning($"View for {instance.Id} not found");
                continue;
            }

            var (idle, attack) = GetUnitFrames(instance.Id);
            view.Setup(instance, idle, attack);
            view.SetFacingRight(isPlayer);  // 我方朝右，敌方朝左
            UnitViews[instance] = view;

            var hpBar = view.GetComponentInChildren<HPBar>();
            if (hpBar != null)
            {
                hpBar.gameObject.SetActive(false);
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

    internal static void SetPrivateField(object target, string fieldName, object value)
    {
        target.GetType()
            .GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(target, value);
    }
}

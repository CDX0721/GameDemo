using System;
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
    private BattleBackgroundSettingsEntry _bgSettings = null!;
    private Camera _cam = null!;

    private const float REF_WIDTH = 1920f;
    private const float REF_HEIGHT = 1080f;
    private const float UNIT_SPRITE_SIZE = 1.28f;  // 128px at PPU=100
    private const float UNIT_SCALE = 0.7f;

    void Awake()
    {
        float targetAspect = 16f / 9f;
        Screen.fullScreen = false;
        int height = Screen.height;
        int width = Mathf.RoundToInt(height * targetAspect);
        Screen.SetResolution(width, height, false);
    }

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

        _cam = Camera.main;
        LoadBackgroundSettings(fieldDef.BackGround);
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

    public Sprite[] GetSkillEffectFrames(string fxId)
    {
        return LoadFramesDirect("Art/Sprites/Skills/" + fxId);
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

    private void LoadBackground(string bgId)
    {
        string fileName = (_bgSettings != null && !string.IsNullOrEmpty(_bgSettings.img))
            ? _bgSettings.img : bgId + ".png";
        string fullPath = "Art/Backgrounds/" + fileName;
        int dotIdx = fullPath.LastIndexOf('.');
        if (dotIdx >= 0) fullPath = fullPath.Substring(0, dotIdx);
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

        if (_cam != null && _cam.orthographic)
        {
            float camH = _cam.orthographicSize * 2f;
            float camW = camH * _cam.aspect;

            float spriteW = bg.bounds.size.x;
            float spriteH = bg.bounds.size.y;

            _backgroundRenderer.transform.localScale = new Vector3(
                camW / spriteW, camH / spriteH, 1f);
            _backgroundRenderer.transform.position =
                _cam.transform.position + Vector3.forward;
        }
    }

    private void LoadBackgroundSettings(string bgId)
    {
        var json = Resources.Load<TextAsset>("Configs/Battle/BackGroundSettings");
        if (json == null)
        {
            Debug.LogWarning("[Bootstrapper] BackGroundSettings.json not found.");
            return;
        }

        // JSON: { "bgId": { settings } } — 动态 key，适配 JsonUtility
        string text = json.text.Trim();
        string keyPattern = $"\"{bgId}\":";
        int keyIdx = text.IndexOf(keyPattern, StringComparison.Ordinal);
        if (keyIdx < 0)
        {
            Debug.LogWarning($"[Bootstrapper] BackGroundSettings for '{bgId}' not found.");
            return;
        }

        int objStart = keyIdx + keyPattern.Length;
        int objEnd = text.LastIndexOf('}');
        string adapted = "{\"Entry\":" + text.Substring(objStart, objEnd - objStart) + "}";
        var wrapper = JsonUtility.FromJson<BackgroundSettingsWrapper>(adapted);
        _bgSettings = wrapper.Entry;
    }

    [Serializable]
    private class BackgroundSettingsWrapper
    {
        public BattleBackgroundSettingsEntry Entry;
    }

    /// <summary>像素坐标（原点：屏幕左下角）→ 世界坐标。</summary>
    private Vector2 PixelToWorld(float pixelX, float pixelY)
    {
        if (_cam == null || !_cam.orthographic) return Vector2.zero;
        float camH = _cam.orthographicSize * 2f;
        float camW = camH * _cam.aspect;
        float worldX = (pixelX / REF_WIDTH) * camW - camW / 2f;
        float worldY = (pixelY / REF_HEIGHT) * camH - camH / 2f;
        return new Vector2(worldX, worldY);
    }

    // ==================== UnitView 构建 ====================

    private void BuildUnitViews(List<UnitPlacementDef> placements, bool isPlayer)
    {
        if (_unitParent == null)
        {
            _unitParent = new GameObject("BattleField").transform;
        }

        // 从 JSON 配置读取阵形格点参数
        if (_bgSettings == null)
        {
            Debug.LogError("[Bootstrapper] BackGroundSettings not loaded.");
            return;
        }

        Vector2 center = isPlayer
            ? PixelToWorld(_bgSettings.PlayerCenterPixel.x, _bgSettings.PlayerCenterPixel.y)
            : PixelToWorld(_bgSettings.EnemyCenterPixel.x, _bgSettings.EnemyCenterPixel.y);

        float pixelToWorld = (_cam != null && _cam.orthographic)
            ? (_cam.orthographicSize * 2f) / REF_HEIGHT
            : 10f / 1080f;

        float rowSpacing = _bgSettings.RowSpacingPixel * pixelToWorld;
        float colSpacing = _bgSettings.ColSpacingPixel * pixelToWorld;
        float bottomOffset = UNIT_SPRITE_SIZE * UNIT_SCALE * 0.5f;

        foreach (var p in placements)
        {
            GameObject go = (_unitViewPrefab != null)
                ? Instantiate(_unitViewPrefab, _unitParent)
                : CreateDefaultUnitViewGo(p.id);
            go.name = $"UnitView_{p.id}";

            var unitView = go.GetComponent<UnitView>();
            if (unitView == null) unitView = go.AddComponent<UnitView>();

            // 数据 row → 屏幕 X：玩家 row1 靠近中，row3 远离中；敌方反之
            float offsetX = isPlayer
                ? (2 - p.row) * rowSpacing
                : (p.row - 2) * rowSpacing;
            // 数据 col → 屏幕 Y：col1 在上，col3 在下
            float offsetY = (2 - p.col) * colSpacing;

            Vector2 worldPos = center + new Vector2(offsetX, offsetY);
            worldPos.y += bottomOffset;  // 底边中点对齐格点

            go.transform.position = new Vector3(worldPos.x, worldPos.y, 0);
            go.transform.localScale = Vector3.one * UNIT_SCALE;
            unitView.SetSortingOrder(p.col);

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

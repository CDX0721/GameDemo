using System.Collections.Generic;
using System.Text;
using GameDemo.Battle;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

using UnityEngine.UI;

namespace GameDemo.UI.Panels
{
    public class BattlePanel : UIPanel
    {
        [Header("Prefab root")]
        [SerializeField] private GameObject _panelPrefab;

        [Header("Unit prefab (separate)")]
        [SerializeField] private GameObject _unitPrefab;

        [Header("Bottom: Unit Status")]
        [SerializeField] private Transform _unitStatusContainer;

        [Header("Bottom: Skill Bar")]
        [SerializeField] private Transform _skillBarContainer;
        [SerializeField] private Button _skillButtonPrefab;
        [SerializeField] private Button _confirmSkillButton;

        [Header("Bottom: Action Queue")]
        [SerializeField] private Transform _actionQueueContainer;
        [SerializeField] private TextMeshProUGUI _actionQueueEntryPrefab;

        [Header("Battle Result")]
        [SerializeField] private GameObject _resultOverlay;
        [SerializeField] private TextMeshProUGUI _resultText;
        [SerializeField] private Button _resultBackButton;

        [Header("Quit")]
        [SerializeField] private Button _quitBattleButton;

        // Runtime
        private BattleManager _battleManager;
        private PlayableUnitInstance _currentPlayableUnit;
        private Skill _selectedSkill;
        private List<BattleUnitInstance> _selectedTargets;
        private Transform _bottomBar;
        private readonly List<GameObject> _skillBtns = new();
        private readonly List<TextMeshProUGUI> _queueEntries = new();
        private readonly Dictionary<string, UnitWidget> _widgets = new();

        private class UnitWidget
        {
            public GameObject Root;
            public TextMeshProUGUI Name, HpVal, MpVal, Info;
            public Image HpFill, MpFill;
        }

        // ============================================================
        // Lifecycle
        // ============================================================
        protected override void OnOpen()
        {
            if (_unitStatusContainer == null)
            {
                FindRefs();
            }
        }

        private void FindRefs()
        {
            _unitStatusContainer = Find("BottomBar/UnitStatus");
            _skillBarContainer    = Find("BottomBar/SkillBar");
            _actionQueueContainer = Find("BottomBar/ActionQueue");
            _confirmSkillButton   = FindBtn("BottomBar/SkillBar/ConfirmBtn");
            if (_confirmSkillButton) _confirmSkillButton.gameObject.SetActive(false);
            _quitBattleButton     = FindBtn("QuitBtn");
            _resultOverlay        = Find("ResultOverlay")?.gameObject;
            if (_resultOverlay) _resultOverlay.SetActive(false);
            _resultText           = FindTxt("ResultOverlay/ResultText");
            _resultBackButton     = FindBtn("ResultOverlay/ResultBackBtn");
        }

        private Transform Find(string path) => transform.Find(path);
        private Button FindBtn(string path) { var t = transform.Find(path); return t ? t.GetComponent<Button>() : null; }
        private TextMeshProUGUI FindTxt(string path) { var t = transform.Find(path); return t ? t.GetComponent<TextMeshProUGUI>() : null; }

        protected override void OnShow()
        {
            if (_resultOverlay) _resultOverlay.SetActive(false);
            if (_confirmSkillButton) { _confirmSkillButton.onClick.AddListener(OnConfirm); _confirmSkillButton.gameObject.SetActive(false); }
            if (_resultBackButton) _resultBackButton.onClick.AddListener(OnBack);
            if (_battleManager != null && _battleManager.StateMachine.IsBattleStart) { _battleManager.StartBattle(); RefreshQueue(); }
        }

        protected override void OnHide()
        {
            Unbind();
            if (_confirmSkillButton) _confirmSkillButton.onClick.RemoveListener(OnConfirm);
            if (_resultBackButton) _resultBackButton.onClick.RemoveListener(OnBack);
        }
        protected override void OnClose() { Unbind(); }


        // ============================================================
        // BattleManager binding
        // ============================================================
        public void BindBattleManager(BattleManager bm)
        {
            Unbind();
            _battleManager = bm;
            bm.OnWaitingForPlayerInput += OnPlayerTurn;
            bm.OnSkillUsed += (c, s, t) => RefreshUnits();
            bm.OnUnitDamaged += (u, d, s) => RefreshUnits();
            bm.OnUnitDied += u => RefreshUnits();
            bm.OnEffectApplied += (u, e) => RefreshUnits();
            bm.OnEffectExpired += (u, e) => RefreshUnits();
            bm.OnActionQueueChanged += () => { RefreshQueue(); RefreshUnits(); };
            bm.OnBattleEnded += OnBattleEnd;
            RefreshQueue();
            RefreshUnits();
        }

        private void Unbind()
        {
            if (_battleManager == null) return;
            _battleManager.OnWaitingForPlayerInput -= OnPlayerTurn;
            _battleManager = null;
        }

        // ============================================================
        // Events
        // ============================================================
        private void OnPlayerTurn(PlayableUnitInstance u) { _currentPlayableUnit = u; _selectedSkill = null; _selectedTargets = null; BuildSkills(u); RefreshUnits(); }
        private void OnBattleEnd(bool won) { ClearSkills(); if (_resultOverlay) { _resultOverlay.SetActive(true); if (_resultText) _resultText.text = won ? "Victory!" : "Defeat..."; } }

        // ============================================================
        // Unit status widgets (VerticalLayoutGroup stacked)
        // ============================================================
        private void RefreshUnits()
        {
            if (_unitStatusContainer == null || _battleManager == null) return;
            var seen = new HashSet<string>();
            foreach (var u in _battleManager.PlayerFormation.Units)
            {
                seen.Add(u.Id);
                if (!_widgets.TryGetValue(u.Id, out var w)) { w = MakeWidget(); _widgets[u.Id] = w; }
                bool act = u == _battleManager.SelectedUnit;
                w.Name.text = (act ? "> " : "  ") + u.DisplayName;
                w.Name.color = u.IsAlive ? (act ? Color.yellow : Color.white) : Color.gray;
                SetBar(w.HpFill, u.MaxHP > 0 ? u.CurrentHP / u.MaxHP : 0);
                w.HpVal.text = string.Format("{0:F0}/{1:F0}", u.CurrentHP, u.MaxHP);
                SetBar(w.MpFill, u.MaxMana > 0 ? u.CurrentMana / u.MaxMana : 0);
                w.MpFill.color = new Color(0.2f, 0.4f, 1f);
                w.MpVal.text = string.Format("{0:F0}/{1:F0}", u.CurrentMana, u.MaxMana);
                var sb = new StringBuilder();
                sb.AppendFormat("ATK{0,3:F0} DEF{1,3:F0} SPD{2,3:F0}", u.CurrentAttack, u.CurrentDefense, u.CurrentSpeed);
                if (u.DamageBonus != 0) sb.AppendFormat(" +{0:P0}", u.DamageBonus);
                if (u.DamageReduction != 0) sb.AppendFormat(" -{0:P0}", u.DamageReduction);
                sb.AppendFormat(" AV{0,4:F1}", u.ActionValue);
                if (u.Effects.Count > 0) { 
                    sb.Append(" | ");
                    foreach (var e in u.Effects) sb.AppendFormat("{0}{1}({2}) ", e.Template.DisplayName ?? e.Template.Id, e.CurrentStackCount > 1 ? "x" + e.CurrentStackCount : "", e.RemainingTurns); 
                }
                w.Info.text = sb.ToString();
            }
            foreach (var kv in _widgets) if (!seen.Contains(kv.Key) && kv.Value.Root) kv.Value.Root.SetActive(false);
        }

        private static void SetBar(Image fill, float pct)
        {
            pct = Mathf.Clamp01(pct);
            fill.rectTransform.sizeDelta = new Vector2(80*pct, 12f);
            fill.color = pct > 0.5f ? new Color(0.15f, 0.85f, 0.2f) : (pct > 0.25f ? new Color(1f, 0.7f, 0.1f) : new Color(1f, 0.15f, 0.1f));
        }

        private UnitWidget MakeWidget()
        {
            if (_unitPrefab == null) _unitPrefab = Resources.Load<GameObject>("UI/Unit");
            var clone = Instantiate(_unitPrefab, _unitStatusContainer);
            clone.SetActive(true);

            T Find<T>(string p) where T : Component {
                var t = clone.transform.Find(p);
                if (t == null) { Debug.LogError("[BP] MakeWidget: NOT FOUND: " + p + " in " + clone.name); return null; }
                var c = t.GetComponent<T>();
                if (c == null) { Debug.LogError("[BP] MakeWidget: NO COMPONENT " + typeof(T).Name + " on " + p); return null; }
                return c;
            }

            var widget = new UnitWidget
            {
                Root   = clone,
                Name   = Find<TextMeshProUGUI>("Line1/UnitName"),
                HpFill = Find<Image>("Line1/HPBar/HPBarValue"),
                HpVal  = Find<TextMeshProUGUI>("Line1/HPText"),
                MpFill = Find<Image>("Line1/MPBar/MPBarValue"),
                MpVal  = Find<TextMeshProUGUI>("Line1/MPText"),
                Info   = Find<TextMeshProUGUI>("Line2/Status2"),
            };
            ApplyCJKFallback(widget.Name);
            ApplyCJKFallback(widget.HpVal);
            ApplyCJKFallback(widget.MpVal);
            ApplyCJKFallback(widget.Info);
            return widget;
        }

        private static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; }

        private static TMP_FontAsset _cjkFont;

        private static TMP_FontAsset GetCJKFont()
        {
            if (_cjkFont != null) return _cjkFont;

            string[] paths = { "C:/Windows/Fonts/simhei.ttf", "C:/Windows/Fonts/msyh.ttf" };
            foreach (var path in paths)
            {
                if (System.IO.File.Exists(path))
                {
                    _cjkFont = TMP_FontAsset.CreateFontAsset(path, 0, 36, 9, GlyphRenderMode.SDFAA, 1024, 1024);
                    if (_cjkFont != null) return _cjkFont;
                }
            }
            return null;
        }

        private static TMP_FontAsset GetTMPFontAsset()
        {
            var sans = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            var cjk = GetCJKFont();
            if (sans != null && cjk != null)
            {
                sans.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
                if (!sans.fallbackFontAssetTable.Contains(cjk))
                    sans.fallbackFontAssetTable.Add(cjk);
            }
            return sans;
        }

        private static void ApplyCJKFallback(TextMeshProUGUI tmp)
        {
            if (tmp == null || tmp.font == null) return;
            var cjk = GetCJKFont();
            if (cjk == null) return;
            tmp.font.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
            if (!tmp.font.fallbackFontAssetTable.Contains(cjk))
                tmp.font.fallbackFontAssetTable.Add(cjk);
        }

        private void BuildSkills(PlayableUnitInstance unit)
        {
            ClearSkills();
            int idx = 0;
            foreach (var (s, t) in _battleManager.GetCastableSkills(unit))
            {
                Button btn;
                if (idx < _skillBtns.Count)
                {
                    var go = _skillBtns[idx];
                    go.SetActive(true);
                    btn = go.GetComponent<Button>();
                    btn.onClick.RemoveAllListeners();
                }
                else
                {
                    btn = _skillButtonPrefab ? Instantiate(_skillButtonPrefab, _skillBarContainer) : DefaultSkillBtn();
                    _skillBtns.Add(btn.gameObject);
                }
                var lbl = btn.GetComponentInChildren<TextMeshProUGUI>(); if (lbl) lbl.text = s.DisplayName;
                var sk = s; var tg = t;
                btn.onClick.AddListener(() => { _selectedSkill = sk; _selectedTargets = tg; });
                idx++;
            }
            for (int i = idx; i < _skillBtns.Count; i++) _skillBtns[i].SetActive(false);
            if (_confirmSkillButton) _confirmSkillButton.gameObject.SetActive(true);
        }
        private void OnConfirm() { if (_currentPlayableUnit == null || _selectedSkill == null || _selectedTargets == null) return; _battleManager.SubmitPlayerAction(_selectedSkill, _selectedTargets); ClearSkills(); if (_confirmSkillButton) _confirmSkillButton.gameObject.SetActive(false); }
        private void ClearSkills() { foreach (var g in _skillBtns) { var b = g.GetComponent<Button>(); if (b) b.onClick.RemoveAllListeners(); g.SetActive(false); } _currentPlayableUnit = null; _selectedSkill = null; _selectedTargets = null; }
        private Button DefaultSkillBtn()
        {
            var go = new GameObject("SkillBtn", typeof(RectTransform));
            go.transform.SetParent(_skillBarContainer, false);
            go.AddComponent<Image>().color = new Color(0.2f, 0.25f, 0.35f, 1f);
            var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(160, 80);
            var lg = new GameObject("Label", typeof(RectTransform)); lg.transform.SetParent(go.transform, false);
            var t = lg.AddComponent<TextMeshProUGUI>(); t.font = GetTMPFontAsset(); t.fontSize = 24; t.color = Color.white; t.alignment = TextAlignmentOptions.Center;
            Stretch(lg.GetComponent<RectTransform>());
            return go.AddComponent<Button>();
        }

        private void RefreshQueue()
        {
            if (_actionQueueContainer == null || _battleManager == null) return;
            int idx = 0;
            for (int i = 0; i < _battleManager.ActionQueue.Count; i++)
            {
                var u = _battleManager.ActionQueue[i]; if (u == null || !u.IsAlive) continue;
                TextMeshProUGUI e;
                if (idx < _queueEntries.Count)
                {
                    e = _queueEntries[idx];
                    e.gameObject.SetActive(true);
                }
                else
                {
                    e = _actionQueueEntryPrefab ? Instantiate(_actionQueueEntryPrefab, _actionQueueContainer) : QueueEntry();
                    _queueEntries.Add(e);
                }
                bool first = u == _battleManager.ActionQueue.Current;
                e.text = first ? "[" + u.DisplayName + "]" : u.DisplayName;
                e.color = first ? Color.yellow : Color.white;
                idx++;
            }
            for (int i = idx; i < _queueEntries.Count; i++) _queueEntries[i].gameObject.SetActive(false);
        }

        private TextMeshProUGUI QueueEntry()
        {
            var g = new GameObject("Q", typeof(RectTransform)); 
            g.transform.SetParent(_actionQueueContainer, false); 
            var t = g.AddComponent<TextMeshProUGUI>(); 
            t.font = GetTMPFontAsset(); 
            t.fontSize = 22; 
            t.color = Color.white; 
            t.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 22);
            return t;
        }

        private void OnBack() { UIManager.Instance.Pop(); }
        private void OnSettings() { Debug.Log("[BattlePanel] Settings N/I"); }
    }
}

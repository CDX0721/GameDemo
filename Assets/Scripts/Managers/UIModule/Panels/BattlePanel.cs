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

        [Header("Skill prefabs")]
        [SerializeField] private GameObject _skillEntryPrefab;

        [Header("Bottom: Unit Status")]
        [SerializeField] private Transform _unitStatusContainer;

        [Header("Bottom: Skill Bar")]
        [SerializeField] private Transform _skillListContainer;
        [SerializeField] private TextMeshProUGUI _skillInfoName;
        [SerializeField] private TextMeshProUGUI _skillInfoDescription;
        [SerializeField] private Button _confirmSkillButton;
        [SerializeField] private GameObject _skillInfoPadding;

        [Header("Bottom: Action Queue")]
        [SerializeField] private Transform _actionQueueContainer;
        [SerializeField] private TextMeshProUGUI _actionQueueEntryPrefab;

        [Header("Battle Result")]
        [SerializeField] private GameObject _resultOverlay;
        [SerializeField] private TextMeshProUGUI _resultText;
        [SerializeField] private Button _resultBackButton;

        [Header("Quit")]
        [SerializeField] private Button _quitBattleButton;

        // Sprites
        private Sprite _skillNormal;
        private Sprite _skillPressed;
        private Sprite _confirmNormal;
        private Sprite _confirmPressed;

        // Runtime
        private BattleManager _battleManager;
        private PlayableUnitInstance _currentPlayableUnit;
        private Skill _selectedSkill;
        private BattleUnitInstance? _selectedTarget;
        private Transform _bottomBar;
        private readonly List<GameObject> _skillBtns = new();
        private readonly List<TextMeshProUGUI> _queueEntries = new();
        private readonly Dictionary<string, UnitWidget> _widgets = new();

        // Targeting
        public event System.Action<BattleUnitInstance> OnTargetChanged;
        private bool _isTargeting;
        private List<BattleUnitInstance> _candidateTargets = new();
        private int _targetIndex;

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
            _unitStatusContainer  = FindOrDie("BottomBar/UnitStatus/Padding", "_unitStatusContainer");
            _actionQueueContainer = FindOrDie("BottomBar/ActionQueue/Padding", "_actionQueueContainer");
            _quitBattleButton     = FindBtn("QuitBtn");
            _resultOverlay        = Find("ResultOverlay")?.gameObject;
            if (_resultOverlay) _resultOverlay.SetActive(false);
            _resultText           = FindTxt("ResultOverlay/ResultText");
            _resultBackButton     = FindBtn("ResultOverlay/ResultBackBtn");

            // SkillBar: load Skill prefab into existing container
            var skillBar = FindOrDie("BottomBar/SkillBar", "SkillBar");
            var skillPrefab = Resources.Load<GameObject>("UI/Skill");
            if (skillPrefab == null) { Debug.LogError("[BP] UI/Skill.prefab not found in Resources"); return; }
            var skillGO = Instantiate(skillPrefab, skillBar);
            skillGO.name = "Skill";
            var skillRoot = skillGO.transform;

            _skillListContainer = FindOrDie(skillRoot, "SkillList/Padding", "_skillListContainer");
            _skillInfoPadding   = FindOrDie(skillRoot, "SkillInfo/Padding", "_skillInfoPadding").gameObject;
            _skillInfoPadding.SetActive(false);
            _skillInfoName        = FindOrDieTxt(skillRoot, "SkillInfo/Padding/Name", "_skillInfoName");
            _skillInfoDescription = FindOrDieTxt(skillRoot, "SkillInfo/Padding/Description", "_skillInfoDescription");
            _confirmSkillButton   = FindOrDieBtn(skillRoot, "SkillInfo/Padding/ConfirmContainer/Confirm", "_confirmSkillButton");
            if (_confirmSkillButton) _confirmSkillButton.gameObject.SetActive(false);

            LoadSprites();
        }

        private Transform FindOrDie(string path, string name)
        {
            var t = transform.Find(path);
            if (t == null) Debug.LogError($"[BP] {name} not found at '{path}'");
            return t;
        }
        private Transform FindOrDie(Transform parent, string path, string name)
        {
            var t = parent.Find(path);
            if (t == null) Debug.LogError($"[BP] {name} not found at '{path}' under '{parent.name}'");
            return t;
        }
        private TextMeshProUGUI FindOrDieTxt(Transform parent, string path, string name)
        {
            var t = parent.Find(path);
            if (t == null) { Debug.LogError($"[BP] {name} not found at '{path}' under '{parent.name}'"); return null; }
            return t.GetComponent<TextMeshProUGUI>();
        }
        private Button FindOrDieBtn(Transform parent, string path, string name)
        {
            var t = parent.Find(path);
            if (t == null) { Debug.LogError($"[BP] {name} not found at '{path}' under '{parent.name}'"); return null; }
            return t.GetComponent<Button>();
        }

        private void LoadSprites()
        {
            _skillNormal   = Resources.Load<Sprite>("Art/UI/SkillBar/SkillEntryNormal");
            _skillPressed  = Resources.Load<Sprite>("Art/UI/SkillBar/SkillEntryPressed");
            _confirmNormal = Resources.Load<Sprite>("Art/UI/General/Button/ButtonNormal_200_30");
            _confirmPressed = Resources.Load<Sprite>("Art/UI/General/Button/ButtonPressed_200_30");
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

        void Update()
        {
            if (!_isTargeting || _candidateTargets.Count == 0) return;

            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                CycleTarget(-1);
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                CycleTarget(1);
        }

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
        private void OnPlayerTurn(PlayableUnitInstance u) { _currentPlayableUnit = u; _selectedSkill = null; _selectedTarget = null; if (_isTargeting) ExitTargeting(); BuildSkills(u); RefreshUnits(); }
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
                w.Name.color = u.IsAlive ? (act ? new Color(0.8f, 0.15f, 0.05f) : Color.black) : new Color(0.45f, 0.45f, 0.45f);
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
            if (_unitStatusContainer == null) { Debug.LogError("[BP] _unitStatusContainer is null, cannot parent Unit prefab"); return null; }
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

        // ============================================================
        // Skill Bar
        // ============================================================
        private void BuildSkills(PlayableUnitInstance unit)
        {
            ClearSkills();
            if (_skillListContainer == null) return;

            var skills = _battleManager.GetCastableSkills(unit);
            var seen = new HashSet<string>();
            int idx = 0;
            foreach (var (s, t) in skills)
            {
                if (!seen.Add(s.Id)) continue; // one entry per unique skill

                GameObject entry;
                if (idx < _skillBtns.Count)
                {
                    entry = _skillBtns[idx];
                    entry.SetActive(true);
                }
                else
                {
                    if (_skillEntryPrefab == null) _skillEntryPrefab = Resources.Load<GameObject>("UI/SkillEntry");
                    entry = Instantiate(_skillEntryPrefab, _skillListContainer);
                    _skillBtns.Add(entry);
                }

                // Sprite on Padding/Image
                var imgT = entry.transform.Find("Padding/Image");
                if (imgT != null)
                {
                    var img = imgT.GetComponent<Image>();
                    if (img != null) img.sprite = GetSkillIcon(s.Id) ?? _skillNormal;
                }

                // Label on Padding/Name
                var nameT = entry.transform.Find("Padding/Name");
                if (nameT != null)
                {
                    var lbl = nameT.GetComponent<TextMeshProUGUI>();
                    if (lbl != null) { lbl.font = GetTMPFontAsset(); lbl.text = SkillLabel(s); }
                }

                var btn = entry.GetComponent<Button>();
                btn.onClick.RemoveAllListeners();
                var sk = s;
                btn.onClick.AddListener(() => SelectSkill(entry, sk));
                idx++;
            }
            for (int i = idx; i < _skillBtns.Count; i++) _skillBtns[i].SetActive(false);
        }

        private void SelectSkill(GameObject entry, Skill skill)
        {
            _selectedSkill = skill;

            foreach (var e in _skillBtns)
            {
                if (!e.activeSelf) continue;
                var img = e.GetComponent<Image>();
                if (img != null && _skillNormal != null && _skillPressed != null)
                    img.sprite = (e == entry) ? _skillPressed : _skillNormal;
            }

            ShowSkillInfo(skill.DisplayName, skill.Description);
            if (_confirmSkillButton != null) _confirmSkillButton.gameObject.SetActive(true);
        }

        private void EnterTargeting()
        {
            if (_selectedSkill == null) return;

            var skills = _battleManager.GetCastableSkills(_currentPlayableUnit);
            _candidateTargets.Clear();
            foreach (var (s, t) in skills)
                if (s.Id == _selectedSkill.Id)
                    _candidateTargets.Add(t);
            _targetIndex = 0;
            _selectedTarget = _candidateTargets.Count > 0 ? _candidateTargets[0] : null;

            _isTargeting = true;

            // Disable skill entries
            foreach (var g in _skillBtns) { var b = g.GetComponent<Button>(); if (b) b.interactable = false; }
            // Switch to target selection UI
            if (_skillInfoName != null) _skillInfoName.text = "选择目标";
            if (_skillInfoDescription != null) _skillInfoDescription.text = "";
            OnTargetChanged?.Invoke(_selectedTarget);
        }

        private void ExitTargeting()
        {
            _isTargeting = false;
            _candidateTargets.Clear();
            foreach (var g in _skillBtns) { var b = g.GetComponent<Button>(); if (b) b.interactable = true; }
            OnTargetChanged?.Invoke(null);
        }

        private void CycleTarget(int direction)
        {
            if (_candidateTargets.Count <= 1) return;
            _targetIndex = (_targetIndex + direction + _candidateTargets.Count) % _candidateTargets.Count;
            _selectedTarget = _candidateTargets[_targetIndex];
            OnTargetChanged?.Invoke(_selectedTarget);
        }

        private void ShowSkillInfo(string name, string desc)
        {
            if (_skillInfoPadding != null) _skillInfoPadding.SetActive(true);
            if (_skillInfoName != null) _skillInfoName.text = name;
            if (_skillInfoDescription != null) _skillInfoDescription.text = desc;
        }

        private void OnConfirm()
        {
            if (_currentPlayableUnit == null || _selectedSkill == null) return;

            bool isAoE = _selectedSkill.TargetType is TargetType.AllEnemies or TargetType.AllAllies or TargetType.AllBoth
                      || _selectedSkill.TargetType is TargetType.SingleSelf;

            if (!_isTargeting && !isAoE)
            {
                // First confirm: enter target selection for single-target skills
                EnterTargeting();
                return;
            }

            // Second confirm (or AoE): submit
            if (_selectedTarget == null && isAoE)
            {
                // Grab first candidate for AoE
                var skills = _battleManager.GetCastableSkills(_currentPlayableUnit);
                foreach (var (s, t) in skills)
                    if (s.Id == _selectedSkill.Id) { _selectedTarget = t; break; }
            }

            if (_selectedTarget == null) return;
            _battleManager.SubmitPlayerAction(_selectedSkill, _selectedTarget);
            ClearSkills();
            _currentPlayableUnit = null;
            if (_confirmSkillButton) _confirmSkillButton.gameObject.SetActive(false);
        }

        private static Sprite GetSkillIcon(string skillId)
            => Resources.Load<Sprite>($"Art/Sprites/UI/Icons/{skillId}_icon");

        private static string SkillLabel(Skill s)
        {
            return s.TargetType switch
            {
                TargetType.AllEnemies or TargetType.AllAllies or TargetType.AllBoth => $"{s.DisplayName} [全体]",
                TargetType.SingleSelf => $"{s.DisplayName} [自身]",
                _ => s.DisplayName,
            };
        }

        private void ClearSkills()
        {
            if (_isTargeting) ExitTargeting();
            foreach (var g in _skillBtns) g.SetActive(false);
            _selectedSkill = null;
            _selectedTarget = null;
            _candidateTargets.Clear();
            if (_skillInfoPadding != null) _skillInfoPadding.SetActive(false);
            if (_skillInfoName != null) _skillInfoName.text = "";
            if (_skillInfoDescription != null) _skillInfoDescription.text = "";
        }

        // ============================================================
        // Action Queue
        // ============================================================
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
                e.color = first ? new Color(0.8f, 0.15f, 0.05f) : Color.black;
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
            t.color = Color.black;
            t.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 22);
            return t;
        }

        private void OnBack() { UIManager.Instance.Pop(); }
        private void OnSettings() { Debug.Log("[BattlePanel] Settings N/I"); }
    }
}

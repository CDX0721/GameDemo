using System.Collections.Generic;
using System.Text;
using GameDemo.Battle;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameDemo.UI.Panels
{
    public class BattlePanel : UIPanel
    {
        // ============================================================
        // Serialized fields (set via prefab, fallback to path search)
        // ============================================================
        [Header("Result")]
        [SerializeField] private GameObject _resultOverlay;
        [SerializeField] private TextMeshProUGUI _resultText;
        [SerializeField] private Button _resultBackButton;

        // ============================================================
        // Runtime references — Battle
        // ============================================================
        private BattleManager _battleManager;
        private BattleUnitInstance _actingUnit;
        private Skill _selectedSkill;

        // ============================================================
        // Object selection (always active)
        // ============================================================
        private BattleUnitInstance _selectedUnit;
        private List<BattleUnitInstance> _selectableUnits = new();

        // ============================================================
        // Unit display (single Unit prefab in UnitStatus/Padding)
        // ============================================================
        private GameObject _unitGO;
        private Image _unitAvatar;
        private TextMeshProUGUI _unitHPText;
        private RectTransform _unitHPBarValue;
        private RectTransform _unitMPBarValue;
        private TextMeshProUGUI _unitAttack, _unitDefend, _unitSpeed, _unitShield, _unitDamageAmp, _unitImmunity;
        private Transform _unitEffectsContainer;

        // ============================================================
        // Skill bar
        // ============================================================
        private GameObject _skillGO;
        private GameObject _skillUnitInfoGO;
        private Image _skillUnitInfoAvatar;
        private TextMeshProUGUI _skillUnitInfoName;
        private GameObject _skillEmptyGO;
        private Transform _skillListPadding;
        private GameObject _skillInfoPadding;
        private TextMeshProUGUI _skillInfoName;
        private TextMeshProUGUI _skillInfoDescription;
        private Button _confirmButton;
        private readonly List<GameObject> _skillEntryGOs = new();
        private readonly List<Skill> _currentSkills = new();
        private int _selectedEntryIdx = -1;
        private Sprite _skillEntryNormalSprite;
        private Sprite _skillEntryPressedSprite;

        // ============================================================
        // Action queue
        // ============================================================
        private Transform _actionQueuePadding;
        private readonly List<GameObject> _queueEntryGOs = new();

        // ============================================================
        // Prefabs (loaded from Resources/UI/)
        // ============================================================
        private GameObject _unitPrefab;
        private GameObject _skillEntryPrefab;
        private GameObject _skillUnitInfoPrefab;
        private GameObject _actionQueueEntryPrefab;
        private GameObject _queueHighlightPrefab;
        private GameObject _queueHighlightGO;
        private GameObject _effectPrefab;
        private GameObject _tooltipPrefab;
        private GameObject _tooltipGO;

        // ============================================================
        // Events (for Bootstrapper target highlight)
        // ============================================================
        public event System.Action<BattleUnitInstance> OnTargetChanged;

        // ============================================================
        // Lifecycle
        // ============================================================
        protected override void OnOpen()
        {
            InitializeUI();
        }

        private void InitializeUI()
        {
            // Load all prefabs
            _unitPrefab              = Resources.Load<GameObject>("UI/Unit");
            _skillEntryPrefab        = Resources.Load<GameObject>("UI/SkillEntry");
            _skillUnitInfoPrefab     = Resources.Load<GameObject>("UI/SkillUnitInfo");
            _actionQueueEntryPrefab  = Resources.Load<GameObject>("UI/ActionQueueEntry");
            _queueHighlightPrefab    = Resources.Load<GameObject>("UI/QueueHighlight");
            _effectPrefab            = Resources.Load<GameObject>("UI/Effect");
            _tooltipPrefab           = Resources.Load<GameObject>("UI/EffectTooltip");
            _skillEntryNormalSprite  = Resources.Load<Sprite>("Art/UI/SkillBar/SkillEntryNormal");
            _skillEntryPressedSprite = Resources.Load<Sprite>("Art/UI/SkillBar/SkillEntryPressed");

            // --- UnitStatus: instantiate single Unit prefab ---
            var unitStatusPadding = SafeFind("BottomBar/UnitStatus/Padding");
            if (_unitPrefab != null && unitStatusPadding != null)
            {
                _unitGO = Instantiate(_unitPrefab, unitStatusPadding);
                _unitGO.name = "Unit";
                _unitGO.SetActive(false);
                FindUnitComponents();
                // If unit was already selected, populate it now
                if (_selectedUnit != null) RefreshUnitDisplay();
            }

            // --- SkillBar: instantiate Skill prefab ---
            var skillBar = SafeFind("BottomBar/SkillBar");
            var skillPrefab = Resources.Load<GameObject>("UI/Skill");
            if (skillPrefab != null && skillBar != null)
            {
                _skillGO = Instantiate(skillPrefab, skillBar);
                _skillGO.name = "Skill";

                _skillListPadding = _skillGO.transform.Find("SkillList/Padding");
                if (_skillListPadding == null) Debug.LogError("[BP] SkillList/Padding not found under Skill prefab");

                _skillInfoPadding = _skillGO.transform.Find("SkillInfo/Padding")?.gameObject;
                if (_skillInfoPadding != null) _skillInfoPadding.SetActive(false);
                else Debug.LogError("[BP] SkillInfo/Padding not found under Skill prefab");

                _skillInfoName        = SafeGetTMP(_skillGO.transform, "SkillInfo/Padding/Name");
                _skillInfoDescription = SafeGetTMP(_skillGO.transform, "SkillInfo/Padding/Description");
                _confirmButton        = SafeGetButton(_skillGO.transform, "SkillInfo/Padding/ConfirmContainer/Confirm");
                if (_confirmButton != null)
                {
                    _confirmButton.onClick.AddListener(OnConfirm);
                    _confirmButton.gameObject.SetActive(false);
                }

                // Pre-instantiate SkillUnitInfo and Empty placeholder in SkillList/Padding
                if (_skillListPadding != null)
                {
                    if (_skillUnitInfoPrefab != null)
                    {
                        _skillUnitInfoGO = Instantiate(_skillUnitInfoPrefab, _skillListPadding);
                        _skillUnitInfoGO.name = "SkillUnitInfo";
                        _skillUnitInfoAvatar = _skillUnitInfoGO.transform.Find("Avatar")?.GetComponent<Image>();
                        _skillUnitInfoName   = _skillUnitInfoGO.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
                        _skillUnitInfoGO.SetActive(false);
                    }
                    _skillEmptyGO = new GameObject("Empty", typeof(RectTransform));
                    _skillEmptyGO.transform.SetParent(_skillListPadding, false);
                    _skillEmptyGO.SetActive(false);
                }
            }

            // --- ActionQueue ---
            _actionQueuePadding = SafeFind("ActionQueue/Padding");
            // If we already have battle data, populate queue now
            if (_actionQueuePadding != null && _battleManager != null)
                RefreshQueue();

            // --- Result ---
            // Fallback: if not wired in prefab, find by path
            if (_resultOverlay == null)
            {
                var resultT = transform.Find("ResultOverlay");
                if (resultT != null) _resultOverlay = resultT.gameObject;
            }
            if (_resultOverlay != null) _resultOverlay.SetActive(false);

            if (_resultText == null)
                _resultText = SafeGetTMP(transform, "ResultOverlay/ResultText");
            if (_resultBackButton == null)
            {
                _resultBackButton = SafeGetButton(transform, "ResultOverlay/ResultBackBtn");
                if (_resultBackButton != null) _resultBackButton.onClick.AddListener(OnBack);
            }
        }

        private void FindUnitComponents()
        {
            if (_unitGO == null) return;
            _unitAvatar       = _unitGO.transform.Find("Basic/Avatar")?.GetComponent<Image>();
            _unitHPText       = _unitGO.transform.Find("Basic/HPText")?.GetComponent<TextMeshProUGUI>();
            _unitHPBarValue   = _unitGO.transform.Find("Basic/HPBar/HPBarValue")?.GetComponent<RectTransform>();
            _unitMPBarValue   = _unitGO.transform.Find("Basic/MPBar/MPBarValue")?.GetComponent<RectTransform>();
            _unitAttack       = _unitGO.transform.Find("Values/Attact")?.GetComponent<TextMeshProUGUI>();
            _unitDefend       = _unitGO.transform.Find("Values/Defend")?.GetComponent<TextMeshProUGUI>();
            _unitSpeed        = _unitGO.transform.Find("Values/Speed")?.GetComponent<TextMeshProUGUI>();
            _unitShield       = _unitGO.transform.Find("Values/Shield")?.GetComponent<TextMeshProUGUI>();
            _unitDamageAmp    = _unitGO.transform.Find("Values/DamageAmp")?.GetComponent<TextMeshProUGUI>();
            _unitImmunity     = _unitGO.transform.Find("Values/Immunity")?.GetComponent<TextMeshProUGUI>();
            _unitEffectsContainer = _unitGO.transform.Find("Effects");
        }

        // ============================================================
        // Helper finders
        // ============================================================
        private Transform SafeFind(string path)
        {
            var t = transform.Find(path);
            if (t == null) Debug.LogError($"[BP] Not found: {path}");
            return t;
        }

        private static TextMeshProUGUI SafeGetTMP(Transform parent, string path)
        {
            var t = parent.Find(path);
            if (t == null) { Debug.LogError($"[BP] TMP not found: {path} under {parent.name}"); return null; }
            var cmp = t.GetComponent<TextMeshProUGUI>();
            if (cmp == null) Debug.LogError($"[BP] No TMP component on: {path} under {parent.name}");
            return cmp;
        }

        private static Button SafeGetButton(Transform parent, string path)
        {
            var t = parent.Find(path);
            if (t == null) { Debug.LogError($"[BP] Button not found: {path} under {parent.name}"); return null; }
            var cmp = t.GetComponent<Button>();
            if (cmp == null) Debug.LogError($"[BP] No Button component on: {path} under {parent.name}");
            return cmp;
        }

        // ============================================================
        // Show / Hide
        // ============================================================
        protected override void OnShow()
        {
            if (_resultOverlay != null) _resultOverlay.SetActive(false);
            if (_confirmButton != null) { _confirmButton.onClick.RemoveListener(OnConfirm); _confirmButton.onClick.AddListener(OnConfirm); _confirmButton.gameObject.SetActive(false); }
            if (_resultBackButton != null) { _resultBackButton.onClick.RemoveListener(OnBack); _resultBackButton.onClick.AddListener(OnBack); }
            if (_battleManager != null && _battleManager.StateMachine.IsBattleStart)
            {
                _battleManager.StartBattle();
                RefreshQueue();
                RefreshUnitDisplay();
            }
        }

        protected override void OnHide()
        {
            Unbind();
            if (_confirmButton != null) _confirmButton.onClick.RemoveListener(OnConfirm);
            if (_resultBackButton != null) _resultBackButton.onClick.RemoveListener(OnBack);
        }

        protected override void OnClose() { Unbind(); }

        // ============================================================
        // Update — object selection via arrow keys (always active)
        // ============================================================
        // Formations are laid out HORIZONTALLY side by side:
        //   Formation.Row  = horizontal position (0-2 left→right within each side)
        //   Formation.Col  = vertical position   (0-2 top→bottom)
        // Combined grid: 6 rows × 3 cols
        //   combinedRow 0-2 = player, combinedRow 3-5 = enemy
        //   combinedCol     = Formation.Col (0-2)
        //   "Left-right" axis = combinedRow, "top-bottom" axis = combinedCol
        //
        // Row-major scan: (r0,c0),(r1,c0),...,(r5,c0), (r0,c1),...,(r5,c2)
        //   Right : +1 row      (left→right), overflow wraps to next col (top→bottom)
        //   Left  : -1 row      (right→left), overflow wraps to prev col (bottom→top)
        //   Down  : +1 col      (top→bottom), overflow wraps to next row (left→right)
        //   Up    : -1 col      (bottom→top), overflow wraps to prev row (right→left)

        private const int COMBINED_ROWS = 6; // player row 0-2 + enemy row 0-2
        private const int COMBINED_COLS = 3;
        private const int TOTAL_SLOTS = COMBINED_ROWS * COMBINED_COLS;

        void Update()
        {
            if (_battleManager == null) return;

            // --- Skill selection via number keys 1-8 ---
            if (_actingUnit != null && _currentSkills.Count > 0)
            {
                for (int i = 0; i < _currentSkills.Count && i < 8; i++)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                    {
                        SelectSkill(i);
                        return;
                    }
                }
            }

            // --- Object selection via arrow keys ---
            if (_selectableUnits.Count == 0) return;

            int dR = 0, dC = 0; // dR: horizontal, dC: vertical
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                dR = 1;
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                dR = -1;
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                dC = 1;
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                dC = -1;
            else
                return;

            if (_selectedUnit == null) return;

            // Current position in combined grid
            bool side = _battleManager.IsPlayerUnit(_selectedUnit);
            int r = side ? _selectedUnit.Row : _selectedUnit.Row + 3;
            int c = _selectedUnit.Col;

            for (int i = 0; i < TOTAL_SLOTS; i++)
            {
                // horizontal movement (left/right) along combinedRow axis
                if (dR != 0)
                {
                    r += dR;
                    // Row overflow → wrap col top/bottom
                    if (r < 0) { r = COMBINED_ROWS - 1; c--; }
                    else if (r >= COMBINED_ROWS) { r = 0; c++; }
                }
                // vertical movement (up/down) along combinedCol axis
                if (dC != 0)
                {
                    c += dC;
                    // Col overflow → wrap row left/right
                    if (c < 0) { c = COMBINED_COLS - 1; r--; }
                    else if (c >= COMBINED_COLS) { c = 0; r++; }
                }
                // Clamp both axes
                if (r < 0) r = COMBINED_ROWS - 1;
                if (r >= COMBINED_ROWS) r = 0;
                if (c < 0) c = COMBINED_COLS - 1;
                if (c >= COMBINED_COLS) c = 0;

                bool s = r < 3;
                int fRow = s ? r : r - 3;
                int fCol = c;
                var form = s ? _battleManager.PlayerFormation : _battleManager.EnemyFormation;
                var unit = form.GetUnit(new BattleSlot(fRow, fCol));
                if (unit != null && unit.IsAlive)
                {
                    _selectedUnit = unit;
                    RefreshUnitDisplay();
                    OnTargetChanged?.Invoke(_selectedUnit);
                    RefreshSkillState();
                    return;
                }
            }
        }

        // ============================================================
        // BattleManager binding
        // ============================================================
        public void BindBattleManager(BattleManager bm)
        {
            Unbind();
            _battleManager = bm;
            bm.OnSkillUsed += (c, s, t) => ScheduleRefresh();
            bm.OnUnitDamaged += (u, d, s, td) => ScheduleRefresh();
            bm.OnUnitHealed += (u, h, s) => ScheduleRefresh();
            bm.OnUnitDied += u => ScheduleRefresh();
            bm.OnEffectApplied += (u, e) => ScheduleRefresh();
            bm.OnEffectExpired += (u, e) => ScheduleRefresh();
            bm.OnActionQueueChanged += () => { RebuildSelectableUnits(); RefreshQueue(); RefreshUnitDisplay(); };
            bm.OnBattleEnded += OnBattleEnd;
            bm.StateMachine.OnStateChanged += OnStateChanged;
            RebuildSelectableUnits();
            RefreshQueue();
            RefreshUnitDisplay();
        }

        private void Unbind()
        {
            if (_battleManager == null) return;
            _battleManager.OnBattleEnded -= OnBattleEnd;
            _battleManager.StateMachine.OnStateChanged -= OnStateChanged;
            _battleManager = null;
        }

        // Deferred refresh to avoid calling during state transitions
        private bool _pendingRefresh;
        private void ScheduleRefresh()
        {
            if (!_pendingRefresh)
            {
                _pendingRefresh = true;
                StartCoroutine(DeferredRefresh());
            }
        }

        private System.Collections.IEnumerator DeferredRefresh()
        {
            yield return null;
            _pendingRefresh = false;
            if (_battleManager == null) yield break;
            RebuildSelectableUnits();
            RefreshQueue();
            RefreshUnitDisplay();
        }

        // ============================================================
        // State changes
        // ============================================================
        private void OnStateChanged(BattleState prev, BattleState next)
        {
            if (next == BattleState.WaitingAction)
            {
                var unit = _battleManager?.SelectedUnit;
                if (unit != null && _battleManager.IsPlayerUnit(unit))
                {
                    _actingUnit = unit;
                    _selectedSkill = null;
                    BuildSkills(unit);
                }
                else
                {
                    ClearSkills();
                }
                RefreshUnitDisplay();
            }
            else if (next == BattleState.PostAction)
            {
                RebuildSelectableUnits();
                RefreshQueue();
                RefreshUnitDisplay();
            }
        }

        // ============================================================
        // Turn events
        // ============================================================
        private void OnBattleEnd(bool won)
        {
            ClearSkills();
            if (_resultOverlay != null)
            {
                _resultOverlay.SetActive(true);
                if (_resultText != null)
                    _resultText.text = won ? "Victory!" : "Defeat...";
            }
        }

        // ============================================================
        // Selectable units
        // ============================================================
        private void RebuildSelectableUnits()
        {
            if (_battleManager == null) return;

            var previous = _selectedUnit;
            _selectableUnits.Clear();

            for (int r = 0; r < Formation.Rows; r++)
                for (int c = 0; c < Formation.Cols; c++)
                {
                    var u = _battleManager.PlayerFormation.GetUnit(new BattleSlot(r, c));
                    if (u != null && u.IsAlive) _selectableUnits.Add(u);
                }

            for (int r = 0; r < Formation.Rows; r++)
                for (int c = 0; c < Formation.Cols; c++)
                {
                    var u = _battleManager.EnemyFormation.GetUnit(new BattleSlot(r, c));
                    if (u != null && u.IsAlive) _selectableUnits.Add(u);
                }

            if (previous != null && _selectableUnits.Contains(previous))
                _selectedUnit = previous;
            else if (_selectableUnits.Count > 0)
                _selectedUnit = _selectableUnits[0];
            else
                _selectedUnit = null;

            if (_selectedUnit != null)
                OnTargetChanged?.Invoke(_selectedUnit);
        }

        // ============================================================
        // Unit display
        // ============================================================
        private void RefreshUnitDisplay()
        {
            if (_unitGO == null || _selectedUnit == null)
            {
                if (_unitGO != null) _unitGO.SetActive(false);
                return;
            }

            _unitGO.SetActive(true);
            var u = _selectedUnit;

            // Avatar
            if (_unitAvatar != null)
            {
                var icon = LoadUnitIcon(u.Id);
                _unitAvatar.sprite = icon;
                _unitAvatar.enabled = icon != null;
            }

            // HP
            float hpPct = u.MaxHP > 0 ? Mathf.Clamp01(u.CurrentHP / u.MaxHP) : 0;
            if (_unitHPText != null)
                _unitHPText.text = $"生命值：{u.CurrentHP:F0}/{u.MaxHP:F0}";
            if (_unitHPBarValue != null)
                _unitHPBarValue.sizeDelta = new Vector2(hpPct * 150f, _unitHPBarValue.sizeDelta.y);

            // MP
            float mpPct = u.MaxMana > 0 ? Mathf.Clamp01(u.CurrentMana / u.MaxMana) : 0;
            if (_unitMPBarValue != null)
                _unitMPBarValue.sizeDelta = new Vector2(mpPct * 150f, _unitMPBarValue.sizeDelta.y);

            // Stats
            if (_unitAttack != null)    _unitAttack.text    = $"攻击力：{u.CurrentAttack:F0}";
            if (_unitDefend != null)    _unitDefend.text    = $"防御力：{u.CurrentDefense:F0}";
            if (_unitSpeed != null)     _unitSpeed.text     = $"速度：{u.CurrentSpeed:F0}";
            if (_unitShield != null)    _unitShield.text    = $"护盾值：{u.Shield:F0}";
            if (_unitDamageAmp != null) _unitDamageAmp.text = $"伤害增幅：{u.DamageBonus:P0}";
            if (_unitImmunity != null)  _unitImmunity.text  = $"伤害减免：{u.DamageReduction:P0}";

            // Effects
            RefreshEffects(u);
        }

        // ============================================================
        // Effects
        // ============================================================
        private void RefreshEffects(BattleUnitInstance unit)
        {
            if (_unitEffectsContainer == null || _effectPrefab == null) return;

            EnsureTooltip();
            HideTooltip();

            for (int i = _unitEffectsContainer.childCount - 1; i >= 0; i--)
                Destroy(_unitEffectsContainer.GetChild(i).gameObject);

            foreach (var effect in unit.Effects)
            {
                var go = Instantiate(_effectPrefab, _unitEffectsContainer);
                go.name = effect.Template.Id;
                var img = go.transform.Find("EffectImage")?.GetComponent<Image>();
                if (img != null)
                {
                    var spr = LoadEffectIcon(effect.Template.Id);
                    if (spr != null) img.sprite = spr;
                }

                var e = effect;
                AddTrigger(go, EventTriggerType.PointerEnter, _ => ShowTooltip(e));
                AddTrigger(go, EventTriggerType.PointerExit, _ => HideTooltip());
                AddTrigger(go, EventTriggerType.PointerDown, _ => HideTooltip());
            }
        }

        private void EnsureTooltip()
        {
            if (_tooltipGO != null) return;
            if (_tooltipPrefab != null)
            {
                _tooltipGO = Instantiate(_tooltipPrefab, transform);
                _tooltipGO.name = "EffectTooltip";
            }
            else
            {
                _tooltipGO = new GameObject("EffectTooltip", typeof(RectTransform));
                _tooltipGO.transform.SetParent(transform, false);
                var rt = _tooltipGO.GetComponent<RectTransform>();
                rt.pivot = Vector2.zero;
                rt.sizeDelta = new Vector2(280, 80);
                var bg = _tooltipGO.AddComponent<Image>();
                bg.color = new Color(0, 0, 0, 0.85f);
                bg.raycastTarget = false;
                var txt = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                txt.transform.SetParent(_tooltipGO.transform, false);
                txt.fontSize = 18;
                txt.color = Color.white;
                txt.alignment = TextAlignmentOptions.Left;
                txt.raycastTarget = false;
                txt.rectTransform.anchorMin = Vector2.zero;
                txt.rectTransform.anchorMax = Vector2.one;
                txt.rectTransform.offsetMin = new Vector2(8, 4);
                txt.rectTransform.offsetMax = new Vector2(-8, -4);
            }

            // Ensure tooltip doesn't block raycasts
            var cg = _tooltipGO.GetComponent<CanvasGroup>();
            if (cg == null) cg = _tooltipGO.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            var tooltipImg = _tooltipGO.GetComponent<Image>();
            if (tooltipImg != null) tooltipImg.raycastTarget = false;

            // Hide tooltip on any click anywhere in the panel
            var panelTrigger = GetComponent<EventTrigger>();
            if (panelTrigger == null) panelTrigger = gameObject.AddComponent<EventTrigger>();
            var clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            clickEntry.callback.AddListener(_ => HideTooltip());
            panelTrigger.triggers.Add(clickEntry);

            _tooltipGO.SetActive(false);
        }

        private void ShowTooltip(BattleEffectInstance effect)
        {
            if (_tooltipGO == null) return;
            var txt = _tooltipGO.GetComponentInChildren<TextMeshProUGUI>();
            if (txt == null) return;

            var sb = new StringBuilder();
            sb.AppendLine(effect.Template.DisplayName ?? effect.Template.Id);
            if (effect.CurrentStackCount > 1)
                sb.AppendLine($"层数：{effect.CurrentStackCount}");
            sb.Append($"剩余回合：{effect.RemainingTurns}");

            txt.text = sb.ToString();
            _tooltipGO.SetActive(true);

            // Position at mouse with offset, bottom-left pivot
            var rt = _tooltipGO.GetComponent<RectTransform>();
            Vector2 mousePos = Input.mousePosition;
            rt.position = mousePos + new Vector2(16, 8);
        }

        private void HideTooltip()
        {
            if (_tooltipGO != null) _tooltipGO.SetActive(false);
        }

        private static void AddTrigger(GameObject go, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
        {
            var trigger = go.GetComponent<EventTrigger>();
            if (trigger == null) trigger = go.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(action);
            trigger.triggers.Add(entry);
        }

        // ============================================================
        // Skill Bar
        // ============================================================
        private void BuildSkills(BattleUnitInstance unit)
        {
            ClearSkills();

            if (_skillListPadding == null) return;
            if (_skillEntryPrefab == null) { Debug.LogError("[BP] SkillEntry prefab not loaded"); return; }

            // Show SkillUnitInfo for acting unit
            if (_skillUnitInfoGO != null)
            {
                _skillUnitInfoGO.SetActive(true);
                if (_skillUnitInfoAvatar != null)
                {
                    var icon = LoadUnitIcon(unit.Id);
                    if (icon != null) _skillUnitInfoAvatar.sprite = icon;
                }
                if (_skillUnitInfoName != null)
                    _skillUnitInfoName.text = unit.DisplayName;
            }

            // Show Empty placeholder
            if (_skillEmptyGO != null)
                _skillEmptyGO.SetActive(true);

            // Build skill entries
            _currentSkills.Clear();
            _selectedEntryIdx = -1;
            var skills = _battleManager.GetCastableSkills(unit);
            var seen = new HashSet<string>();
            int idx = 0;

            foreach (var (s, t) in skills)
            {
                if (!seen.Add(s.Id)) continue;

                GameObject entry;
                if (idx < _skillEntryGOs.Count)
                {
                    entry = _skillEntryGOs[idx];
                    entry.SetActive(true);
                }
                else
                {
                    entry = Instantiate(_skillEntryPrefab, _skillListPadding);
                    _skillEntryGOs.Add(entry);
                }

                // Reset entry image to normal
                var entryImg = entry.GetComponent<Image>();
                if (entryImg != null && _skillEntryNormalSprite != null)
                    entryImg.sprite = _skillEntryNormalSprite;

                // Set skill icon on Padding/Image
                var iconT = entry.transform.Find("Padding/Image");
                if (iconT != null)
                {
                    var icon = iconT.GetComponent<Image>();
                    if (icon != null)
                    {
                        var spr = Resources.Load<Sprite>($"Art/Sprites/UI/Icons/skills/{s.Id}_icon");
                        if (spr != null) icon.sprite = spr;
                    }
                }

                // Set skill name
                var nameT = entry.transform.Find("Padding/Name");
                if (nameT != null)
                {
                    var lbl = nameT.GetComponent<TextMeshProUGUI>();
                    if (lbl != null) lbl.text = SkillLabel(s);
                }

                var btn = entry.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    int i = idx;
                    btn.onClick.AddListener(() => SelectSkill(i));
                }

                _currentSkills.Add(s);
                idx++;
            }

            for (int i = idx; i < _skillEntryGOs.Count; i++)
                _skillEntryGOs[i].SetActive(false);
        }

        private void SelectSkill(int idx)
        {
            if (idx < 0 || idx >= _currentSkills.Count) return;

            // Revert previous entry
            if (_selectedEntryIdx >= 0 && _selectedEntryIdx < _skillEntryGOs.Count)
            {
                var prevImg = _skillEntryGOs[_selectedEntryIdx].GetComponent<Image>();
                if (prevImg != null && _skillEntryNormalSprite != null)
                    prevImg.sprite = _skillEntryNormalSprite;
            }

            _selectedEntryIdx = idx;
            _selectedSkill = _currentSkills[idx];

            // Set pressed sprite on selected entry
            if (idx < _skillEntryGOs.Count)
            {
                var img = _skillEntryGOs[idx].GetComponent<Image>();
                if (img != null && _skillEntryPressedSprite != null)
                    img.sprite = _skillEntryPressedSprite;
            }

            ShowSkillInfo(_selectedSkill.DisplayName, _selectedSkill.Description);

            if (_confirmButton != null)
                _confirmButton.gameObject.SetActive(true);

            RefreshSkillState();
        }

        private void RefreshSkillState()
        {
            if (_selectedSkill == null || _actingUnit == null) return;

            bool isSingleTarget = _selectedSkill.TargetType is TargetType.SingleEnemy
                or TargetType.SingleAlly or TargetType.SingleBoth;

            bool canCast = true;
            string desc = _selectedSkill.Description;

            if (isSingleTarget && _selectedUnit != null)
            {
                canCast = _selectedSkill.CanCast(_actingUnit, _selectedUnit)
                       && TargetMatchesSkill(_selectedSkill.TargetType, _selectedUnit);
                if (!canCast)
                    desc += "\n" + GetTargetRestriction(_selectedSkill.TargetType);
            }

            if (_skillInfoDescription != null)
                _skillInfoDescription.text = desc;

            if (_confirmButton != null)
                _confirmButton.interactable = canCast;
        }

        private void ShowSkillInfo(string name, string desc)
        {
            if (_skillInfoPadding != null) _skillInfoPadding.SetActive(true);
            if (_skillInfoName != null) _skillInfoName.text = name;
            if (_skillInfoDescription != null) _skillInfoDescription.text = desc;
        }

        private void OnConfirm()
        {
            if (_actingUnit == null || _selectedSkill == null) return;

            bool isMultiOrSelf = _selectedSkill.TargetType is TargetType.AllEnemies
                or TargetType.AllAllies or TargetType.AllBoth
                or TargetType.SingleSelf;

            BattleUnitInstance target;
            if (isMultiOrSelf)
            {
                target = ResolveMultiTarget(_selectedSkill);
                if (target == null) return;
            }
            else
            {
                if (_selectedUnit == null) return;
                if (!_selectedSkill.CanCast(_actingUnit, _selectedUnit)) return;
                target = _selectedUnit;
            }

            _battleManager.SubmitPlayerAction(_selectedSkill, target);
            ClearSkills();
            _actingUnit = null;
        }

        private BattleUnitInstance ResolveMultiTarget(Skill skill)
        {
            if (skill.TargetType == TargetType.SingleSelf)
                return _actingUnit;

            var skills = _battleManager.GetCastableSkills(_actingUnit);
            foreach (var (s, t) in skills)
                if (s.Id == skill.Id)
                    return t;
            return null;
        }

        private void ClearSkills()
        {
            if (_skillUnitInfoGO != null) _skillUnitInfoGO.SetActive(false);
            if (_skillEmptyGO != null) _skillEmptyGO.SetActive(false);
            foreach (var g in _skillEntryGOs) g.SetActive(false);
            _currentSkills.Clear();
            _selectedSkill = null;
            _selectedEntryIdx = -1;

            if (_skillInfoPadding != null) _skillInfoPadding.SetActive(false);
            if (_skillInfoName != null) _skillInfoName.text = "";
            if (_skillInfoDescription != null) _skillInfoDescription.text = "";
            if (_confirmButton != null)
            {
                _confirmButton.gameObject.SetActive(false);
                _confirmButton.interactable = true;
            }
        }

        // ============================================================
        // Action Queue
        // ============================================================
        private void RefreshQueue()
        {
            const int MAX_DISPLAY = 6;

            if (_actionQueuePadding == null || _battleManager == null) return;
            if (_actionQueueEntryPrefab == null) { Debug.LogError("[BP] ActionQueueEntry prefab not loaded"); return; }

            // Destroy old highlight
            if (_queueHighlightGO != null) { Destroy(_queueHighlightGO); _queueHighlightGO = null; }

            int idx = 0;
            for (int i = 0; i < _battleManager.ActionQueue.Count && idx < MAX_DISPLAY; i++)
            {
                var u = _battleManager.ActionQueue[i];
                if (u == null || !u.IsAlive) continue;

                GameObject entry;
                if (idx < _queueEntryGOs.Count)
                {
                    entry = _queueEntryGOs[idx];
                    entry.SetActive(true);
                }
                else
                {
                    entry = Instantiate(_actionQueueEntryPrefab, _actionQueuePadding);
                    _queueEntryGOs.Add(entry);
                }

                var img = entry.GetComponent<Image>();
                if (img == null) img = entry.GetComponentInChildren<Image>();
                if (img != null)
                {
                    var icon = LoadUnitIcon(u.Id);
                    if (icon != null) img.sprite = icon;
                }

                bool isCurrent = u == _battleManager.ActionQueue.Current;
                var nameT = entry.GetComponentInChildren<TextMeshProUGUI>();
                if (nameT != null)
                {
                    nameT.text = isCurrent ? $"[{u.DisplayName}]" : u.DisplayName;
                    nameT.color = isCurrent ? new Color(0.8f, 0.15f, 0.05f) : Color.white;
                }

                // QueueHighlight under the first (current) entry
                if (idx == 0 && _queueHighlightPrefab != null)
                {
                    _queueHighlightGO = Instantiate(_queueHighlightPrefab, entry.transform);
                    _queueHighlightGO.name = "QueueHighlight";
                }

                idx++;
            }

            for (int i = idx; i < _queueEntryGOs.Count; i++)
                _queueEntryGOs[i].SetActive(false);
        }

        // ============================================================
        // Resource helpers
        // ============================================================
        private static Sprite LoadUnitIcon(string unitId)
            => Resources.Load<Sprite>($"Art/Sprites/UI/Icons/battleunits/{unitId}_icon");

        private static Sprite LoadEffectIcon(string effectId)
            => Resources.Load<Sprite>($"Art/Sprites/UI/Icons/effects/{effectId}_icon");

        private static string SkillLabel(Skill s)
        {
            return s.TargetType switch
            {
                TargetType.AllEnemies or TargetType.AllAllies or TargetType.AllBoth => $"{s.DisplayName} [全体]",
                TargetType.SingleSelf => $"{s.DisplayName} [自身]",
                _ => s.DisplayName,
            };
        }

        private bool TargetMatchesSkill(TargetType type, BattleUnitInstance target)
        {
            if (target == null || _actingUnit == null || _battleManager == null) return false;
            bool targetIsAlly = _battleManager.IsPlayerUnit(target) == _battleManager.IsPlayerUnit(_actingUnit);
            return type switch
            {
                TargetType.SingleEnemy or TargetType.AllEnemies => !targetIsAlly && target.IsAlive,
                TargetType.SingleAlly or TargetType.AllAllies   =>  targetIsAlly && target.IsAlive,
                TargetType.SingleBoth or TargetType.AllBoth     => target.IsAlive,
                TargetType.SingleSelf                           => target == _actingUnit,
                _ => false,
            };
        }

        private static string GetTargetRestriction(TargetType type) => type switch
        {
            TargetType.SingleEnemy => "目标限制：敌方单体",
            TargetType.AllEnemies => "目标限制：敌方全体",
            TargetType.SingleAlly => "目标限制：我方单体",
            TargetType.AllAllies => "目标限制：我方全体",
            TargetType.SingleBoth => "目标限制：任意单体",
            TargetType.AllBoth => "目标限制：任意全体",
            TargetType.SingleSelf => "目标限制：自身",
            _ => "",
        };

        // ============================================================
        // Navigation
        // ============================================================
        private void OnBack() { UIManager.Instance.Pop(); }
    }
}

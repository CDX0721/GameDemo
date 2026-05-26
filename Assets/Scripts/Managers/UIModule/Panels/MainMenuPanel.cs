using UnityEngine;
using UnityEngine.UI;

namespace GameDemo.UI.Panels
{
    /// <summary>
    /// Full-screen main menu. Self-initializes if no Inspector bindings provided.
    /// </summary>
    public class MainMenuPanel : UIPanel
    {
        [Header("UI References")]
        [SerializeField] private Text _titleText;
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _quitButton;

        protected override void OnOpen()
        {
            if (_titleText == null) EnsureUI();
        }

        protected override void OnShow()
        {
            _startButton?.onClick.AddListener(HandleStart);
            _settingsButton?.onClick.AddListener(HandleSettings);
            _quitButton?.onClick.AddListener(HandleQuit);
        }

        protected override void OnHide()
        {
            _startButton?.onClick.RemoveListener(HandleStart);
            _settingsButton?.onClick.RemoveListener(HandleSettings);
            _quitButton?.onClick.RemoveListener(HandleQuit);
        }

        private void EnsureUI()
        {
            // Background
            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.15f, 1f);

            // Title
            var tgo = new GameObject("Title", typeof(RectTransform));
            tgo.transform.SetParent(transform, false);
            _titleText = tgo.AddComponent<Text>();
            _titleText.text = "CODENAME TRAVERSE"; _titleText.fontSize = 60;
            _titleText.alignment = TextAnchor.MiddleCenter; _titleText.color = Color.white;
            _titleText.font = GetFont();
            var tr = tgo.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0.5f, 0.75f); tr.anchorMax = new Vector2(0.5f, 0.75f);
            tr.sizeDelta = new Vector2(600, 100); tr.anchoredPosition = Vector2.zero;

            _startButton = MakeButton("Start Game", 80);
            _settingsButton = MakeButton("Settings", 0);
            _quitButton = MakeButton("Quit", -80);
        }

        private Button MakeButton(string label, float yPos)
        {
            var go = new GameObject(label + "Btn", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            go.AddComponent<Image>().color = new Color(0.2f, 0.25f, 0.35f, 1f);
            var btn = go.AddComponent<Button>();
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.4f); rt.anchorMax = new Vector2(0.5f, 0.4f);
            rt.sizeDelta = new Vector2(280, 60); rt.anchoredPosition = new Vector2(0, yPos);

            var lgo = new GameObject("Label", typeof(RectTransform));
            lgo.transform.SetParent(go.transform, false);
            var t = lgo.AddComponent<Text>();
            t.text = label; t.fontSize = 24; t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter; t.font = GetFont();
            var lrt = lgo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            return btn;
        }

        private static Font GetFont()
        {
            var font = Font.CreateDynamicFontFromOSFont("Arial", 48);
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font;
        }

        private void HandleStart()
        {
            UIManager.Instance.Push<BattlePanel>();
        }

        private void HandleSettings()
        {
            Debug.Log("[MainMenu] Settings not yet implemented.");
        }

        private void HandleQuit()
        {
            Application.Quit();
        }
    }
}

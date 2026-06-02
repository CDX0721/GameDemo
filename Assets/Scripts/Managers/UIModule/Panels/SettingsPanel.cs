using GameDemo.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace GameDemo.UI.Panels
{
    public class SettingsPanel : UIPanel
    {
        private Slider _bgmSlider;
        private Slider _sfxSlider;
        private Text _bgmLabel;
        private Text _sfxLabel;
        private Button _backButton;

        private const string SETTINGS_PATH = "Configs/GeneralSettings";

        protected override void OnOpen()
        {
            BuildUI();
            LoadSettings();
        }

        protected override void OnShow()
        {
            _backButton?.onClick.AddListener(HandleBack);
            _bgmSlider?.onValueChanged.AddListener(OnBgmChanged);
            _sfxSlider?.onValueChanged.AddListener(OnSfxChanged);
        }

        protected override void OnHide()
        {
            _backButton?.onClick.RemoveListener(HandleBack);
            _bgmSlider?.onValueChanged.RemoveListener(OnBgmChanged);
            _sfxSlider?.onValueChanged.RemoveListener(OnSfxChanged);
        }

        private void BuildUI()
        {
            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.15f, 1f);

            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(transform, false);
            var titleT = titleGo.AddComponent<Text>();
            titleT.text = "Settings"; titleT.fontSize = 48;
            titleT.alignment = TextAnchor.MiddleCenter; titleT.color = Color.white;
            titleT.font = GetFont();
            var tr = titleGo.GetComponent<RectTransform>();
            tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.8f);
            tr.sizeDelta = new Vector2(400, 70); tr.anchoredPosition = Vector2.zero;

            _bgmSlider = MakeSlider("BGM", 160, OnBgmChanged);
            _sfxSlider = MakeSlider("SFX", 80, OnSfxChanged);

            _backButton = MakeButton("Back", -120);
        }

        private Slider MakeSlider(string label, float yPos, UnityEngine.Events.UnityAction<float> onChange)
        {
            var go = new GameObject(label + "Slider", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(500, 60);
            rt.anchoredPosition = new Vector2(0, yPos);

            // Label
            var lblGo = new GameObject("Label", typeof(RectTransform));
            lblGo.transform.SetParent(go.transform, false);
            var t = lblGo.AddComponent<Text>();
            t.text = label; t.fontSize = 28; t.color = Color.white;
            t.alignment = TextAnchor.MiddleLeft; t.font = GetFont();
            var lrt = lblGo.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(0.2f, 1);
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;

            // Slider
            var sliderGo = new GameObject("Slider", typeof(RectTransform));
            sliderGo.transform.SetParent(go.transform, false);
            var slider = sliderGo.AddComponent<Slider>();
            slider.minValue = 0; slider.maxValue = 1; slider.value = 1;
            slider.onValueChanged.AddListener(onChange);
            var srt = sliderGo.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.25f, 0.15f); srt.anchorMax = new Vector2(0.75f, 0.85f);
            srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;

            // Background
            var bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(sliderGo.transform, false);
            bgGo.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f);
            var bgrt = bgGo.GetComponent<RectTransform>();
            bgrt.anchorMin = Vector2.zero; bgrt.anchorMax = Vector2.one;
            bgrt.offsetMin = Vector2.zero; bgrt.offsetMax = Vector2.zero;

            // Fill
            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(sliderGo.transform, false);
            fillGo.AddComponent<Image>().color = new Color(0.3f, 0.5f, 0.8f);
            var frt = fillGo.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;

            // Handle
            var handleGo = new GameObject("Handle", typeof(RectTransform));
            handleGo.transform.SetParent(sliderGo.transform, false);
            handleGo.AddComponent<Image>().color = Color.white;
            var hrt = handleGo.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0.5f, 0); hrt.anchorMax = new Vector2(0.5f, 1);
            hrt.sizeDelta = new Vector2(20, 0);

            slider.fillRect = frt;
            slider.handleRect = hrt;
            slider.targetGraphic = handleGo.GetComponent<Image>();

            // Value label
            var valGo = new GameObject("Value", typeof(RectTransform));
            valGo.transform.SetParent(go.transform, false);
            var tv = valGo.AddComponent<Text>();
            tv.text = "100%"; tv.fontSize = 22; tv.color = Color.white;
            tv.alignment = TextAnchor.MiddleCenter; tv.font = GetFont();
            var vrt = valGo.GetComponent<RectTransform>();
            vrt.anchorMin = new Vector2(0.78f, 0); vrt.anchorMax = new Vector2(1, 1);
            vrt.offsetMin = Vector2.zero; vrt.offsetMax = Vector2.zero;

            if (label == "BGM") _bgmLabel = tv;
            else _sfxLabel = tv;

            return slider;
        }

        private Button MakeButton(string label, float yPos)
        {
            var go = new GameObject(label + "Btn", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            go.AddComponent<Image>().color = new Color(0.2f, 0.25f, 0.35f);
            var btn = go.AddComponent<Button>();
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(240, 50);
            rt.anchoredPosition = new Vector2(0, yPos);

            var lgo = new GameObject("Label", typeof(RectTransform));
            lgo.transform.SetParent(go.transform, false);
            var t = lgo.AddComponent<Text>();
            t.text = label; t.fontSize = 24; t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter; t.font = GetFont();
            var lrt = lgo.GetComponent<RectTransform>();
            lrt.anchorMin = lrt.anchorMax = Vector2.one * 0.5f;
            lrt.sizeDelta = new Vector2(200, 40);
            lrt.anchoredPosition = Vector2.zero;
            return btn;
        }

        private void OnBgmChanged(float val)
        {
            AudioManager.Instance?.SetMasterBgmVolume(val);
            if (_bgmLabel != null) _bgmLabel.text = $"{Mathf.RoundToInt(val * 100)}%";
            SaveSettings();
        }

        private void OnSfxChanged(float val)
        {
            AudioManager.Instance?.SetMasterSfxVolume(val);
            if (_sfxLabel != null) _sfxLabel.text = $"{Mathf.RoundToInt(val * 100)}%";
            SaveSettings();
        }

        private void HandleBack()
        {
            UIManager.Instance.Pop();
        }

        private static string SavePath =>
            System.IO.Path.Combine(Application.persistentDataPath, "GeneralSettings.json");

        private void LoadSettings()
        {
            string jsonText = null;
            if (System.IO.File.Exists(SavePath))
                jsonText = System.IO.File.ReadAllText(SavePath);
            else
                jsonText = AssetManager.Instance.Load<TextAsset>(SETTINGS_PATH)?.text;

            if (string.IsNullOrEmpty(jsonText)) return;
            try
            {
                var data = JsonUtility.FromJson<SettingsData>(jsonText);
                _bgmSlider?.SetValueWithoutNotify(data.bgmVolume);
                _sfxSlider?.SetValueWithoutNotify(data.sfxVolume);
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.SetMasterBgmVolume(data.bgmVolume);
                    AudioManager.Instance.SetMasterSfxVolume(data.sfxVolume);
                }
                if (_bgmLabel != null) _bgmLabel.text = $"{Mathf.RoundToInt(data.bgmVolume * 100)}%";
                if (_sfxLabel != null) _sfxLabel.text = $"{Mathf.RoundToInt(data.sfxVolume * 100)}%";
            }
            catch { }
        }

        private void SaveSettings()
        {
            var data = new SettingsData
            {
                bgmVolume = AudioManager.Instance?.MasterBgmVolume ?? 1f,
                sfxVolume = AudioManager.Instance?.MasterSfxVolume ?? 1f
            };
            System.IO.File.WriteAllText(SavePath, JsonUtility.ToJson(data));
        }

        private static Font GetFont()
        {
            var font = Font.CreateDynamicFontFromOSFont("Arial", 48);
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font;
        }

        [System.Serializable]
        private class SettingsData
        {
            public float bgmVolume = 1f;
            public float sfxVolume = 1f;
        }
    }
}

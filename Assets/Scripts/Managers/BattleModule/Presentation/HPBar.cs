using UnityEngine;

public class HPBar : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _border = null!;
    [SerializeField] private SpriteRenderer _background = null!;
    [SerializeField] private SpriteRenderer _fill = null!;

    private static Sprite _white;
    private float _maxHp;
    private float _barWidth = 5.0f;
    private float _barHeight = 0.6f;
    private float _borderPad = 0.10f;

    void Awake()
    {
        if (_white == null)
            _white = CreateSprite();
    }

    public void Setup(float maxHp, float barWidth = 5.0f)
    {
        _maxHp = maxHp;
        _barWidth = barWidth;

        if (_border == null)     { _border = AddChild("Border"); }
        if (_background == null) { _background = AddChild("BG"); }
        if (_fill == null)       { _fill = AddChild("Fill"); }

        // 边框左移以包裹背景：左边框左边界在 -borderPad，右边框右边界在 barWidth+borderPad
        _border.sprite = _white;
        _border.transform.localPosition = new Vector3(-_borderPad, 0f, 0f);
        _border.transform.localScale = new Vector3(_barWidth + _borderPad * 2f, _barHeight + _borderPad * 2f, 1f);
        _border.color = Color.white;
        _border.sortingOrder = 9;

        // 背景和填充从 x=0 开始，右侧到 barWidth，不覆盖边框
        _background.sprite = _white;
        _background.transform.localPosition = Vector3.zero;
        _background.transform.localScale = new Vector3(_barWidth, _barHeight, 1f);
        _background.color = new Color(0.08f, 0.08f, 0.08f, 0.95f);
        _background.sortingOrder = 10;

        _fill.sprite = _white;
        _fill.transform.localPosition = Vector3.zero;
        _fill.transform.localScale = new Vector3(_barWidth, _barHeight, 1f);
        _fill.sortingOrder = 11;

        // 整体居中于单位上方
        transform.localPosition = new Vector3(-_barWidth * 0.5f, 1.2f, 0f);
    }

    public void SetHP(float current, float max)
    {
        float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;

        if (_fill != null)
        {
            _fill.transform.localScale = new Vector3(_barWidth * ratio, _barHeight, 1f);
            _fill.color = ratio > 0.5f ? new Color(0.15f, 0.9f, 0.2f, 1f)
                        : ratio > 0.25f ? new Color(1f, 0.7f, 0.1f, 1f)
                        : new Color(1f, 0.1f, 0.1f, 1f);
        }
    }

    private SpriteRenderer AddChild(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        return go.AddComponent<SpriteRenderer>();
    }

    private static Sprite CreateSprite()
    {
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var c = new Color[16];
        for (int i = 0; i < c.Length; i++) c[i] = Color.white;
        tex.SetPixels(c);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0f, 0.5f), 4f);
    }
}

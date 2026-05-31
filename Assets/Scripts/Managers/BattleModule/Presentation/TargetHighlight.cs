using UnityEngine;

public class TargetHighlight : MonoBehaviour
{
    [SerializeField] private float _offsetX = 0f;
    [SerializeField] private float _offsetY = -1f;
    [SerializeField] private float _width    = 3f;
    [SerializeField] private float _height   = 4f;
    [SerializeField] private float _thickness = 0.08f;
    [SerializeField] private Color _color = new Color(1f, 0.9f, 0.2f, 1f);
    [SerializeField] private int _sortingOrder = 900;

    private bool _built;

    void Awake() => EnsureBuilt();

    private void EnsureBuilt()
    {
        if (_built) return;
        _built = true;

        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        var white = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

        float hw = _width / 2f - _thickness / 2f;
        float hh = _height / 2f - _thickness / 2f;

        AddBar("Top",    new Vector2(0,  hh),  new Vector2(_width, _thickness), white);
        AddBar("Bottom", new Vector2(0, -hh),  new Vector2(_width, _thickness), white);
        AddBar("Left",   new Vector2(-hw, 0),  new Vector2(_thickness, _height), white);
        AddBar("Right",  new Vector2( hw, 0),  new Vector2(_thickness, _height), white);

        transform.localPosition = new Vector3(_offsetX, _offsetY, 0);
        gameObject.SetActive(false);
    }

    private void AddBar(string name, Vector2 pos, Vector2 size, Sprite sprite)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = _color;
        sr.sortingOrder = _sortingOrder;
        sr.transform.localScale = new Vector3(size.x, size.y, 1f);
    }

    public void Show() { EnsureBuilt(); gameObject.SetActive(true); }
    public void Hide() { gameObject.SetActive(false); }
}

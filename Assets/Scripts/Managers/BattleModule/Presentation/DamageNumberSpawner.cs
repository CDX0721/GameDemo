using System.Collections;
using UnityEngine;

/// <summary>
/// 伤害数字生成器。挂到战场 World Space Canvas 上。
/// 数字浮现→上升→淡出，不阻塞战斗流程。
/// </summary>
public class DamageNumberSpawner : MonoBehaviour
{
    [SerializeField] private GameObject _damageNumberPrefab = null!;
    [SerializeField] private float _riseSpeed = 1.2f;
    [SerializeField] private float _lifetime = 0.8f;

    private static readonly Color TrueDamageColor = new Color(0f, 1f, 1f, 1f);  // #00FFFF

    /// <summary>在指定世界坐标显示伤害数字。</summary>
    public void SpawnDamage(Vector3 worldPosition, int damage)
    {
        SpawnDamage(worldPosition, damage, null);
    }

    /// <summary>在指定世界坐标显示伤害数字（可指定颜色）。</summary>
    public void SpawnDamage(Vector3 worldPosition, int damage, Color? color)
    {
        if (_damageNumberPrefab == null) return;

        var go = Instantiate(_damageNumberPrefab, transform);
        go.transform.position = worldPosition;

        var dn = go.GetComponent<DamageNumber>();
        if (dn != null)
            dn.Play(damage, _riseSpeed, _lifetime, color);
    }

    /// <summary>在指定世界坐标显示真实伤害数字（#00FFFF 青色）。</summary>
    public void SpawnTrueDamage(Vector3 worldPosition, int damage)
        => SpawnDamage(worldPosition, damage, TrueDamageColor);

    private static readonly Color HealColor = new Color(0.2f, 1f, 0.3f, 1f);

    /// <summary>在指定世界坐标显示治疗数字（绿色 +xxx）。</summary>
    public void SpawnHeal(Vector3 worldPosition, int amount)
    {
        if (_damageNumberPrefab == null) return;

        var go = Instantiate(_damageNumberPrefab, transform);
        go.transform.position = worldPosition;

        var dn = go.GetComponent<DamageNumber>();
        if (dn != null)
            dn.PlayHeal(amount, _riseSpeed, _lifetime);
    }
}

/// <summary>
/// 单个伤害数字。挂在预制体上。
/// </summary>
public class DamageNumber : MonoBehaviour
{
    [SerializeField] private TMPro.TextMeshPro _text = null!;

    void Awake()
    {
        if (_text == null)
            _text = GetComponent<TMPro.TextMeshPro>();
    }

    public void Play(int damage, float riseSpeed, float lifetime, Color? color = null)
    {
        if (_text != null)
        {
            _text.text = $"-{damage}";
            if (color.HasValue) _text.color = color.Value;
        }
        StartCoroutine(FloatAndFade(riseSpeed, lifetime));
    }

    public void PlayHeal(int amount, float riseSpeed, float lifetime)
    {
        if (_text != null)
        {
            _text.text = $"+{amount}";
            _text.color = new Color(0.2f, 1f, 0.3f, 1f);
        }
        StartCoroutine(FloatAndFade(riseSpeed, lifetime));
    }

    private IEnumerator FloatAndFade(float riseSpeed, float lifetime)
    {
        var startPos = transform.position;
        float elapsed = 0f;

        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / lifetime;
            transform.position = startPos + Vector3.up * riseSpeed * t;

            if (_text != null)
                _text.alpha = 1f - t;

            yield return null;
        }

        Destroy(gameObject);
    }

    /// <summary>创建默认预制体（不含 TMP 预制体时使用）。</summary>
    public static GameObject CreateDefaultPrefab()
    {
        var go = new GameObject("DamageNumber");
        var tmp = go.AddComponent<TMPro.TextMeshPro>();
        tmp.fontSize = 3.5f;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.35f, 0.25f, 1f);
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.sortingOrder = 100;
        go.AddComponent<DamageNumber>();
        return go;
    }
}

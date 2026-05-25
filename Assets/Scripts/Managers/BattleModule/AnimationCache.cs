using System;

/// <summary>
/// 动画帧的资源路径缓存（非 Sprite 引用，而是 Resources 路径）。
/// 由 Editor 工具自动填充，运行时通过 AssetManager 加载。
/// </summary>
public class AnimationCache : UnityEngine.ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        /// <summary>动画 key，如 "p_warrior_idle", "fx_slash"</summary>
        public string Key;
        /// <summary>精灵表 Resources 路径，如 "Art/Sprites/Units/p_warrior_idle"（不含扩展名）</summary>
        public string Path;
    }

    [UnityEngine.SerializeField] private Entry[] _entries = Array.Empty<Entry>();

    /// <summary>根据 key 获取 Resources 路径，未找到返回 null。</summary>
    public string GetPath(string key)
    {
        foreach (var e in _entries)
            if (e.Key == key)
                return e.Path;
        return null;
    }

    public bool HasKey(string key)
    {
        foreach (var e in _entries)
            if (e.Key == key) return true;
        return false;
    }

#if UNITY_EDITOR
    public void SetEntries(Entry[] entries)
    {
        _entries = entries;
    }
#endif
}

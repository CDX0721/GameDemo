using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameDemo
{
    /// <summary>
    /// Custom asset manager wrapping Unity's Resources API.
    /// Provides synchronous and asynchronous loading, reference-counted unloading,
    /// caching, and asset queries.
    /// </summary>
    public class AssetManager
    {
        static AssetManager _instance;
        public static AssetManager Instance => _instance ??= new AssetManager();

        readonly Dictionary<string, Object> _cache = new();
        readonly Dictionary<string, int> _refCounts = new();
        readonly Dictionary<string, AssetHandle> _activeHandles = new();

        AssetManager() { }

        #region Synchronous Load

        /// <summary>
        /// Synchronously load an asset from a Resources path.
        /// Returns cached asset if already loaded; increments reference count.
        /// </summary>
        public T Load<T>(string path) where T : Object
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[AssetManager] Load: path is null or empty.");
                return null;
            }

            if (_cache.TryGetValue(path, out var cached))
            {
                _refCounts[path]++;
                return cached as T;
            }

            var asset = Resources.Load<T>(path);
            if (asset != null)
            {
                _cache[path] = asset;
                _refCounts[path] = 1;
            }
            else
            {
                Debug.LogWarning($"[AssetManager] Load failed at path: \"{path}\" (type: {typeof(T).Name})");
            }
            return asset;
        }

        #endregion

        /// <summary>
        /// Synchronously load all sub-assets of a given type from a Resources path.
        /// Used for sliced sprite sheets where each frame is a sub-asset.
        /// Note: returned array is NOT cached by AssetManager; caller should cache if needed.
        /// </summary>
        public T[] LoadAll<T>(string path) where T : Object
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[AssetManager] LoadAll: path is null or empty.");
                return null;
            }
            return Resources.LoadAll<T>(path);
        }

        #region Asynchronous Load

        /// <summary>
        /// Asynchronously load an asset. Returns a handle immediately.
        /// If the asset is already cached, returns a completed handle.
        /// If already loading, returns the existing pending handle.
        /// </summary>
        public AssetHandle LoadAsync<T>(string path) where T : Object
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[AssetManager] LoadAsync: path is null or empty.");
                return null;
            }

            if (_cache.TryGetValue(path, out var cached))
            {
                _refCounts[path]++;
                return new AssetHandle(path, cached);
            }

            if (_activeHandles.TryGetValue(path, out var existing))
            {
                _refCounts[path]++;
                return existing;
            }

            var request = Resources.LoadAsync<T>(path);
            var handle = new AssetHandle(path, request);

            handle.OnCompleted += () =>
            {
                var result = handle.GetResult<T>();
                if (result != null)
                {
                    _cache[path] = result;
                    _refCounts[path] = 1;
                }
                else
                {
                    Debug.LogWarning($"[AssetManager] LoadAsync failed at path: \"{path}\" (type: {typeof(T).Name})");
                }
                _activeHandles.Remove(path);
            };

            if (!handle.IsDone)
                _activeHandles[path] = handle;

            return handle;
        }

        /// <summary>
        /// Coroutine-based async load with a callback on completion.
        /// </summary>
        public IEnumerator LoadAsyncCoroutine<T>(string path, Action<T> onLoaded) where T : Object
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[AssetManager] LoadAsyncCoroutine: path is null or empty.");
                yield break;
            }

            if (_cache.TryGetValue(path, out var cached))
            {
                _refCounts[path]++;
                onLoaded?.Invoke(cached as T);
                yield break;
            }

            if (_activeHandles.ContainsKey(path))
            {
                yield return new WaitUntil(() => _cache.ContainsKey(path));
                onLoaded?.Invoke(_cache[path] as T);
                yield break;
            }

            var request = Resources.LoadAsync<T>(path);
            yield return request;

            var asset = request.asset as T;
            if (asset != null)
            {
                _cache[path] = asset;
                _refCounts[path] = 1;
                onLoaded?.Invoke(asset);
            }
            else
            {
                Debug.LogWarning($"[AssetManager] LoadAsyncCoroutine failed at path: \"{path}\"");
                onLoaded?.Invoke(null);
            }
        }

        #endregion

        #region Unload

        /// <summary>
        /// Decrement the reference count for an asset. The asset is actually unloaded
        /// only when the count reaches zero.
        /// </summary>
        /// <returns>true if the asset was unloaded; false if still referenced or not found</returns>
        public bool Unload(string path)
        {
            if (!_cache.ContainsKey(path))
            {
                Debug.LogWarning($"[AssetManager] Unload: asset not in cache: \"{path}\"");
                return false;
            }

            _refCounts[path]--;
            if (_refCounts[path] <= 0)
            {
                var asset = _cache[path];
                _cache.Remove(path);
                _refCounts.Remove(path);
                Resources.UnloadAsset(asset);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Unload an asset by its handle.
        /// </summary>
        public bool Unload(AssetHandle handle)
        {
            if (handle == null) return false;
            return Unload(handle.Path);
        }

        /// <summary>
        /// Force-unload an asset regardless of its reference count.
        /// </summary>
        public void ForceUnload(string path)
        {
            if (!_cache.ContainsKey(path)) return;

            var asset = _cache[path];
            _cache.Remove(path);
            _refCounts.Remove(path);
            _activeHandles.Remove(path);
            Resources.UnloadAsset(asset);
        }

        /// <summary>
        /// Unload all assets that are no longer referenced, then invoke GC.
        /// </summary>
        public void UnloadUnused()
        {
            Resources.UnloadUnusedAssets();
            GC.Collect();
        }

        /// <summary>
        /// Clear the entire cache and unload all managed assets.
        /// </summary>
        public void ClearCache()
        {
            _cache.Clear();
            _refCounts.Clear();
            _activeHandles.Clear();
            Resources.UnloadUnusedAssets();
            GC.Collect();
        }

        #endregion

        #region Query

        /// <summary>
        /// Check whether an asset is currently cached.
        /// </summary>
        public bool IsLoaded(string path)
        {
            return !string.IsNullOrEmpty(path) && _cache.ContainsKey(path);
        }

        /// <summary>
        /// Check whether an asset is currently being loaded asynchronously.
        /// </summary>
        public bool IsLoading(string path)
        {
            return !string.IsNullOrEmpty(path) && _activeHandles.ContainsKey(path);
        }

        /// <summary>
        /// Retrieve a cached asset without incrementing the reference count.
        /// Returns null if not loaded.
        /// </summary>
        public T Get<T>(string path) where T : Object
        {
            if (!string.IsNullOrEmpty(path) && _cache.TryGetValue(path, out var cached))
                return cached as T;
            return null;
        }

        /// <summary>
        /// Get the current reference count for an asset path.
        /// </summary>
        public int GetRefCount(string path)
        {
            if (!string.IsNullOrEmpty(path) && _refCounts.TryGetValue(path, out var count))
                return count;
            return 0;
        }

        public int CacheCount => _cache.Count;
        public int ActiveLoadCount => _activeHandles.Count;

        #endregion
    }
}

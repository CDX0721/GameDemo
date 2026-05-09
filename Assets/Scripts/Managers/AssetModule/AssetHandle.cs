using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameDemo
{
    /// <summary>
    /// Handle for asynchronous asset loading. Returned by AssetManager.LoadAsync.
    /// Supports checking progress, waiting for completion, and retrieving the loaded asset.
    /// </summary>
    public class AssetHandle
    {
        public string Path { get; }
        public bool IsDone { get; private set; }
        public float Progress { get; private set; }

        Object _asset;
        ResourceRequest _request;
        event Action _onCompleted;

        /// <summary>
        /// Create a completed handle for a cached asset.
        /// </summary>
        internal AssetHandle(string path, Object asset)
        {
            Path = path;
            _asset = asset;
            IsDone = true;
            Progress = 1f;
        }

        /// <summary>
        /// Create a pending handle that waits on a ResourceRequest.
        /// </summary>
        internal AssetHandle(string path, ResourceRequest request)
        {
            Path = path;
            _request = request;
            IsDone = false;
            Progress = 0f;
            request.completed += OnRequestCompleted;
        }

        void OnRequestCompleted(AsyncOperation op)
        {
            IsDone = true;
            Progress = 1f;
            _asset = _request.asset;
            _onCompleted?.Invoke();
        }

        /// <summary>
        /// Get the loaded asset. Returns null if loading is not yet complete.
        /// </summary>
        public T GetResult<T>() where T : Object
        {
            return IsDone ? _asset as T : null;
        }

        /// <summary>
        /// Register a callback invoked when loading completes.
        /// If already complete, the callback fires immediately.
        /// </summary>
        public event Action OnCompleted
        {
            add
            {
                if (IsDone) value?.Invoke();
                else _onCompleted += value;
            }
            remove { _onCompleted -= value; }
        }
    }
}

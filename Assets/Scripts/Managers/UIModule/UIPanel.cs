using UnityEngine;

namespace GameDemo.UI
{
    /// <summary>
    /// 面板生命周期状态。Closed → Open → Active ⇄ Paused
    /// </summary>
    public enum PanelState
    {
        Closed,
        Open,
        Active,
        Paused
    }

    /// <summary>
    /// 所有 UI 面板的抽象基类。提供生命周期钩子、CanvasGroup 可见性管理、自动注册。
    /// 继承后在 Inspector 中挂载到 Canvas 下的 GameObject 上即可。
    /// </summary>
    public abstract class UIPanel : MonoBehaviour
    {
        [Header("Panel Settings")]
        [SerializeField] [Tooltip("是否在 Awake 时自动注册到 UIManager")] private bool _registerOnAwake = true;
        [SerializeField] [Tooltip("是否在 Awake 后初始隐藏")] private bool _startHidden = true;

        private CanvasGroup _canvasGroup;
        private bool _hasBeenOpened;

        /// <summary>面板当前所处的生命周期状态。</summary>
        public PanelState State { get; private set; } = PanelState.Closed;
        /// <summary>面板类型名称，用于调试。</summary>
        public string PanelName => GetType().Name;

        protected virtual void Awake()
        {
            EnsureCanvasGroup();

            if (_startHidden)
                SetCanvasVisible(false);

            if (_registerOnAwake)
                UIManager.Instance.Register(this);
        }

        protected virtual void OnDestroy()
        {
            if (_registerOnAwake)
                UIManager.Instance.Unregister(this);
        }

        /// <summary>首次显示时调用一次。适合初始化重型资源、播放入场动画。</summary>
        protected virtual void OnOpen() { }
        /// <summary>每次面板变为 Active 状态时调用。在此注册 UI 事件监听。</summary>
        protected virtual void OnShow() { }
        /// <summary>面板被完全隐藏时调用。在此注销 UI 事件监听。</summary>
        protected virtual void OnHide() { }
        /// <summary>面板被销毁前调用。最后清理机会。</summary>
        protected virtual void OnClose() { }
        /// <summary>被其他面板覆盖（暂停）时调用。可在此暂停动画或输入。</summary>
        protected virtual void OnPause() { }
        /// <summary>覆盖面板移除后恢复时调用。可在此恢复动画或刷新数据。</summary>
        protected virtual void OnResume() { }

        internal void DoShow()
        {
            if (!_hasBeenOpened)
            {
                _hasBeenOpened = true;
                State = PanelState.Open;
                OnOpen();
            }
            State = PanelState.Active;
            SetCanvasVisible(true);
            transform.SetAsLastSibling();
            OnShow();
        }

        internal void DoHide()
        {
            State = PanelState.Closed;
            SetCanvasVisible(false);
            OnHide();
        }

        internal void DoPause()
        {
            State = PanelState.Paused;
            SetCanvasVisible(false);
            OnPause();
        }

        internal void DoResume()
        {
            State = PanelState.Active;
            SetCanvasVisible(true);
            transform.SetAsLastSibling();
            OnResume();
        }

        internal void DoClose()
        {
            DoHide();
            OnClose();
            Destroy(gameObject);
        }

        private void EnsureCanvasGroup()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        private void SetCanvasVisible(bool visible)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }
    }
}

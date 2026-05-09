using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameDemo.UI
{
    /// <summary>
    /// 全局 UI 管理器单例，管理所有 UIPanel 的显示、隐藏与栈式导航。
    /// 不关注 UI 具体内容，只负责面板生命周期调度。
    /// </summary>
    public sealed class UIManager
    {
        private static readonly Lazy<UIManager> _instance =
            new Lazy<UIManager>(() => new UIManager(), LazyThreadSafetyMode.ExecutionAndPublication);

        public static UIManager Instance => _instance.Value;

        private readonly Dictionary<Type, UIPanel> _registry = new Dictionary<Type, UIPanel>();
        private readonly Stack<UIPanel> _stack = new Stack<UIPanel>();
        private UIPanel _currentPanel;
        private bool _initialized;
        private IPanelFactory _panelFactory;

        internal Canvas RootCanvas { get; private set; }
        internal MonoBehaviour CoroutineRunner { get; private set; }

        private UIManager() { }

        /// <summary>
        /// 由 UIRoot 调用，注入 Unity 上下文。未初始化前所有显示操作都会静默失败。
        /// </summary>
        public void Initialize(Canvas rootCanvas, MonoBehaviour coroutineRunner)
        {
            RootCanvas = rootCanvas ?? throw new ArgumentNullException(nameof(rootCanvas));
            CoroutineRunner = coroutineRunner ?? throw new ArgumentNullException(nameof(coroutineRunner));
            _initialized = true;
        }

        internal void UpdateUnityContext(Canvas rootCanvas, MonoBehaviour coroutineRunner)
        {
            RootCanvas = rootCanvas;
            CoroutineRunner = coroutineRunner;
        }

        internal void OnUIRootDestroyed()
        {
            _currentPanel = null;
            _stack.Clear();
            _registry.Clear();
        }

        /// <summary>
        /// 注入面板实例化策略。不设置时仅支持场景内预置面板。
        /// </summary>
        public void SetPanelFactory(IPanelFactory factory)
        {
            _panelFactory = factory;
        }

        /// <summary>
        /// 泛型显示面板。隐藏当前面板，显示目标面板。若目标已在栈中则弹出到目标。
        /// 返回面板实例，未初始化或无法解析时返回 null。
        /// </summary>
        public T Show<T>() where T : UIPanel
        {
            return (T)Show(typeof(T));
        }

        /// <summary>
        /// 指定 Type 显示面板。行为同 <see cref="Show{T}"/>。
        /// </summary>
        public UIPanel Show(Type panelType)
        {
            if (!ValidateInitialized()) return null;

            UIPanel panel = ResolvePanel(panelType);
            if (panel == null) return null;

            if (_currentPanel == panel)
                return panel;

            if (IsInStack(panel))
            {
                return PopTo(panelType);
            }

            if (_currentPanel != null)
            {
                _currentPanel.DoPause();
                _stack.Push(_currentPanel);
            }

            _stack.Push(panel);
            _currentPanel = panel;
            panel.DoShow();
            return panel;
        }

        /// <summary>
        /// 泛型隐藏指定面板。若为当前面板则恢复栈中下一层。
        /// </summary>
        public void Hide<T>() where T : UIPanel
        {
            Hide(typeof(T));
        }

        public void Hide(Type panelType)
        {
            if (!ValidateInitialized()) return;

            if (!_registry.TryGetValue(panelType, out UIPanel panel)) return;

            if (_currentPanel == panel)
            {
                _currentPanel.DoHide();
                _currentPanel = null;

                if (_stack.Count > 0)
                    _stack.Pop();

                if (_stack.Count > 0)
                {
                    _currentPanel = _stack.Peek();
                    _currentPanel.DoResume();
                }
            }
            else if (IsInStack(panel))
            {
                RemoveFromStack(panel);
                panel.DoHide();
            }
        }

        /// <summary>
        /// 隐藏所有面板并清空导航栈。
        /// </summary>
        public void HideAll()
        {
            if (!ValidateInitialized()) return;

            if (_currentPanel != null)
            {
                _currentPanel.DoHide();
                _currentPanel = null;
            }

            while (_stack.Count > 0)
            {
                _stack.Pop().DoHide();
            }
        }

        /// <summary>
        /// 泛型压入面板到栈顶。暂停当前面板，新面板覆盖显示，可通过 <see cref="Pop"/> 返回。
        /// </summary>
        public T Push<T>() where T : UIPanel
        {
            return (T)Push(typeof(T));
        }

        /// <summary>
        /// 指定 Type 压入面板。行为同 <see cref="Push{T}"/>。
        /// </summary>
        public UIPanel Push(Type panelType)
        {
            if (!ValidateInitialized()) return null;

            UIPanel panel = ResolvePanel(panelType);
            if (panel == null) return null;

            if (_currentPanel != null)
            {
                _currentPanel.DoPause();
                _stack.Push(_currentPanel);
            }

            _stack.Push(panel);
            _currentPanel = panel;
            panel.DoShow();
            return panel;
        }

        /// <summary>
        /// 弹出栈顶面板并隐藏，恢复到前一个面板。栈内 ≤1 项时打印警告，无操作。
        /// </summary>
        public void Pop()
        {
            if (!ValidateInitialized()) return;

            if (_stack.Count <= 1)
            {
                Debug.LogWarning("[UIManager] Pop() called but stack has 1 or fewer items.");
                return;
            }

            _stack.Pop().DoHide();

            UIPanel resume = _stack.Peek();
            _currentPanel = resume;
            resume.DoResume();
        }

        /// <summary>
        /// 泛型弹出栈顶面板直到找到目标类型并恢复。目标不在栈中时打印错误。
        /// </summary>
        public T PopTo<T>() where T : UIPanel
        {
            return (T)PopTo(typeof(T));
        }

        /// <summary>
        /// 指定 Type 弹出面板。行为同 <see cref="PopTo{T}"/>。
        /// </summary>
        public UIPanel PopTo(Type panelType)
        {
            if (!ValidateInitialized()) return null;

            if (!IsInStack(panelType))
            {
                Debug.LogError($"[UIManager] PopTo<{panelType.Name}>() failed: panel not found in stack.");
                return null;
            }

            while (_stack.Count > 0)
            {
                UIPanel top = _stack.Peek();
                if (panelType.IsInstanceOfType(top))
                {
                    _currentPanel = top;
                    top.DoResume();
                    return top;
                }
                _stack.Pop().DoHide();
            }

            return null;
        }

        /// <summary>
        /// 弹出所有面板只保留栈底，恢复栈底面板。
        /// </summary>
        public void PopToRoot()
        {
            if (!ValidateInitialized()) return;

            if (_stack.Count <= 1) return;

            while (_stack.Count > 1)
            {
                _stack.Pop().DoHide();
            }

            UIPanel root = _stack.Peek();
            _currentPanel = root;
            root.DoResume();
        }

        /// <summary>
        /// 获取已注册的面板实例。未注册时尝试通过工厂创建。
        /// </summary>
        public T Get<T>() where T : UIPanel
        {
            return (T)Get(typeof(T));
        }

        public UIPanel Get(Type type)
        {
            _registry.TryGetValue(type, out UIPanel panel);
            return panel;
        }

        /// <summary>
        /// 获取当前活动面板，无活动面板时返回 null。
        /// </summary>
        public UIPanel GetActivePanel()
        {
            return _currentPanel;
        }

        /// <summary>
        /// 检查指定类型面板是否为当前活动面板。
        /// </summary>
        public bool IsShowing<T>() where T : UIPanel
        {
            return IsShowing(typeof(T));
        }

        public bool IsShowing(Type type)
        {
            return _currentPanel != null && type.IsInstanceOfType(_currentPanel);
        }

        public bool IsInitialized => _initialized;
        public int StackCount => _stack.Count;

        /// <summary>
        /// 由 UIPanel.Awake 调用，将面板注册到管理器。重复类型覆盖并打印警告。
        /// </summary>
        internal void Register(UIPanel panel)
        {
            if (panel == null) return;

            Type key = panel.GetType();

            if (_registry.ContainsKey(key))
            {
                UIPanel existing = _registry[key];
                if (existing != null && existing != panel)
                {
                    Debug.LogWarning($"[UIManager] Duplicate registration for type {key.Name}. Overwriting.");
                }
            }

            _registry[key] = panel;
        }

        /// <summary>
        /// 由 UIPanel.OnDestroy 调用，从管理器移除面板并进行必要的栈清理。
        /// </summary>
        internal void Unregister(UIPanel panel)
        {
            if (panel == null) return;

            Type key = panel.GetType();

            if (_registry.TryGetValue(key, out UIPanel existing) && existing == panel)
            {
                _registry.Remove(key);
            }

            if (_currentPanel == panel)
            {
                _currentPanel = null;
            }

            RemoveFromStack(panel);
        }

        private UIPanel ResolvePanel(Type panelType)
        {
            if (_registry.TryGetValue(panelType, out UIPanel panel))
            {
                if (!IsPanelAlive(panel))
                {
                    _registry.Remove(panelType);
                    panel = null;
                }
            }

            if (panel == null && _panelFactory != null)
            {
                panel = _panelFactory.CreatePanel(panelType);
            }

            if (panel == null)
            {
                Debug.LogError(
                    $"[UIManager] Cannot resolve panel of type {panelType.Name}. " +
                    "Ensure it is pre-placed in scene or a PanelFactory is set.");
            }

            return panel;
        }

        private bool ValidateInitialized()
        {
            if (!_initialized)
            {
                Debug.LogError("[UIManager] Not initialized. Ensure a UIRoot exists in the scene.");
                return false;
            }
            return true;
        }

        private bool IsInStack(UIPanel panel)
        {
            foreach (UIPanel item in _stack)
            {
                if (item == panel) return true;
            }
            return false;
        }

        private bool IsInStack(Type panelType)
        {
            foreach (UIPanel item in _stack)
            {
                if (panelType.IsInstanceOfType(item)) return true;
            }
            return false;
        }

        private void RemoveFromStack(UIPanel target)
        {
            if (!IsInStack(target)) return;

            var temp = new Stack<UIPanel>();
            while (_stack.Count > 0)
            {
                UIPanel item = _stack.Pop();
                if (item != target)
                    temp.Push(item);
            }

            while (temp.Count > 0)
            {
                _stack.Push(temp.Pop());
            }
        }

        private static bool IsPanelAlive(UIPanel panel)
        {
            return panel != null && panel.gameObject != null;
        }
    }
}

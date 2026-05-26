using UnityEngine;

namespace GameDemo.UI
{
    /// <summary>
    /// UIManager 的 Unity 桥接，须挂载在 Canvas 上。
    /// Awake 时初始化 UIManager，注入 Canvas 和协程宿主。
    /// </summary>
    public class UIRoot : MonoBehaviour
    {
        [SerializeField] [Tooltip("根 Canvas，留空则自动取当前 GameObject 上的 Canvas")] private Canvas _canvas;
        [SerializeField] [Tooltip("场景中预置的面板，Awake 时自动注册")] private UIPanel[] _prePlacedPanels;

        private void Awake()
        {
            if (_canvas == null)
                _canvas = GetComponent<Canvas>();

            UIManager manager = UIManager.Instance;

            if (!manager.IsInitialized)
            {
                manager.Initialize(_canvas, this);
            }
            else
            {
                manager.UpdateUnityContext(_canvas, this);
            }

            if (_prePlacedPanels != null)
            {
                foreach (UIPanel panel in _prePlacedPanels)
                {
                    if (panel != null)
                        manager.Register(panel);
                }
            }
        }

        private void OnDestroy()
        {
            if (UIManager.Instance.IsInitialized)
            {
                UIManager.Instance.OnUIRootDestroyed();
            }
        }
    }
}

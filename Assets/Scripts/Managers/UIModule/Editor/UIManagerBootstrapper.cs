using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GameDemo.UI.Editor
{
    /// <summary>
    /// 编辑器菜单工具：一键创建 Canvas + UIRoot + EventSystem。
    /// </summary>
    public static class UIManagerBootstrapper
    {
        public static void SetupUIRoot()
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasGO = new GameObject("Canvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.AddComponent<CanvasScaler>();
                canvasGO.AddComponent<GraphicRaycaster>();
            }

            if (canvas.GetComponent<UIRoot>() == null)
            {
                canvas.gameObject.AddComponent<UIRoot>();
            }

            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject esGO = new GameObject("EventSystem");
                esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            Selection.activeGameObject = canvas.gameObject;
            Debug.Log("[GameDemo] UI Root setup complete.");
        }
    }
}

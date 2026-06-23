using UnityEngine;

namespace DesignDemo
{
    public sealed class BattleDemoBootstrap : MonoBehaviour
    {
        private static bool bootstrapped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (bootstrapped || FindObjectOfType<BattleDemoBootstrap>() != null)
            {
                return;
            }

            GameObject root = new GameObject("Design Demo Bootstrap");
            root.AddComponent<BattleDemoBootstrap>();
            bootstrapped = true;
        }

        private void Start()
        {
            CombatController controller = gameObject.AddComponent<CombatController>();
            RuntimeBattleUI ui = gameObject.AddComponent<RuntimeBattleUI>();
            ui.Bind(controller);
            controller.StartRun();
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DesignDemo
{
    public sealed class RuntimeBattleUI : MonoBehaviour
    {
        private CombatController controller;
        private Font font;
        private Text playerText;
        private Text enemyText;
        private Text intentText;
        private Text pileText;
        private Text logText;
        private Transform handRoot;
        private Transform rewardRoot;
        private Button endTurnButton;
        private Button preserveCostButton;
        private Button restartButton;

        public void Bind(CombatController combatController)
        {
            controller = combatController;
            controller.Changed += Refresh;
            BuildUi();
            Refresh();
        }

        private void OnDestroy()
        {
            if (controller != null)
            {
                controller.Changed -= Refresh;
            }
        }

        private void BuildUi()
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            EnsureEventSystem();

            GameObject canvasObject = new GameObject("Design Demo Canvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<GraphicRaycaster>();
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);

            GameObject root = CreatePanel("Root", canvasObject.transform, new Color(0.08f, 0.09f, 0.10f, 1f));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = new Vector2(18f, 18f);
            rootRect.offsetMax = new Vector2(-18f, -18f);

            VerticalLayoutGroup rootLayout = root.AddComponent<VerticalLayoutGroup>();
            rootLayout.spacing = 10f;
            rootLayout.padding = new RectOffset(10, 10, 10, 10);
            rootLayout.childControlHeight = true;
            rootLayout.childControlWidth = true;
            rootLayout.childForceExpandHeight = false;

            GameObject top = CreatePanel("Status Row", root.transform, new Color(0.13f, 0.14f, 0.16f, 1f));
            top.AddComponent<LayoutElement>().preferredHeight = 110f;
            HorizontalLayoutGroup topLayout = top.AddComponent<HorizontalLayoutGroup>();
            topLayout.spacing = 10f;
            topLayout.padding = new RectOffset(10, 10, 10, 10);
            topLayout.childControlWidth = true;
            topLayout.childForceExpandWidth = true;

            playerText = CreateText("Player", top.transform, 20, TextAnchor.MiddleLeft);
            enemyText = CreateText("Enemy", top.transform, 20, TextAnchor.MiddleLeft);
            intentText = CreateText("Intent", top.transform, 18, TextAnchor.MiddleLeft);

            GameObject middle = CreatePanel("Middle Row", root.transform, new Color(0.10f, 0.11f, 0.12f, 1f));
            middle.AddComponent<LayoutElement>().preferredHeight = 250f;
            HorizontalLayoutGroup middleLayout = middle.AddComponent<HorizontalLayoutGroup>();
            middleLayout.spacing = 10f;
            middleLayout.padding = new RectOffset(10, 10, 10, 10);

            logText = CreateText("Log", middle.transform, 17, TextAnchor.UpperLeft);
            logText.supportRichText = true;
            logText.GetComponent<LayoutElement>().flexibleWidth = 2f;

            GameObject side = CreatePanel("Side Controls", middle.transform, new Color(0.15f, 0.16f, 0.18f, 1f));
            side.AddComponent<LayoutElement>().preferredWidth = 300f;
            VerticalLayoutGroup sideLayout = side.AddComponent<VerticalLayoutGroup>();
            sideLayout.spacing = 8f;
            sideLayout.padding = new RectOffset(8, 8, 8, 8);
            pileText = CreateText("Piles", side.transform, 17, TextAnchor.UpperLeft);
            pileText.GetComponent<LayoutElement>().preferredHeight = 90f;
            endTurnButton = CreateButton("结束回合", side.transform);
            endTurnButton.onClick.AddListener(delegate { controller.EndPlayerTurn(); });
            preserveCostButton = CreateButton("保留首张手牌耗能(1)", side.transform);
            preserveCostButton.onClick.AddListener(delegate { controller.PreserveFirstHandCardCost(); });
            restartButton = CreateButton("重新开始", side.transform);
            restartButton.onClick.AddListener(delegate { controller.Restart(); });

            GameObject rewardPanel = CreatePanel("Rewards", root.transform, new Color(0.11f, 0.13f, 0.14f, 1f));
            rewardPanel.AddComponent<LayoutElement>().preferredHeight = 92f;
            HorizontalLayoutGroup rewardLayout = rewardPanel.AddComponent<HorizontalLayoutGroup>();
            rewardLayout.spacing = 8f;
            rewardLayout.padding = new RectOffset(8, 8, 8, 8);
            rewardRoot = rewardPanel.transform;

            GameObject handPanel = CreatePanel("Hand", root.transform, new Color(0.12f, 0.10f, 0.09f, 1f));
            handPanel.AddComponent<LayoutElement>().preferredHeight = 175f;
            HorizontalLayoutGroup handLayout = handPanel.AddComponent<HorizontalLayoutGroup>();
            handLayout.spacing = 8f;
            handLayout.padding = new RectOffset(8, 8, 8, 8);
            handLayout.childForceExpandWidth = false;
            handRoot = handPanel.transform;
        }

        private void Refresh()
        {
            if (controller == null || controller.Player == null || controller.Enemy == null)
            {
                return;
            }

            Combatant player = controller.Player;
            Combatant enemy = controller.Enemy;
            playerText.text = "玩家：" + player.Name + "\nHP " + player.Hp + "/" + player.MaxHp + "   格挡 " + player.Block + "   能量 " + player.Energy + "/" + player.MaxEnergy + "\n状态：" + player.DescribeStatuses();
            enemyText.text = "敌人：" + enemy.Name + "\nHP " + enemy.Hp + "/" + enemy.MaxHp + "   格挡 " + enemy.Block + "\n状态：" + enemy.DescribeStatuses();
            intentText.text = controller.AwaitingReward ? "选择战斗奖励" : "敌人意图\n" + (controller.CurrentIntent != null ? controller.CurrentIntent.Label : "无");
            pileText.text = "抽牌堆：" + controller.DrawPile.Count + "\n弃牌堆：" + controller.DiscardPile.Count + "\n手牌：" + controller.Hand.Count + "\n战斗：" + controller.CombatIndex + "/3";
            logText.text = string.Join("\n", controller.LogLines.ToArray());

            endTurnButton.interactable = !controller.AwaitingReward && !controller.CombatEnded;
            preserveCostButton.interactable = player.BakaMode && player.Energy >= 1 && controller.Hand.Count > 0 && !controller.AwaitingReward && !controller.CombatEnded;

            RebuildHand();
            RebuildRewards();
        }

        private void RebuildHand()
        {
            ClearChildren(handRoot);
            foreach (CardInstance card in controller.Hand)
            {
                CardInstance captured = card;
                int cost = CombatRules.GetEffectiveCost(card, controller.Player);
                Button button = CreateButton(BuildCardText(card, cost), handRoot);
                button.GetComponent<LayoutElement>().preferredWidth = 180f;
                button.GetComponent<LayoutElement>().preferredHeight = 150f;
                button.interactable = !controller.AwaitingReward && !controller.CombatEnded && cost <= controller.Player.Energy && (!card.Data.OncePerBattle || !card.UsedThisBattle);
                button.onClick.AddListener(delegate { controller.PlayCard(captured); });
            }
        }

        private void RebuildRewards()
        {
            ClearChildren(rewardRoot);
            if (!controller.AwaitingReward)
            {
                Text hint = CreateText("Reward Hint", rewardRoot, 18, TextAnchor.MiddleLeft);
                hint.text = controller.CombatEnded ? "Demo 已结束。可以重新开始。" : "击败敌人后会在这里出现奖励。";
                return;
            }

            foreach (RewardData reward in controller.CurrentRewards)
            {
                RewardData captured = reward;
                Button button = CreateButton(reward.Name, rewardRoot);
                button.onClick.AddListener(delegate { controller.ChooseReward(captured); });
            }
        }

        private string BuildCardText(CardInstance card, int cost)
        {
            string once = card.Data.OncePerBattle ? "\n每战一次" : "";
            string kept = card.KeepCostNextTurn ? "\n保留耗能" : "";
            return card.Data.Name + "\n耗能：" + cost + "   品质：" + card.Data.Quality + once + kept + "\n" + card.Data.EffectText;
        }

        private GameObject CreatePanel(string panelName, Transform parent, Color color)
        {
            GameObject go = new GameObject(panelName);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.localScale = Vector3.one;
            Image image = go.AddComponent<Image>();
            image.color = color;
            return go;
        }

        private Text CreateText(string textName, Transform parent, int size, TextAnchor anchor)
        {
            GameObject go = new GameObject(textName);
            go.transform.SetParent(parent, false);
            Text text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = new Color(0.93f, 0.92f, 0.88f, 1f);
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            LayoutElement layout = go.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1f;
            layout.flexibleHeight = 1f;
            return text;
        }

        private Button CreateButton(string label, Transform parent)
        {
            GameObject go = CreatePanel("Button", parent, new Color(0.22f, 0.24f, 0.27f, 1f));
            Button button = go.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.22f, 0.24f, 0.27f, 1f);
            colors.highlightedColor = new Color(0.32f, 0.35f, 0.38f, 1f);
            colors.pressedColor = new Color(0.16f, 0.17f, 0.19f, 1f);
            colors.disabledColor = new Color(0.13f, 0.13f, 0.14f, 0.7f);
            button.colors = colors;

            LayoutElement layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = 50f;
            layout.flexibleWidth = 1f;

            Text text = CreateText("Label", go.transform, 16, TextAnchor.MiddleCenter);
            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 5f);
            textRect.offsetMax = new Vector2(-8f, -5f);
            text.text = label;
            return button;
        }

        private void ClearChildren(Transform root)
        {
            List<GameObject> children = new List<GameObject>();
            for (int i = 0; i < root.childCount; i++)
            {
                children.Add(root.GetChild(i).gameObject);
            }

            foreach (GameObject child in children)
            {
                child.transform.SetParent(null, false);
                Destroy(child);
            }
        }

        private void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }
    }
}

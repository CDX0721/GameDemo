using System;
using System.Collections.Generic;
using UnityEngine;

namespace DesignDemo
{
    public sealed class CombatController : MonoBehaviour
    {
        public event Action Changed;

        public Combatant Player { get; private set; }
        public Combatant Enemy { get; private set; }
        public EnemyData CurrentEnemyData { get; private set; }
        public EnemyMoveData CurrentIntent { get; private set; }
        public bool AwaitingReward { get; private set; }
        public bool CombatEnded { get; private set; }
        public int CombatIndex { get; private set; }
        public int EnemyMoveIndex { get; private set; }

        public readonly List<CardInstance> DrawPile = new List<CardInstance>();
        public readonly List<CardInstance> Hand = new List<CardInstance>();
        public readonly List<CardInstance> DiscardPile = new List<CardInstance>();
        public readonly List<string> LogLines = new List<string>();
        public readonly List<RewardData> CurrentRewards = new List<RewardData>();

        private readonly RunModifiers runModifiers = new RunModifiers();
        private readonly List<SkillData> allSkills = new List<SkillData>();
        private readonly List<EnemyData> enemies = new List<EnemyData>();
        private readonly System.Random shuffleRandom = new System.Random();
        private int playerBaseMaxHp = 80;
        private int playerBaseMaxEnergy = 3;

        public void StartRun()
        {
            allSkills.Clear();
            allSkills.AddRange(DesignDatabase.CreateSkills());
            enemies.Clear();
            enemies.AddRange(DesignDatabase.CreateEnemies());

            runModifiers.PlayerMaxHpBonus = 0;
            runModifiers.PlayerMaxEnergyBonus = 0;
            runModifiers.StartStrength = 0;
            runModifiers.KeepBlockAtEndTurn = false;
            runModifiers.FirstTurnBlockBattles = 0;
            runModifiers.FirstTurnBlockAmount = 0;
            runModifiers.EnemyVulnerableBattles = 0;
            runModifiers.EnemyVulnerableAmount = 0;
            runModifiers.EnemyWeakBattles = 0;
            runModifiers.EnemyWeakAmount = 0;

            Player = new Combatant("就一个角色", playerBaseMaxHp, playerBaseMaxEnergy, false);
            CombatIndex = 0;
            LogLines.Clear();
            AddLog("Demo 开始。策划案数据已载入。");
            StartNextCombat();
        }

        public void StartNextCombat()
        {
            AwaitingReward = false;
            CombatEnded = false;
            CombatIndex += 1;
            EnemyMoveIndex = 0;

            int enemyIndex = Mathf.Min(CombatIndex - 1, enemies.Count - 1);
            CurrentEnemyData = enemies[enemyIndex];
            Enemy = new Combatant(CurrentEnemyData.Name, CurrentEnemyData.MaxHp, 0, true);
            CurrentIntent = CurrentEnemyData.Moves[0];

            Player.MaxHp = playerBaseMaxHp + runModifiers.PlayerMaxHpBonus;
            Player.MaxEnergy = playerBaseMaxEnergy + runModifiers.PlayerMaxEnergyBonus;
            Player.Energy = Player.MaxEnergy;
            Player.Block = 0;
            Player.BakaMode = false;
            Player.NextNonAttackFree = false;
            Player.ExtraEnergyNextTurn = 0;
            Player.OverdrawUses = 0;
            Player.Statuses.Clear();

            if (runModifiers.StartStrength > 0)
            {
                Player.AddStatus(StatusIds.Strength, runModifiers.StartStrength);
            }

            if (runModifiers.EnemyVulnerableBattles > 0)
            {
                Enemy.AddStatus(StatusIds.Vulnerable, runModifiers.EnemyVulnerableAmount);
                runModifiers.EnemyVulnerableBattles -= 1;
            }

            if (runModifiers.EnemyWeakBattles > 0)
            {
                Enemy.AddStatus(StatusIds.Weak, runModifiers.EnemyWeakAmount);
                runModifiers.EnemyWeakBattles -= 1;
            }

            BuildDeck();
            Shuffle(DrawPile);
            DiscardPile.Clear();
            Hand.Clear();

            AddLog("战斗 " + CombatIndex + " 开始：" + Enemy.Name + "。");
            StartPlayerTurn(true);
            NotifyChanged();
        }

        public void PlayCard(CardInstance card)
        {
            if (card == null || !Hand.Contains(card) || AwaitingReward || CombatEnded)
            {
                return;
            }

            if (card.Data.OncePerBattle && card.UsedThisBattle)
            {
                AddLog(card.Data.Name + " 一场战斗只能使用一次。");
                NotifyChanged();
                return;
            }

            int cost = CombatRules.GetEffectiveCost(card, Player);
            if (cost > Player.Energy)
            {
                AddLog("能量不足，无法使用 " + card.Data.Name + "。");
                NotifyChanged();
                return;
            }

            int energyBefore = Player.Energy;
            Player.SpendEnergy(cost);
            if (Player.NextNonAttackFree && !card.Data.IsAttack)
            {
                Player.NextNonAttackFree = false;
            }

            card.UsedThisBattle = true;
            AddLog("使用 " + card.Data.Name + "（耗能 " + cost + "）。");
            CombatRules.ApplySkill(card, Player, Enemy, energyBefore, AddLog);

            Hand.Remove(card);
            DiscardPile.Add(card);

            if (Enemy.IsDead)
            {
                WinCombat();
            }
            else if (Player.IsDead)
            {
                LoseRun();
            }

            NotifyChanged();
        }

        public void EndPlayerTurn()
        {
            if (AwaitingReward || CombatEnded)
            {
                return;
            }

            if (!runModifiers.KeepBlockAtEndTurn)
            {
                Player.Block = 0;
            }

            MoveHandToDiscard();
            Player.DecayTurnBasedStatuses();
            EnemyTurn();
            if (!Player.IsDead && !Enemy.IsDead)
            {
                StartPlayerTurn(false);
            }

            NotifyChanged();
        }

        public void PreserveFirstHandCardCost()
        {
            if (!Player.BakaMode || Player.Energy < 1 || Hand.Count == 0 || AwaitingReward || CombatEnded)
            {
                return;
            }

            Player.SpendEnergy(1);
            Hand[0].KeepCostNextTurn = true;
            AddLog("保留 " + Hand[0].Data.Name + " 的当前耗能到下回合。");
            NotifyChanged();
        }

        public void ChooseReward(RewardData reward)
        {
            if (!AwaitingReward || reward == null)
            {
                return;
            }

            ApplyReward(reward);
            AddLog("获得奖励：" + reward.Name + "。");

            if (CombatIndex >= enemies.Count)
            {
                CombatEnded = true;
                AwaitingReward = false;
                AddLog("Demo 通关。可以重新开始测试不同路线。");
            }
            else
            {
                StartNextCombat();
            }

            NotifyChanged();
        }

        public void Restart()
        {
            StartRun();
            NotifyChanged();
        }

        private void StartPlayerTurn(bool firstTurn)
        {
            Player.Energy = Player.MaxEnergy + Player.ExtraEnergyNextTurn;
            Player.ExtraEnergyNextTurn = 0;

            if (firstTurn && runModifiers.FirstTurnBlockBattles > 0)
            {
                Player.GainBlock(runModifiers.FirstTurnBlockAmount);
                runModifiers.FirstTurnBlockBattles -= 1;
                AddLog("战斗奖励触发：第一回合获得 " + runModifiers.FirstTurnBlockAmount + " 格挡。");
            }

            if (Player.BakaMode)
            {
                RandomizeAllCardCosts();
                AddLog("baka 形态：所有技能耗能被打乱。");
            }
            else
            {
                ResetAllCardCosts();
            }

            DrawCards(5);
            CurrentIntent = CurrentEnemyData.Moves[EnemyMoveIndex % CurrentEnemyData.Moves.Count];
            AddLog("你的回合。敌人意图：" + CurrentIntent.Label + "。");
        }

        private void EnemyTurn()
        {
            if (Enemy.GetStatus(StatusIds.Stun) > 0)
            {
                AddLog(Enemy.Name + " 被眩晕，跳过行动。");
                Enemy.DecayTurnBasedStatuses();
                EnemyMoveIndex += 1;
                return;
            }

            EnemyMoveData move = CurrentEnemyData.Moves[EnemyMoveIndex % CurrentEnemyData.Moves.Count];
            if (move.Block > 0)
            {
                Enemy.GainBlock(move.Block);
            }

            if (move.Strength > 0)
            {
                Enemy.AddStatus(StatusIds.Strength, move.Strength);
            }

            if (move.Vulnerable > 0)
            {
                Player.AddStatus(StatusIds.Vulnerable, move.Vulnerable);
            }

            if (move.Damage > 0)
            {
                int total = CombatRules.DealAttack(Enemy, Player, move.Damage, move.Hits, true, AddLog);
                AddLog(Enemy.Name + " 执行 " + move.Label + "，造成 " + total + " 点生命伤害。");
            }
            else
            {
                AddLog(Enemy.Name + " 执行 " + move.Label + "。");
            }

            Enemy.DecayTurnBasedStatuses();
            EnemyMoveIndex += 1;

            if (Player.IsDead)
            {
                LoseRun();
            }
        }

        private void WinCombat()
        {
            AwaitingReward = true;
            CombatEnded = false;
            MoveHandToDiscard();
            CurrentRewards.Clear();
            CurrentRewards.AddRange(DesignDatabase.CreateRewards(CurrentEnemyData.IsElite));
            AddLog("战斗胜利，选择一个奖励。");
        }

        private void LoseRun()
        {
            CombatEnded = true;
            AwaitingReward = false;
            AddLog("角色倒下了。点击重新开始再试一次。");
        }

        private void ApplyReward(RewardData reward)
        {
            switch (reward.Kind)
            {
                case RewardKind.HealPercent:
                    Player.HealPercent(reward.Amount);
                    break;
                case RewardKind.FirstTurnBlock:
                    runModifiers.FirstTurnBlockAmount = reward.Amount;
                    runModifiers.FirstTurnBlockBattles += reward.BattleCount;
                    break;
                case RewardKind.EnemyVulnerable:
                    runModifiers.EnemyVulnerableAmount = reward.Amount;
                    runModifiers.EnemyVulnerableBattles += reward.BattleCount;
                    break;
                case RewardKind.EnemyWeak:
                    runModifiers.EnemyWeakAmount = reward.Amount;
                    runModifiers.EnemyWeakBattles += reward.BattleCount;
                    break;
                case RewardKind.MaxHp:
                    runModifiers.PlayerMaxHpBonus += reward.Amount;
                    Player.MaxHp += reward.Amount;
                    Player.Hp += reward.Amount;
                    break;
                case RewardKind.MaxEnergy:
                    runModifiers.PlayerMaxEnergyBonus += reward.Amount;
                    Player.MaxEnergy += reward.Amount;
                    break;
                case RewardKind.StartStrength:
                    runModifiers.StartStrength += reward.Amount;
                    break;
                case RewardKind.KeepBlock:
                    runModifiers.KeepBlockAtEndTurn = true;
                    break;
            }
        }

        private void BuildDeck()
        {
            DrawPile.Clear();
            foreach (SkillData skill in allSkills)
            {
                DrawPile.Add(new CardInstance(skill));
            }
        }

        private void DrawCards(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (DrawPile.Count == 0)
                {
                    if (DiscardPile.Count == 0)
                    {
                        return;
                    }

                    DrawPile.AddRange(DiscardPile);
                    DiscardPile.Clear();
                    Shuffle(DrawPile);
                }

                CardInstance card = DrawPile[0];
                DrawPile.RemoveAt(0);
                Hand.Add(card);
            }
        }

        private void MoveHandToDiscard()
        {
            DiscardPile.AddRange(Hand);
            Hand.Clear();
        }

        private void Shuffle(List<CardInstance> cards)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = shuffleRandom.Next(i + 1);
                CardInstance temp = cards[i];
                cards[i] = cards[j];
                cards[j] = temp;
            }
        }

        private void RandomizeAllCardCosts()
        {
            RandomizeCardCosts(DrawPile);
            RandomizeCardCosts(Hand);
            RandomizeCardCosts(DiscardPile);
        }

        private void RandomizeCardCosts(List<CardInstance> cards)
        {
            foreach (CardInstance card in cards)
            {
                if (card.KeepCostNextTurn)
                {
                    card.KeepCostNextTurn = false;
                }
                else
                {
                    card.CurrentCost = UnityEngine.Random.Range(0, 4);
                }
            }
        }

        private void ResetAllCardCosts()
        {
            ResetCardCosts(DrawPile);
            ResetCardCosts(Hand);
            ResetCardCosts(DiscardPile);
        }

        private void ResetCardCosts(List<CardInstance> cards)
        {
            foreach (CardInstance card in cards)
            {
                CombatRules.ResetCardCost(card);
            }
        }

        private void AddLog(string line)
        {
            LogLines.Add(line);
            while (LogLines.Count > 12)
            {
                LogLines.RemoveAt(0);
            }
        }

        private void NotifyChanged()
        {
            if (Changed != null)
            {
                Changed();
            }
        }
    }
}

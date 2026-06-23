using System;
using System.Collections.Generic;
using UnityEngine;

namespace DesignDemo
{
    public static class StatusIds
    {
        public const string Weak = "虚弱";
        public const string Vulnerable = "易伤";
        public const string Freeze = "冰冻";
        public const string Thorns = "荆棘";
        public const string Stun = "眩晕";
        public const string Agility = "敏捷";
        public const string Strength = "力量";
    }

    [Serializable]
    public sealed class CardInstance
    {
        public SkillData Data;
        public int CurrentCost;
        public bool UsedThisBattle;
        public bool KeepCostNextTurn;

        public CardInstance(SkillData data)
        {
            Data = data;
            CurrentCost = data.BaseCost;
        }
    }

    [Serializable]
    public sealed class Combatant
    {
        public string Name;
        public int MaxHp;
        public int Hp;
        public int MaxEnergy;
        public int Energy;
        public int Block;
        public bool IsEnemy;
        public bool BakaMode;
        public bool NextNonAttackFree;
        public int ExtraEnergyNextTurn;
        public int OverdrawUses;
        public Dictionary<string, int> Statuses = new Dictionary<string, int>();

        public bool IsDead
        {
            get { return Hp <= 0; }
        }

        public int Strength
        {
            get { return GetStatus(StatusIds.Strength); }
        }

        public Combatant(string name, int maxHp, int maxEnergy, bool isEnemy)
        {
            Name = name;
            MaxHp = maxHp;
            Hp = maxHp;
            MaxEnergy = maxEnergy;
            Energy = maxEnergy;
            IsEnemy = isEnemy;
        }

        public int GetStatus(string id)
        {
            int value;
            return Statuses.TryGetValue(id, out value) ? value : 0;
        }

        public void AddStatus(string id, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Statuses[id] = GetStatus(id) + amount;
        }

        public void SetStatus(string id, int amount)
        {
            if (amount <= 0)
            {
                Statuses.Remove(id);
            }
            else
            {
                Statuses[id] = amount;
            }
        }

        public void HealPercent(int percent)
        {
            int amount = Mathf.CeilToInt(MaxHp * percent / 100f);
            Hp = Mathf.Min(MaxHp, Hp + amount);
        }

        public void LoseLife(int amount)
        {
            Hp = Mathf.Max(0, Hp - Mathf.Max(0, amount));
        }

        public void GainBlock(int amount)
        {
            Block += Mathf.Max(0, amount);
        }

        public void SpendEnergy(int amount)
        {
            Energy = Mathf.Max(0, Energy - Mathf.Max(0, amount));
        }

        public void DecayTurnBasedStatuses()
        {
            Decay(StatusIds.Weak);
            Decay(StatusIds.Vulnerable);
            Decay(StatusIds.Stun);
        }

        public string DescribeStatuses()
        {
            if (Statuses.Count == 0)
            {
                return "无";
            }

            List<string> parts = new List<string>();
            foreach (KeyValuePair<string, int> kv in Statuses)
            {
                if (kv.Value > 0)
                {
                    parts.Add(kv.Key + " " + kv.Value);
                }
            }

            return parts.Count == 0 ? "无" : string.Join(" / ", parts.ToArray());
        }

        private void Decay(string id)
        {
            int value = GetStatus(id);
            if (value > 0)
            {
                SetStatus(id, value - 1);
            }
        }
    }

    [Serializable]
    public sealed class RunModifiers
    {
        public int PlayerMaxHpBonus;
        public int PlayerMaxEnergyBonus;
        public int StartStrength;
        public bool KeepBlockAtEndTurn;
        public int FirstTurnBlockBattles;
        public int FirstTurnBlockAmount;
        public int EnemyVulnerableBattles;
        public int EnemyVulnerableAmount;
        public int EnemyWeakBattles;
        public int EnemyWeakAmount;
    }
}

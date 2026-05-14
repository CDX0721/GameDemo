using System;
using UnityEngine;

namespace DesignDemo
{
    public static class CombatRules
    {
        public static int GetEffectiveCost(CardInstance card, Combatant player)
        {
            if (player.NextNonAttackFree && !card.Data.IsAttack)
            {
                return 0;
            }

            if (card.Data.VariableCost && !player.BakaMode)
            {
                return player.Energy;
            }

            return Mathf.Clamp(card.CurrentCost, 0, 3);
        }

        public static void ResetCardCost(CardInstance card)
        {
            if (card.KeepCostNextTurn)
            {
                card.KeepCostNextTurn = false;
                return;
            }

            card.CurrentCost = card.Data.BaseCost;
        }

        public static int DealAttack(Combatant attacker, Combatant defender, int baseDamage, int hits, bool activeAttack, Action<string> log)
        {
            if (hits <= 0 || baseDamage <= 0)
            {
                return 0;
            }

            int total = 0;
            int safeHits = hits;
            for (int i = 0; i < safeHits; i++)
            {
                int damage = Mathf.Max(0, baseDamage + attacker.Strength);

                if (attacker.GetStatus(StatusIds.Weak) > 0)
                {
                    damage = Mathf.FloorToInt(damage * 0.75f);
                }

                if (activeAttack && defender.GetStatus(StatusIds.Vulnerable) > 0)
                {
                    damage = Mathf.CeilToInt(damage * 1.5f);
                }

                int blocked = Mathf.Min(defender.Block, damage);
                defender.Block -= blocked;
                int hpDamage = damage - blocked;
                defender.LoseLife(hpDamage);
                total += hpDamage;

                if (hpDamage > 0 && defender.GetStatus(StatusIds.Thorns) > 0)
                {
                    int thorns = defender.GetStatus(StatusIds.Thorns);
                    attacker.LoseLife(thorns);
                    if (log != null)
                    {
                        log(defender.Name + " 的荆棘反弹 " + thorns + " 伤害。");
                    }
                }
            }

            return total;
        }

        public static void ApplyFreeze(Combatant target, int amount, Action<string> log)
        {
            target.AddStatus(StatusIds.Freeze, amount);
            int stacks = target.GetStatus(StatusIds.Freeze);

            if (stacks >= 8)
            {
                target.SetStatus(StatusIds.Freeze, 0);
                target.AddStatus(StatusIds.Stun, 1);
                target.LoseLife(20);
                if (log != null)
                {
                    log(target.Name + " 冰冻达到 8 层，眩晕 1 回合并受到 20 伤害。");
                }
                return;
            }

            if (stacks >= 5)
            {
                target.SetStatus(StatusIds.Freeze, stacks % 5);
                target.LoseLife(10);
                if (log != null)
                {
                    log(target.Name + " 冰冻爆发，受到 10 伤害。");
                }
            }
        }

        public static void ApplySkill(CardInstance card, Combatant player, Combatant enemy, int energyBeforeSpend, Action<string> log)
        {
            switch (card.Data.Id)
            {
                case 101:
                    DealAttack(player, enemy, 6, 1, true, log);
                    AddBakaFreezeIfNeeded(player, enemy, log);
                    break;
                case 102:
                    player.GainBlock(5);
                    break;
                case 103:
                    player.AddStatus(StatusIds.Agility, 1);
                    player.AddStatus(StatusIds.Thorns, 4);
                    break;
                case 104:
                    DealAttack(player, enemy, 8, 1, true, log);
                    enemy.AddStatus(StatusIds.Vulnerable, 2);
                    AddBakaFreezeIfNeeded(player, enemy, log);
                    break;
                case 105:
                    DealAttack(player, enemy, 7, 1, true, log);
                    enemy.AddStatus(StatusIds.Weak, 1);
                    enemy.AddStatus(StatusIds.Vulnerable, 1);
                    AddBakaFreezeIfNeeded(player, enemy, log);
                    break;
                case 106:
                    player.GainBlock(15);
                    player.LoseLife(2);
                    break;
                case 107:
                    DealAttack(player, enemy, 32, 1, true, log);
                    AddBakaFreezeIfNeeded(player, enemy, log);
                    break;
                case 108:
                    player.GainBlock(18);
                    player.ExtraEnergyNextTurn += 2;
                    break;
                case 109:
                    DealAttack(player, enemy, 12, 1, true, log);
                    player.NextNonAttackFree = true;
                    AddBakaFreezeIfNeeded(player, enemy, log);
                    break;
                case 110:
                    player.BakaMode = true;
                    break;
                case 111:
                    DealAttack(player, enemy, player.Block, 1, true, log);
                    AddBakaFreezeIfNeeded(player, enemy, log);
                    break;
                case 112:
                    DealAttack(player, enemy, 5, Mathf.Max(0, energyBeforeSpend), true, log);
                    AddBakaFreezeIfNeeded(player, enemy, log);
                    break;
                case 113:
                    player.OverdrawUses += 1;
                    player.Energy += 2;
                    player.LoseLife(3 + (player.OverdrawUses - 1) * 2);
                    break;
            }
        }

        private static void AddBakaFreezeIfNeeded(Combatant player, Combatant enemy, Action<string> log)
        {
            if (player.BakaMode)
            {
                ApplyFreeze(enemy, 1, log);
            }
        }
    }
}

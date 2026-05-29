#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GameDemo.Battle;
using NUnit.Framework;
using UnityEngine;

namespace GameDemo.Tests.EditMode
{
    public class BattleFlowTest
    {
        private BattleManager _mgr = null!;
        private readonly StringBuilder _log = new();
        private int _round;

        private void Log(string msg)
        {
            _log.AppendLine($"[{_round:D3}] {msg}");
            Debug.Log($"[{_round:D3}] {msg}");
        }

        [Test]
        public void RunFullBattle()
        {
            SetupBattle();

            _round = 0;
            _mgr.StartBattle();

            const int maxFrames = 500;
            while (_round < maxFrames && !_mgr.StateMachine.IsBattleEnd)
            {
                _round++;
                var st = _mgr.StateMachine.CurrentState;
                if (st == BattleState.PreAction)
                    _mgr.EnterPreAction();
                else if (st == BattleState.WaitingAction)
                    _mgr.EnterWaitingAction();
                else if (st == BattleState.PostAction)
                    _mgr.EnterPostAction();
            }

            if (_round >= maxFrames)
                Log("【警告】达到最大帧数上限");

            int aliveP = _mgr.AliveCountPlayer;
            int aliveE = _mgr.AliveCountEnemy;
            Log($"========== 战斗结束 ==========");
            Log($"我方存活: {aliveP}  敌方存活: {aliveE}");
            Log($"结果: {(aliveP > 0 && aliveE == 0 ? "玩家胜利" : aliveE > 0 && aliveP == 0 ? "玩家败北" : "未分胜负")}");

            string logPath = Path.Combine(Application.dataPath, "Tests", "EditMode", "Battle", "BattleFlowTest.log");
            File.WriteAllText(logPath, _log.ToString());
            Debug.Log($"日志已保存到: {logPath}");

            Assert.Less(_round, maxFrames, "战斗未在最大帧数内结束");

            // 验证关键逻辑是否在日志中出现
            string logStr = _log.ToString();
            Assert.IsTrue(logStr.Contains("攻击") || logStr.Contains("普通攻击"), "日志应包含攻击行为");
            Assert.IsTrue(logStr.Contains("强化") && logStr.Contains("法师"), "战士应给法师加攻击强化");
            Assert.IsTrue(logStr.Contains("荆棘") && logStr.Contains("x4"), "荆棘应正确叠加至 x4");
            Assert.IsTrue(logStr.Contains("沙暴"), "巨魔应使用过沙暴");
            Assert.IsTrue(logStr.Contains("哈米吉多顿"), "战士仅剩自己时应使用哈米吉多顿");
            Assert.IsTrue(logStr.Contains("法师") && logStr.Contains("战士") && logStr.Contains("牧师"), "至少应出现三个我方单位");
            Assert.IsTrue(logStr.Contains("玩家胜利") || logStr.Contains("玩家败北"), "战斗应有明确结果");
        }

        // ==================== 布阵 ====================

        private void SetupBattle()
        {
            Log("========== 初始化战斗 ==========");

            _mgr = new BattleManager();
            _mgr.OnWaitingForAutoAction += AI_OnTurn;

            // --- 我方 ---
            var ptWarrior = new BattleUnit("p_warrior", "战士",   attack: 80, defense: 30, hp: 800, speed: 60, mana: 100);
            var ptMage    = new BattleUnit("p_mage",    "法师",   attack: 120, defense: 15, hp: 300, speed: 40, mana: 200);
            var ptPriest  = new BattleUnit("p_priest",  "牧师",   attack: 50, defense: 20, hp: 350, speed: 50, mana: 150);

            PlaceUnit(ptWarrior, 1, 0, true,
                SkillCatalog.Get("NormalAttack", 2),
                SkillCatalog.Get("Shield", 2),
                SkillCatalog.Get("AtkStrongUp", 1),
                SkillCatalog.Get("Armageddon", 1));

            PlaceUnit(ptMage, 0, 1, true,
                SkillCatalog.Get("NormalAttack", 2),
                SkillCatalog.Get("ManaDrain", 2),
                SkillCatalog.Get("ThornsWrap", 2));

            PlaceUnit(ptPriest, 2, 1, true,
                SkillCatalog.Get("NormalAttack", 1),
                SkillCatalog.Get("Heal", 3),
                SkillCatalog.Get("FlashStrike", 1));

            Log($"我方: 战士(HP:800 ATK:80) 法师(HP:300 ATK:120) 牧师(HP:350 ATK:50)");

            // --- 敌方 ---
            var etGoblin = new BattleUnit("e_goblin", "哥布林",     attack: 60, defense: 10, hp: 300, speed: 70, mana: 50);
            var etArcher = new BattleUnit("e_archer", "哥布林射手", attack: 70, defense: 8,  hp: 250, speed: 80, mana: 50);
            var etTroll  = new BattleUnit("e_troll",  "巨魔",       attack: 100, defense: 40, hp: 650, speed: 30, mana: 80);

            PlaceUnit(etGoblin, 1, 1, false,
                SkillCatalog.Get("NormalAttack", 2),
                SkillCatalog.Get("Shield", 1));

            PlaceUnit(etArcher, 0, 2, false,
                SkillCatalog.Get("NormalAttack", 2),
                SkillCatalog.Get("ThornsWrap", 2));

            PlaceUnit(etTroll, 1, 2, false,
                SkillCatalog.Get("NormalAttack", 3),
                SkillCatalog.Get("SandStorm", 1));

            Log($"敌方: 哥布林(HP:300 ATK:60) 射手(HP:250 ATK:70) 巨魔(HP:650 ATK:100)");
            Log("========== 战斗开始 ==========\n");
        }

        private void PlaceUnit(BattleUnit template, int row, int col, bool isPlayer, params Skill[] skills)
        {
            foreach (var s in skills)
                template.InnateSkills.Add(s);
            var unit = new AutoUnitInstance(template, initialCost: 100f);
            var formation = isPlayer ? _mgr.PlayerFormation : _mgr.EnemyFormation;
            formation.PlaceUnit(unit, new BattleSlot(row, col));
        }

        // ==================== AI 逻辑 ====================

        private void AI_OnTurn(AutoUnitInstance caster)
        {
            var skills = _mgr.GetCastableSkills(caster);
            if (skills.Count == 0)
            {
                Log($"[AI] {caster.DisplayName} 无可用技能，跳过");
                _mgr.StateMachine.SetState(BattleState.PostAction);
                return;
            }

            bool isPlayer = _mgr.IsPlayerUnit(caster);
            Formation enemyForm = isPlayer ? _mgr.EnemyFormation : _mgr.PlayerFormation;
            Formation allyForm  = isPlayer ? _mgr.PlayerFormation : _mgr.EnemyFormation;

            float hpPct = caster.CurrentHP / caster.MaxHP;

            // 0. 哈米吉多顿：只剩自己时秒杀全场
            var armageddon = FindSkill(skills, "Armageddon");
            if (armageddon.HasValue)
            {
                Log($"[AI] {caster.DisplayName} 仅剩自己，发动 [{armageddon.Value.skill.DisplayName}]！");
                SubmitAndAct(caster, armageddon.Value.skill, armageddon.Value.target);
                return;
            }

            // 1. 自救：自身 HP < 35%
            if (hpPct < 0.35f)
            {
                // 找最低血量队友（包含自己）治疗
                var lowestAlly = FindLowestHpUnit(allyForm);
                var heal = FindSkill(skills, "Heal");
                if (heal.HasValue)
                {
                    var target = lowestAlly ?? caster;
                    Log($"[AI] {caster.DisplayName} HP={caster.CurrentHP:F0} ({(hpPct*100):F0}%)，低血量，使用 [{heal.Value.skill.DisplayName}] 治疗 {target.DisplayName}");
                    SubmitAndAct(caster, heal.Value.skill, target);
                    return;
                }
                var shield = FindSkill(skills, "Shield");
                if (shield.HasValue)
                {
                    Log($"[AI] {caster.DisplayName} HP={caster.CurrentHP:F0} ({(hpPct*100):F0}%)，低血量，使用 [{shield.Value.skill.DisplayName}] 自救");
                    SubmitAndAct(caster, shield.Value.skill, shield.Value.target);
                    return;
                }
            }

            // 2. 救援队友：任何队友 HP < 30%
            var criticalAlly = FindCriticalAlly(allyForm, 0.30f);
            if (criticalAlly != null)
            {
                var heal = FindSkill(skills, "Heal");
                if (heal.HasValue)
                {
                    Log($"[AI] {caster.DisplayName} 发现 {criticalAlly.DisplayName} HP={criticalAlly.CurrentHP:F0} 危急，使用 [{heal.Value.skill.DisplayName}] 救援");
                    SubmitAndAct(caster, heal.Value.skill, criticalAlly);
                    return;
                }
            }

            // 3. 强化攻击最高的未持有 Buff 的队友
            var atkBuff = FindSkill(skills, "AtkStrongUp");
            if (atkBuff.HasValue)
            {
                var bestAlly = FindBestAtkBuffTarget(allyForm, caster);
                if (bestAlly != null)
                {
                    Log($"[AI] {caster.DisplayName} 使用 [{atkBuff.Value.skill.DisplayName}] 强化 {bestAlly.DisplayName}");
                    SubmitAndAct(caster, atkBuff.Value.skill, bestAlly);
                    return;
                }
            }

            var lastStand = FindSkill(skills, "TheLastStand");
            if (lastStand.HasValue && !HasEffect(caster, "DmgBonusUp"))
            {
                Log($"[AI] {caster.DisplayName} 使用 [{lastStand.Value.skill.DisplayName}] 强化全体");
                SubmitAndAct(caster, lastStand.Value.skill, lastStand.Value.target);
                return;
            }

            // 4. 群体攻击（敌方 >= 2 时优先 AOE）
            var sandStorm = FindSkill(skills, "SandStorm");
            if (sandStorm.HasValue && CountAlive(enemyForm) >= 2)
            {
                Log($"[AI] {caster.DisplayName} 使用 [{sandStorm.Value.skill.DisplayName}] 攻击全体");
                SubmitAndAct(caster, sandStorm.Value.skill, sandStorm.Value.target);
                return;
            }

            // 5. 荆棘缠绕
            var thorns = FindSkill(skills, "ThornsWrap");
            if (thorns.HasValue)
            {
                Log($"[AI] {caster.DisplayName} 使用 [{thorns.Value.skill.DisplayName}]");
                SubmitAndAct(caster, thorns.Value.skill, thorns.Value.target);
                return;
            }

            // 6. 吸魔
            var manaDrain = FindSkill(skills, "ManaDrain");
            if (manaDrain.HasValue)
            {
                Log($"[AI] {caster.DisplayName} 使用 [{manaDrain.Value.skill.DisplayName}]");
                SubmitAndAct(caster, manaDrain.Value.skill, manaDrain.Value.target);
                return;
            }

            // 7. 攻击最低血量敌人
            var normalAtk = FindLowestHpTarget(skills, "NormalAttack", enemyForm);
            if (normalAtk.HasValue)
            {
                var (ns, nt) = normalAtk.Value;
                Log($"[AI] {caster.DisplayName} 使用 [{ns.DisplayName}] → {nt.DisplayName}(HP:{nt.CurrentHP:F0})");
                SubmitAndAct(caster, ns, nt);
                return;
            }

            // 8. 默认
            var first = skills[0];
            Log($"[AI] {caster.DisplayName} 使用 [{first.skill.DisplayName}]（默认）");
            SubmitAndAct(caster, first.skill, first.target);
        }

        private void SubmitAndAct(BattleUnitInstance caster, Skill skill, BattleUnitInstance target)
        {
            // 记录前HP
            var sb = new StringBuilder();
            foreach (var u in GetAllAlive(_mgr.PlayerFormation, _mgr.EnemyFormation))
                sb.Append($"{u.DisplayName}(HP:{u.CurrentHP:F0} MP:{u.CurrentMana:F0}) ");
            Log($"  [状态] {sb}");

            if (!skill.CanCast(caster, target))
            {
                Log($"  [失败] 不满足释放条件，跳过");
                _mgr.StateMachine.SetState(BattleState.PostAction);
                return;
            }

            _mgr.SelectedSkill = skill;
            skill.Cast(caster);

            // 记录施放
            Log($"  [施放] {caster.DisplayName} → [{skill.DisplayName}] → [{target.DisplayName}]");

            skill.Apply(caster, target);

            // 记录后HP
            sb.Clear();
            foreach (var u in GetAllAlive(_mgr.PlayerFormation, _mgr.EnemyFormation))
            {
                sb.Append($"{u.DisplayName}(HP:{u.CurrentHP:F0} MP:{u.CurrentMana:F0}) ");
                if (u.Effects.Count > 0)
                {
                    sb.Append("[");
                    foreach (var e in u.Effects)
                        sb.Append($"{e.Template.DisplayName}x{e.CurrentStackCount}({e.RemainingTurns}t) ");
                    sb.Append("]");
                }
                sb.Append("| ");
            }
            Log($"  [结果] {sb}");

            _mgr.RaiseSkillUsed(caster, skill, new List<BattleUnitInstance> { target });
            _mgr.RefreshAllUnits();
            _mgr.CleanupDeadUnits();

            _mgr.StateMachine.SetState(BattleState.PostAction);
        }

        // ==================== 辅助 ====================

        private static BattleUnitInstance? FindBestAtkBuffTarget(Formation allyForm, BattleUnitInstance caster)
        {
            BattleUnitInstance? best = null;
            float maxAtk = 0f;
            foreach (var u in allyForm.Units)
            {
                if (!u.IsAlive) continue;
                if (u == caster) continue;
                if (HasEffect(u, "AtkMultUp")) continue;
                if (u.CurrentAttack > maxAtk)
                { maxAtk = u.CurrentAttack; best = u; }
            }
            return best;
        }

        private static bool HasEffect(BattleUnitInstance unit, string effectId)
        {
            foreach (var e in unit.Effects)
                if (e.Template.Id == effectId)
                    return true;
            return false;
        }

        private static BattleUnitInstance? FindCriticalAlly(Formation allyForm, float threshold)
        {
            foreach (var u in allyForm.Units)
                if (u.IsAlive && u.CurrentHP / u.MaxHP < threshold)
                    return u;
            return null;
        }

        private static BattleUnitInstance? FindLowestHpUnit(Formation formation)
        {
            BattleUnitInstance? best = null;
            float minHp = float.MaxValue;
            foreach (var u in formation.Units)
                if (u.IsAlive && u.CurrentHP < minHp)
                { minHp = u.CurrentHP; best = u; }
            return best;
        }

        private static (Skill skill, BattleUnitInstance target)? FindSkill(
            List<(Skill skill, BattleUnitInstance target)> skills, string id)
        {
            foreach (var (s, t) in skills)
                if (s.Id == id)
                    return (s, t);
            return null;
        }

        private static (Skill skill, BattleUnitInstance target)? FindLowestHpTarget(
            List<(Skill skill, BattleUnitInstance target)> skills, string id, Formation enemyForm)
        {
            BattleUnitInstance? lowest = null;
            float minHp = float.MaxValue;
            foreach (var u in enemyForm.Units)
                if (u.IsAlive && u.CurrentHP < minHp)
                { minHp = u.CurrentHP; lowest = u; }

            if (lowest == null) return null;

            foreach (var (s, t) in skills)
                if (s.Id == id)
                    return (s, lowest);
            return null;
        }

        private static int CountAlive(Formation f)
        {
            int c = 0;
            foreach (var u in f.Units) if (u.IsAlive) c++;
            return c;
        }

        private static List<BattleUnitInstance> FindAllAlive(Formation f)
        {
            var r = new List<BattleUnitInstance>();
            foreach (var u in f.Units) if (u.IsAlive) r.Add(u);
            return r;
        }

        private static List<BattleUnitInstance> GetAllAlive(Formation a, Formation b)
        {
            var r = FindAllAlive(a);
            r.AddRange(FindAllAlive(b));
            return r;
        }

    }
}
#endif

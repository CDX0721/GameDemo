# 战斗技能列表 (SkillCatalog)

共 18 个技能。数据来源：`Assets/Scripts/Managers/BattleModule/Core/SkillCatalog.cs`

---

## 技能概览

| # | 技能ID | 名称 | 类型 | 目标 | 等级 | 消耗 |
|---|---|---|---|---|---|---|
| 1 | NormalAttack | 普通攻击 | SingleAttack | SingleEnemy | L1-L3 | 无 |
| 2 | Shield | 护盾 | Defense | SingleAlly | L1-L3 | 3-5 Mana |
| 3 | EquivalentExchange | 等价交换 | Support | SingleSelf | L1 | 无 |
| 4 | PenetrateArrow | 贯穿之箭 | Spread | SingleEnemy | L1 | 25 Mana |
| 5 | Heal | 治疗 | Healing | SingleAlly | L1-L3 | 3-5 Mana |
| 6 | SandStorm | 沙暴 | AoE | AllEnemies | L1 | 25 Mana |
| 7 | FlashStrike | 闪击 | Support | SingleAlly | L1 | 10 Mana |
| 8 | ManaDrain | 吸魔 | SingleAttack | SingleEnemy | L1-L3 | 无 |
| 9 | AtkStrongUp | 你被强化了！快上！ | Support | SingleAlly | L1 | 10 Mana |
| 10 | ThornsWrap | 荆棘缠绕 | SingleAttack | SingleEnemy | L1-L3 | 5-7 Mana |
| 11 | DefenseBreak | 这就破防了？ | Support | SingleEnemy | L1-L3 | 12-20 Mana |
| 12 | Terminate | 终结 | SingleAttack | SingleEnemy | L1-L3 | 无 |
| 13 | DarkCurse | 黑暗诅咒 | SingleAttack | SingleEnemy | L1-L3 | 7-11 Mana |
| 14 | Swamp | 沼泽 | Support | AllEnemies | L1-L3 | 15-25 Mana |
| 15 | LightningChain | 闪电链 | Spread | SingleEnemy | L1-L4 | 5-8 Mana |
| 16 | DiamondDust | 钻石星辰 | Spread | SingleEnemy | L1 | 15 Mana |
| 17 | Melt | 熔化 | SingleAttack | SingleEnemy | L1 | 15 Mana |
| 18 | TheLastStand | 背水一战 | Support | AllAllies | L1 | 20 Mana |
| 19 | Armageddon | 哈米吉多顿 | AoE | AllEnemies | L1 | 见描述 |

---

## 详细描述

### 1. NormalAttack — 普通攻击

| 属性 | 值 |
|---|---|
| **技能类型** | SingleAttack（单体攻击） |
| **目标类型** | SingleEnemy（敌方单体） |
| **等级范围** | L1 / L2 / L3 |
| **法力消耗** | 无 |

**效果：** 对敌方单体造成伤害。

| 等级 | 伤害倍率 |
|---|---|
| L1 | 100% 攻击力 |
| L2 | 150% 攻击力 |
| L3 | 200% 攻击力 |

**AI 优先度：** 优先攻击低血量目标。`50 + (1 - HP比例) × 30`

---

### 2. Shield — 护盾

| 属性 | 值 |
|---|---|
| **技能类型** | Defense（防御） |
| **目标类型** | SingleAlly（友方单体） |
| **等级范围** | L1 / L2 / L3 |

**效果：** 消耗法力，为目标附加护盾值。

| 等级 | 法力消耗 | 护盾比例（最大HP） |
|---|---|---|
| L1 | 3 | 10% |
| L2 | 4 | 15% |
| L3 | 5 | 20% |

**AI 优先度：** 优先低护盾、低血量目标。`shieldNeed × 50 + hpNeed × 30`

---

### 3. EquivalentExchange — 等价交换

| 属性 | 值 |
|---|---|
| **技能类型** | Support（辅助） |
| **目标类型** | SingleSelf（自身） |
| **等级范围** | L1 |
| **法力消耗** | 无 |

**效果：** 交换自身当前 HP 比例和 MP 比例，然后扣除 10% 最大法力值。

**AI 优先度：** MP >> HP 时使用。`min((MP比例 - HP比例) × 85, 80)`，差值为负时降至 5。

---

### 4. PenetrateArrow — 贯穿之箭

| 属性 | 值 |
|---|---|
| **技能类型** | Spread（扩散） |
| **目标类型** | SingleEnemy（敌方单体） |
| **等级范围** | L1 |
| **法力消耗** | 25 |

**效果：** 对目标同列所有敌方造成 **250% ATK + 10% MaxHP** 的伤害，按排号升序依次播放动画（间隔 0.2s）。

**AI 优先度：** 同列敌人越多越优先。

---

### 5. Heal — 治疗

| 属性 | 值 |
|---|---|
| **技能类型** | Healing（治疗） |
| **目标类型** | SingleAlly（友方单体） |
| **等级范围** | L1 / L2 / L3 |

**效果：** 消耗法力，恢复目标一定比例最大 HP。

| 等级 | 法力消耗 | 回复比例（最大HP） |
|---|---|---|
| L1 | 3 | 15% |
| L2 | 4 | 20% |
| L3 | 5 | 25% |

**AI 优先度：** HP ≥ 90% 时为 5；否则 `(1 - HP比例) × 85`。

---

### 6. SandStorm — 沙暴

| 属性 | 值 |
|---|---|
| **技能类型** | AoE（群体攻击） |
| **目标类型** | AllEnemies（敌方全体） |
| **等级范围** | L1 |
| **法力消耗** | 25 |

**效果：** 对敌方全体造成 **200% ATK** 的伤害。

**AI 优先度：** 敌方越多越优先。`min(敌方数 × 22, 85)`

---

### 7. FlashStrike — 闪击

| 属性 | 值 |
|---|---|
| **技能类型** | Support（辅助） |
| **目标类型** | SingleAlly（友方单体） |
| **等级范围** | L1 |
| **法力消耗** | 10 |

**效果：** 驱散目标所有负面效果，`RemainingCost` 重置为 0（立即行动）。

**AI 优先度：** 负面效果越多越优先。`min(负面数 × 25, 80)`

---

### 8. ManaDrain — 吸魔

| 属性 | 值 |
|---|---|
| **技能类型** | SingleAttack（单体攻击） |
| **目标类型** | SingleEnemy（敌方单体） |
| **等级范围** | L1 / L2 / L3 |
| **法力消耗** | 无 |

**效果：** 吸取目标百分比最大法力，回复给施法者。

| 等级 | 吸取比例（最大Mana） |
|---|---|
| L1 | 10% |
| L2 | 15% |
| L3 | 20% |

**AI 优先度：** 自身 MP ≥ 80% 时为 15；否则 `15 + (1 - MP比例) × 70`。

---

### 9. AtkStrongUp — 你被强化了！快上！

| 属性 | 值 |
|---|---|
| **技能类型** | Support（辅助） |
| **目标类型** | SingleAlly（友方单体） |
| **等级范围** | L1 |
| **法力消耗** | 10 |

**效果：** 施加 `AtkMultUp`：攻击力 ×200%，持续 1 回合。

**AI 优先度：** 固定 65。

---

### 10. ThornsWrap — 荆棘缠绕

| 属性 | 值 |
|---|---|
| **技能类型** | SingleAttack（单体攻击） |
| **目标类型** | SingleEnemy（敌方单体） |
| **等级范围** | L1 / L2 / L3 |

**效果：** 施加 2 层 `Thorns` 效果（每回合受到 %ATK 持续伤害），持续 2 回合。最多叠加 5 层，可驱散。

| 等级 | 法力消耗 | 每层伤害比例 |
|---|---|---|
| L1 | 5 | 50% ATK |
| L2 | 6 | 70% ATK |
| L3 | 7 | 90% ATK |

**AI 优先度：** 高血量敌人优先。HP < 30% 时仅 10；`20 + HP比例 × 60`。

---

### 11. DefenseBreak — 这就破防了？

| 属性 | 值 |
|---|---|
| **技能类型** | Support（辅助） |
| **目标类型** | SingleEnemy（敌方单体） |
| **等级范围** | L1 / L2 / L3 |

**效果：** 施加 `DefBonusDown`：防御力降低固定值，持续 2 回合。

| 等级 | 法力消耗 | 防御降低 |
|---|---|---|
| L1 | 12 | -400 |
| L2 | 16 | -600 |
| L3 | 20 | -800 |

**AI 优先度：** 高防御目标优先。`20 + defNorm × 50`

---

### 12. Terminate — 终结

| 属性 | 值 |
|---|---|
| **技能类型** | SingleAttack（单体攻击） |
| **目标类型** | SingleEnemy（敌方单体） |
| **等级范围** | L1 / L2 / L3 |
| **法力消耗** | 无 |

**效果：** 造成 **100% MaxHP** 真实伤害。仅当目标 HP 低于阈值时可释放。

| 等级 | 阈值 |
|---|---|
| L1 | HP < 10% |
| L2 | HP < 13% |
| L3 | HP < 16% |

**AI 优先度：** 仅对低于阈值的有效目标。`60 + MaxHP/1000 × 20`

---

### 13. DarkCurse — 黑暗诅咒

| 属性 | 值 |
|---|---|
| **技能类型** | SingleAttack（单体攻击） |
| **目标类型** | SingleEnemy（敌方单体） |
| **等级范围** | L1 / L2 / L3 |

**效果：** 施加 2 层 `Poison` 效果（每回合受到 2.5% MaxHP 真实伤害），持续 2 回合。

| 等级 | 法力消耗 |
|---|---|
| L1 | 7 |
| L2 | 9 |
| L3 | 11 |

**AI 优先度：** 高血量敌人优先。`20 + HP比例 × 50 + MaxHP/1000 × 10`

---

### 14. Swamp — 沼泽

| 属性 | 值 |
|---|---|
| **技能类型** | Support（辅助） |
| **目标类型** | AllEnemies（敌方全体） |
| **等级范围** | L1 / L2 / L3 |

**效果：** 敌方全体速度降低 + 行动延后。

| 等级 | 法力消耗 | 速度降低 | 行动延后 |
|---|---|---|---|
| L1 | 15 | -15% | +50% |
| L2 | 20 | -20% | +75% |
| L3 | 25 | -25% | +100% |

**AI 优先度：** 敌方越多越优先。`min(敌方数 × 25, 80)`

---

### 15. LightningChain — 闪电链

| 属性 | 值 |
|---|---|
| **技能类型** | Spread（扩散） |
| **目标类型** | SingleEnemy（敌方单体） |
| **等级范围** | L1 / L2 / L3 / L4 |

**效果：** 对目标造成 **150% ATK** 的伤害，然后在 3×3 范围内弹射，每跳间隔 0.2s。

| 等级 | 法力消耗 | 弹射次数 |
|---|---|---|
| L1 | 5 | 3 |
| L2 | 6 | 4 |
| L3 | 7 | 5 |
| L4 | 8 | 6 |

**AI 优先度：** 邻近敌人越多越优先。`30 + min(邻敌数 × 15, 45) + 弹射数 × 3`

---

### 16. DiamondDust — 钻石星辰

| 属性 | 值 |
|---|---|
| **技能类型** | Spread（扩散） |
| **目标类型** | SingleEnemy（敌方单体） |
| **等级范围** | L1 |
| **法力消耗** | 15 |

**效果：** 使目标所在排的所有敌方单位陷入 `Freeze`（冰冻，无法行动），持续 2 回合。

**AI 优先度：** 同排敌人越多越优先。`20 + min(同排数 × 25, 70)`

---

### 17. Melt — 熔化

| 属性 | 值 |
|---|---|
| **技能类型** | SingleAttack（单体攻击） |
| **目标类型** | SingleEnemy（敌方单体） |
| **等级范围** | L1 |
| **法力消耗** | 15 |

**效果：** 仅可对冰冻目标使用。造成 **20% MaxHP + 500% ATK** 真实伤害，并移除 `Freeze` 效果。

**AI 优先度：** 仅对有冰冻的目标有效，低血量优先。`50 + (1 - HP比例) × 35`

---

### 18. TheLastStand — 背水一战

| 属性 | 值 |
|---|---|
| **技能类型** | Support（辅助） |
| **目标类型** | AllAllies（友方全体） |
| **等级范围** | L1 |
| **法力消耗** | 20 |

**效果：** 全体友方施加两个效果（持续 3 回合）：
- `DmgBonusUp`：**造成伤害 +100%**
- `DmgReductionDown`：**受到伤害 +50%**

**AI 优先度：** 友方越多越优先。`min(友方数 × 20, 80)`

---

### 19. Armageddon — 哈米吉多顿

| 属性 | 值 |
|---|---|
| **技能类型** | AoE（群体攻击） |
| **目标类型** | AllEnemies（敌方全体） |
| **等级范围** | L1 |
| **法力消耗** | 见效果 |

**效果：**
- **前提：** 己方仅剩 1 个存活单位
- **代价：** 施法者 HP 降为 1，Mana 降为 1
- **伤害：** 对敌方全体造成 **500% MaxHP** 真实伤害

**AI 优先度：** 固定 95（绝境优先）。

---

## AI 优先度总览

| 排名 | 技能 | 优先度 | 逻辑 |
|---|---|---|---|
| ★★★ | Armageddon | 95 | 绝境杀招 |
| ★★ | Terminate | 60-80 | HP 低于阈值时执行 |
| ★★ | AtkStrongUp | 65 | 强化 Buff |
| ★★ | NormalAttack | 50-80 | 低血量优先 |
| ★★ | ManaDrain | 15-85 | 自身缺蓝优先 |
| ★ | Shield | 0-80 | 低护盾/低血量优先 |
| ★ | Heal | 5-85 | 低血量优先 |
| ★ | ThornsWrap | 10-80 | 高血量敌人优先 |
| ★ | DarkCurse | 10-80 | 高血量敌人优先 |
| ★ | Melt | 50-85 | 仅冰冻目标 |
| ★ | EquivalentExchange | 5-80 | MP >> HP 时使用 |
| ★ | FlashStrike | 10-80 | 负面效果越多越优先 |
| ★ | SandStorm | 20-85 | 敌人越多越优先 |
| ★ | Swamp | 25-80 | 敌人越多越优先 |
| ★ | TheLastStand | 15-80 | 友方越多越优先 |
| ★ | PenetrateArrow | 30-85 | 同列敌人越多越优先 |
| ★ | LightningChain | 30-85 | 邻敌越多越优先 |
| ★ | DiamondDust | 20-70 | 同排敌人越多越优先 |
| ★ | DefenseBreak | 20-70 | 高防御目标优先 |

## 战斗效果一览

| ID | 名称 | 类型 | 叠层上限 | 可驱散 | 效果 |
|---|---|---|---|---|---|
| Thorns | 荆棘 | Damage | 5 | 是 | 每回合受到固定值伤害 |
| Poison | 中毒 | Damage | 5 | 是 | 每回合受到 2.5% MaxHP × 层数 真实伤害 |
| Freeze | 冰冻 | Control | 1 | 是 | CanAct = false，无法行动 |
| AtkMultUp/Down | 攻击乘数 | StatChange | 1 | 负面可驱散 | 攻击力 ×(1±%) |
| DefBonusUp/Down | 防御附加 | StatChange | 1 | 负面可驱散 | 防御力 ±固定值 |
| SpdMultUp/Down | 速度乘数 | StatChange | 1 | 负面可驱散 | 速度 ×(1±%) |
| DmgBonusUp/Down | 伤害修正 | StatChange | 1 | 负面可驱散 | 造成伤害 ±% |
| DmgReductionUp/Down | 受伤修正 | StatChange | 1 | 负面可驱散 | 受到伤害 ±% |

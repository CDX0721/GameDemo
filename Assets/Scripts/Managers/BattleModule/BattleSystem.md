# 战斗系统设计文档

## 1. 单位、技能、效果的关联

### 单位 (BattleUnit / BattleUnitInstance)

**数据模板** (`BattleUnit`) 定义单位的六维基础属性：

| 属性 | 说明 |
|------|------|
| Attack | 攻击力 |
| Defense | 防御力 |
| HP | 生命值 |
| Speed | 速度 |
| Mana | 法力值 |

**对局实例** (`BattleUnitInstance`) 是对局中的运行时对象，持有模板引用和所有会话状态。最终属性公式：

```
最终属性 = 基础属性 × 乘数 + 额外值
```

其中 `乘数` 和 `额外值` 由附加效果（buff/debuff）动态修改，每回合开始时重置为默认值后重新计算。

实例分为两种子类型，仅用于标识操控方式：
- `PlayableUnitInstance` — 玩家操控
- `AutoUnitInstance` — AI 操控

### 技能 (Skill)

技能是单位在战斗中的行为单元。采用 **闭包捕获上下文 + 仅传承受方列表** 的设计——施法者、技能等级、伤害数值等信息在创建委托时通过闭包捕获，仅将承受方单位列表作为参数传入（支持 AoE）。

```
技能
├── 等级 (Level):           影响函数通过闭包捕获此属性按等级缩放
├── 类型 (SkillType):       单体攻击 / 扩散 / 范围 / 防御 / 治疗 / 辅助
├── 目标 (TargetType):      单体敌方 / 全体敌方 / 单体友方 / 全体友方 / 单体双方 / 全体双方
├── 释放条件列表 (CanCastConditions):  List<Func<List<BUI>, bool>>
└── 影响函数列表 (ApplyActions):       List<Action<List<BUI>>>
```

**释放判定**：遍历 `CanCastConditions` 列表，所有条件返回 `true` 时才可释放（AND 逻辑，空列表视为无条件通过）。

**影响执行**：按顺序遍历 `ApplyActions` 列表，依次执行所有影响函数。

### 附加效果 (BattleEffect / BattleEffectInstance)

**效果模板** (`BattleEffect`) 定义，同样采用闭包捕获上下文的设计：

```
BattleEffect
├── 效果类型:  正面 / 负面 / 其他
├── 状态类型:  控制 / 属性变化 / 持续伤害 / 标记
├── 影响函数列表 (ApplyActions):  List<Action<List<BUI>>>
├── 初始持续回合 (InitialTurns)
└── 最大叠加层数 (MaxStackCount)
```

**效果实例** (`BattleEffectInstance`) 以 `(Template.Id, Source)` 作为去重关键字：
- 相同关键字 → 层数 +1，刷新剩余回合
- 不同关键字 → 新增实例

影响函数通过闭包捕获 `BattleEffectInstance` 引用以读取 `CurrentStackCount`，无需参数传递。

### 伤害计算流程

```
原始伤害
  → × (1 - DamageReduction)     // 技能/效果的伤害减免
  → × (1 - DefenseRate)          // 防御比例减伤
  → 扣除护盾                     // Shield 吸收等量伤害（等效生命值）
  → 应用到 HP
```

**防御减伤公式** (K = 1000)：

| DEF | DefenseRate | 最终伤害乘数 |
|-----|-------------|-------------|
| ≥ 0 | 1 − e^(−DEF/K) | e^(−DEF/K)，0~1 之间 |
| < 0 | max(−4, −0.5(DEF/K)² + DEF/K) | 1 − rate，1~5 之间 |

`DamageReduction` 和 `DefenseRate` 作为独立乘数分别应用，互不干扰。

---

## 2. 行动队列与阵形

### 行动队列 (ActionQueue)

每个单位持有一个 `下一次行动所需行动值 (ActionValue)`：

```
ActionValue = RemainingCost / CurrentSpeed
```

- `RemainingCost`：剩余行动代价，初始值在创建时设定
- `CurrentSpeed`：当前速度（可被 buff/debuff 修改）

队列中所有单位按 `ActionValue` **升序排列**，值最小的位于队首。

**时间推进** (`AdvanceTime`)：

```
t = 队首单位的 ActionValue
所有单位: RemainingCost -= t × CurrentSpeed
队首单位 RemainingCost → 0，获得行动权
重新排序
```

速度快、代价低的单位行动频率更高。

**单回合周期**：

```
PreAction → WaitingAction → PostAction
     ↑                          |
     └──────────────────────────┘
```

`PostAction` 结束时调用 `unit.ResetCost()` 将 `RemainingCost` 重置为 `InitialCost`，然后重建队列进入下一轮。

### 阵形 (Formation)

双方各自拥有一个 **3×3 网格阵形**：

```
列:  0    1    2
行 0 [ ] [  ] [  ]    ← 前排
行 1 [ ] [  ] [  ]    ← 中排
行 2 [ ] [  ] [  ]    ← 后排
```

- `BattleSlot(int row, int col)` 表示格子坐标
- 单位普及时通过 `PlaceUnit(unit, slot)` 放入阵形
- 阵亡时通过 `RemoveUnit(slot)` 移出

当前技能的目标选择尚未与阵形位置深度耦合，`FindTargetForSkill` 仅按敌方/友方阵形返回首个存活单位。

---

## 3. 状态机

BattleStart → PreAction → WaitingAction → Acting → PostAction → (循环回 PreAction)

各状态均可转入 Paused（暂停）或 BattleEnd（结束）。

### BattleStart（战斗开始）
- 初始化双方阵形和行动队列
- 订阅效果事件
- 直接转入 PreAction

### PreAction（行动前）
1. 选中队列队首单位
2. **时间推进**：`AdvanceTime()` 更新所有单位行动代价
3. **刷新非 DoT 效果**：`RefreshPersistentEffects` 重置修正并重新应用非持续伤害效果
4. **单独应用 DoT**：遍历自身效果，仅执行持续伤害类效果
5. **重算属性**：`RecalculateStats()`
6. **清理死亡单位**
7. 检查游戏结束、死亡、无法行动
8. 否则进入 WaitingAction

### WaitingAction（等待行动）
- 调用回调获取 `List<(Skill, List<BUI>)>` 行动列表，存储到 `_pendingActions`
- 立即转入 Acting

### Acting（行动中）
- 遍历 `_pendingActions`，对每条记录实时检查 `CanCast(targets)` → 满足则执行 `Apply(targets)`
- **每次释放技能后**：调用 `RefreshAllUnits()` 刷新双方所有单位的非 DoT 效果
- 若施法者死亡则 break，目标列表为空则 continue
- 完成后进入 PostAction

### PostAction（行动后）
1. **效果倒计时**：自身效果 `RemainingTurns--`，到期移除（仅此阶段减少回合数）
2. **刷新自身效果**：`RefreshPersistentEffects` 重置修正并重新应用非 DoT 效果
3. **重置代价**：`RemainingCost = InitialCost`
4. **重建队列**：收集双方存活单位重新排序
5. 检查游戏结束，否则回到 PreAction

#### 效果刷新方法 (`RefreshPersistentEffects`)
```
ResetModifiers → 遍历效果（跳过 Damage 类）→ ApplyTo → RecalculateStats
```
调用点：
- 每个技能释放后 → `RefreshAllUnits()` 刷新双方全体单位
- PostAction 结束前 → 刷新行动单位自身（处理效果过期）

### BattleEnd（战斗结束）
- 触发 `OnBattleEnded` 事件，参数 `true` 为我方胜利
- 状态机停转

### Paused（暂停）
- 预留状态，当前未使用

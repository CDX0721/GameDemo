# UIModule — UI 管理模块

## 架构概览

```
┌──────────────────────────────┐
│  UIRoot (MonoBehaviour)      │  ← Unity 桥接：持有 Canvas，初始化 UIManager
├──────────────────────────────┤
│  UIManager (纯 C# 单例)      │  ← 核心：栈导航、面板注册表、生命周期调度
├──────────────────────────────┤
│  UIPanel (MonoBehaviour)     │  ← 面板基类：生命周期钩子、CanvasGroup 管理
├──────────────────────────────┤
│  IPanelFactory (接口)         │  ← 注入点：解耦面板实例化策略
└──────────────────────────────┘
```

**设计原则**：

- **UIManager 是纯 C# 单例**，不继承 MonoBehaviour，不直接调用 `Object.Instantiate`
- **UIRoot 是 Unity 桥接层**，是一个必须挂载在 Canvas 上的 MonoBehaviour，在 `Awake()` 中将 Canvas 和协程宿主注入 UIManager
- **UIPanel 用 CanvasGroup 控制可见性**（而非 `SetActive`），避免破坏面板内部状态
- **IPanelFactory 解耦实例化策略**，方便后续切换 Resources / Addressables / 对象池

---

## 快速开始

### 1. 搭建 UI 环境

在 Unity 菜单栏点击 **GameDemo → Setup UI Root**，自动创建 Canvas + UIRoot + EventSystem。

或手动操作：在场景的 Canvas 上挂载 `UIRoot` 组件。

### 2. 创建第一个面板

```csharp
using GameDemo.UI;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuPanel : UIPanel
{
    [SerializeField] private Button _startButton;

    protected override void OnShow()
    {
        _startButton.onClick.AddListener(HandleStart);
    }

    protected override void OnHide()
    {
        _startButton.onClick.RemoveListener(HandleStart);
    }

    private void HandleStart()
    {
        UIManager.Instance.Push<GameHudPanel>();
    }
}
```

将脚本挂载在 Canvas 下的 GameObject 上，面板会自动注册到 UIManager。

### 3. 调用 API

```csharp
// 显示面板（替换当前）
UIManager.Instance.Show<MainMenuPanel>();

// 压入栈（可返回）
UIManager.Instance.Push<SettingsPanel>();

// 返回上一级
UIManager.Instance.Pop();
```

---

## UIManager API 参考

### 初始化与配置

| 方法 | 说明 |
|---|---|
| `UIManager.Instance` | 获取单例，首次访问时自动懒加载创建 |
| `Initialize(Canvas, MonoBehaviour)` | 由 UIRoot 调用；传入根 Canvas 和协程宿主 |
| `SetPanelFactory(IPanelFactory)` | 注入面板工厂；未设置时只能使用场景预置面板 |
| `IsInitialized` | 是否已完成初始化 |

### 显示与隐藏（非栈模式）

#### Show\<T\>()

```csharp
MainMenuPanel panel = UIManager.Instance.Show<MainMenuPanel>();
```

- 显示目标面板，隐藏当前面板
- 若 T 已在栈中（被暂停），则弹出到 T 并恢复
- 若 T 已是当前面板，无操作
- 返回面板实例；初始化前调用返回 null

#### Hide\<T\>()

```csharp
UIManager.Instance.Hide<SettingsPanel>();
```

- 隐藏指定类型面板
- 若目标为当前面板，恢复栈中下一层面板
- 若目标在栈中但非当前，从栈中移除并隐藏

#### HideAll()

```csharp
UIManager.Instance.HideAll();
```

- 隐藏所有面板，清空导航栈

### 栈导航

#### Push\<T\>()

```csharp
UIManager.Instance.Push<InventoryPanel>();
```

- 暂停当前面板，压入新面板到栈顶并显示
- 新面板覆盖在原面板之上，Pop 后可返回

#### Pop()

```csharp
UIManager.Instance.Pop();
```

- 弹出栈顶面板并隐藏，恢复前一个面板
- 栈内 ≤1 项时打印警告，无操作

#### PopTo\<T\>()

```csharp
UIManager.Instance.PopTo<MainMenuPanel>();
```

- 弹出栈顶面板直到找到目标类型，恢复目标面板
- T 不在栈中时打印错误，无操作
- 若 T 已是当前面板，无操作

#### PopToRoot()

```csharp
UIManager.Instance.PopToRoot();
```

- 弹出所有面板只保留栈底，恢复栈底面板

### 查询

| 方法 | 说明 |
|---|---|
| `Get<T>()` | 获取已注册的面板实例；未注册时尝试通过工厂创建 |
| `GetActivePanel()` | 获取当前活动面板（可能为 null） |
| `IsShowing<T>()` | 检查指定类型面板是否当前活动 |
| `StackCount` | 当前导航栈深度 |

---

## UIPanel 生命周期

### PanelState 状态机

```
  Awake
    │
    ▼
 Closed ──→ Open ──→ Active ⇄ Paused
              │         │
              └─── Hide │
                        ▼
                     Closed
```

| 状态 | 含义 |
|---|---|
| `Closed` | 从未打开或已完全隐藏 |
| `Open` | 首次打开（仅触发一次 OnOpen） |
| `Active` | 当前可见且可交互 |
| `Paused` | 被其他面板覆盖，不可见但保持状态 |

### 生命周期钩子

```csharp
protected virtual void OnOpen()    { }  // 首次显示时调用一次
protected virtual void OnShow()    { }  // 每次变为 Active 时调用
protected virtual void OnHide()    { }  // 每次完全隐藏时调用
protected virtual void OnPause()   { }  // 被其他面板覆盖时调用
protected virtual void OnResume()  { }  // 覆盖面板移除后恢复时调用
protected virtual void OnClose()   { }  // 面板被销毁前调用
```

**典型绑定模式**：在 `OnShow` 中注册事件，在 `OnHide` 中注销。

### 序列化字段

| 字段 | 默认值 | 说明 |
|---|---|---|
| `_registerOnAwake` | `true` | 是否在 Awake 中自动注册到 UIManager |
| `_startHidden` | `true` | 是否在 Awake 后初始隐藏 |

---

## 栈导航说明

### Show vs Push

| | Show | Push |
|---|---|---|
| 当前面板 | 被替换（不在栈中保留） | 被暂停（保留在栈中） |
| 返回上一级 | 不可返回 | 可 Pop 返回 |
| 适用场景 | 界面平级切换（Tab、主菜单） | 层级递进（子菜单、弹窗） |

```
Show<B>() 后:                    Push<B>() 后:

    ┌──────┐                         ┌──────┐
    │  B   │ ← 当前                  │  B   │ ← 当前
    └──────┘                         ├──────┤
     A 已不在栈中                     │  A   │ ← 暂停
                                     └──────┘
                                     Pop() → 回到 A
```

### 典型流程

```
MainMenu  ──Push──→  Settings  ──Push──→  ConfirmDialog
                                                  │
                                               Pop()
                                                  │
                                                  ▼
                                            Settings（恢复）
```

---

## IPanelFactory — 面板工厂接口

```csharp
public interface IPanelFactory
{
    UIPanel CreatePanel(Type panelType);
}
```

当 `Show<T>()` / `Push<T>()` 在注册表中找不到面板时，会调用工厂创建。未设置工厂时仅支持场景预置面板。

**示例实现（Resources 加载）**：

```csharp
public class ResourcesPanelFactory : IPanelFactory
{
    public UIPanel CreatePanel(Type panelType)
    {
        var prefab = Resources.Load<UIPanel>($"UI/{panelType.Name}");
        return Object.Instantiate(prefab, UIManager.Instance.RootCanvas.transform);
    }
}

// 初始化时注入
UIManager.Instance.SetPanelFactory(new ResourcesPanelFactory());
```

---

## UIRoot — Unity 桥接

挂载在 Canvas 上的 MonoBehaviour，负责：

- **场景启动时**：调用 `UIManager.Instance.Initialize(canvas, this)` 
- **场景重载时**：更新 Canvas 和协程宿主引用
- **场景卸载时**：通知 UIManager 清理无效引用

支持通过 `_prePlacedPanels` 数组预先拖入面板引用。

---

## UIManagerBootstrapper — 编辑器工具

菜单项 **GameDemo → Setup UI Root** 一键创建：

- Canvas（ScreenSpaceOverlay + CanvasScaler + GraphicRaycaster）
- UIRoot 组件
- EventSystem（含 StandaloneInputModule）

---

## 边界条件速查

| 场景 | 行为 |
|---|---|
| `Show<T>()` T 已是当前面板 | 无操作 |
| `Show<T>()` T 在栈中被暂停 | 弹出到 T 并 Resume |
| `Pop()` 栈空（≤1 项） | 打印警告，无操作 |
| `PopTo<T>()` T 不在栈中 | 打印错误，无操作 |
| 未初始化时调用任何 API | 打印错误，返回 null / 无操作 |
| 同名类型重复注册 | 覆盖并打印警告 |
| 场景重载 | UIRoot 重新初始化，旧面板 OnDestroy 自动取消注册 |
| 面板被外部 Destroy | OnDestroy 自动取消注册，后续 Get 可通过工厂重建 |

---

## 完整示例

```csharp
// ── MainMenuPanel.cs ──
using GameDemo.UI;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuPanel : UIPanel
{
    [SerializeField] private Button _playButton;
    [SerializeField] private Button _settingsButton;

    protected override void OnShow()
    {
        _playButton.onClick.AddListener(() => UIManager.Instance.Push<GameHudPanel>());
        _settingsButton.onClick.AddListener(() => UIManager.Instance.Push<SettingsPanel>());
    }

    protected override void OnHide()
    {
        _playButton.onClick.RemoveAllListeners();
        _settingsButton.onClick.RemoveAllListeners();
    }
}

// ── SettingsPanel.cs ──
public class SettingsPanel : UIPanel
{
    [SerializeField] private Button _backButton;

    protected override void OnShow()
    {
        _backButton.onClick.AddListener(() => UIManager.Instance.Pop());
    }

    protected override void OnHide()
    {
        _backButton.onClick.RemoveAllListeners();
    }
}

// ── 初始化流程（由 UIRoot 自动完成） ──
// 1. 菜单: GameDemo → Setup UI Root
// 2. 在 Canvas 下放置 MainMenuPanel GameObject
// 3. 在 MainMenuPanel 上挂载 MainMenuPanel 脚本
// 4. 运行场景 → Awake 自动注册 → 代码调用 Show<MainMenuPanel>()
```

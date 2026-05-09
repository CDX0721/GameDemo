# AssetManager 使用文档

## 概述

`AssetManager` 是 `GameDemo` 项目自定义的资产管理器，封装 Unity 的 `Resources` API，提供同步/异步加载、引用计数卸载、缓存管理和资源查询功能。

- **命名空间**: `GameDemo`
- **设计模式**: 单例（Singleton）
- **访问入口**: `AssetManager.Instance`
- **底层依赖**: `UnityEngine.Resources`

---

## API 参考

### 1. 同步加载

```csharp
// 加载 TextAsset（同步，阻塞主线程）
var asset = AssetManager.Instance.Load<TextAsset>("Config/game_settings");
if (asset != null)
    Debug.Log(asset.text);
```

| 签名 | 说明 |
|---|---|
| `T Load<T>(string path)` | 从 Resources 目录同步加载资源，自动缓存并初始化引用计数为 1。若已缓存则直接返回并递增引用计数。返回 `null` 表示加载失败。 |

**参数**: `path` — 相对 Resources 目录的路径，**不带扩展名**。例如 `Assets/Resources/Config/game_settings.json` 对应路径 `"Config/game_settings"`。

---

### 2. 异步加载

#### 2.1 Handle 模式

```csharp
var handle = AssetManager.Instance.LoadAsync<TextAsset>("Config/game_settings");

// 注册完成回调（若已完成则立即触发）
handle.OnCompleted += () =>
{
    var result = handle.GetResult<TextAsset>();
    if (result != null)
        Debug.Log($"加载完成: {result.text}");
};

// 在其他地方轮询
if (handle.IsDone)
    Debug.Log($"进度: {handle.Progress:P0}");
```

| 签名 | 说明 |
|---|---|
| `AssetHandle LoadAsync<T>(string path)` | 异步加载，立即返回 `AssetHandle`。若资源已缓存则返回已完成句柄；若已在加载中则返回现有句柄。 |

#### 2.2 协程模式

```csharp
StartCoroutine(AssetManager.Instance.LoadAsyncCoroutine<TextAsset>(
    "Config/game_settings",
    asset => { if (asset != null) Debug.Log(asset.text); }
));
```

| 签名 | 说明 |
|---|---|
| `IEnumerator LoadAsyncCoroutine<T>(string path, Action<T> onLoaded)` | 协程方式异步加载，完成后通过回调返回结果。适用于需要在协程上下文中使用的场景。 |

#### 2.3 AssetHandle 成员

| 成员 | 类型 | 说明 |
|---|---|---|
| `Path` | `string` | 资源路径 |
| `IsDone` | `bool` | 加载是否完成 |
| `Progress` | `float` | 加载进度 [0, 1] |
| `GetResult<T>()` | `T` | 获取已加载的资源，未完成时返回 `null` |
| `OnCompleted` | `event Action` | 完成回调；已完成时注册后立即触发 |

---

### 3. 资源卸载

#### 3.1 引用计数卸载

```csharp
var a1 = AssetManager.Instance.Load<TextAsset>("Config/game_settings"); // ref = 1
var a2 = AssetManager.Instance.Load<TextAsset>("Config/game_settings"); // ref = 2

AssetManager.Instance.Unload("Config/game_settings"); // ref = 1, 未真正卸载
AssetManager.Instance.Unload("Config/game_settings"); // ref = 0, 资源被释放
```

| 签名 | 说明 |
|---|---|
| `bool Unload(string path)` | 递减引用计数，计数归零时调用 `Resources.UnloadAsset` 释放资源。返回 `true` 表示资源已被释放。 |
| `bool Unload(AssetHandle handle)` | 通过句柄卸载，等同于 `Unload(handle.Path)`。 |

#### 3.2 强制卸载

```csharp
// 无视引用计数，立即卸载
AssetManager.Instance.ForceUnload("Config/game_settings");
```

| 签名 | 说明 |
|---|---|
| `void ForceUnload(string path)` | 无视引用计数强制卸载，同时清理缓存和活跃句柄。 |

#### 3.3 批量清理

```csharp
// 释放所有不再被引用的资源
AssetManager.Instance.UnloadUnused();

// 清空全部缓存
AssetManager.Instance.ClearCache();
```

| 签名 | 说明 |
|---|---|
| `void UnloadUnused()` | 调用 `Resources.UnloadUnusedAssets()` 并触发 GC。 |
| `void ClearCache()` | 卸载所有被 AssetManager 管理的资源，清空缓存和引用计数。 |

---

### 4. 查询

```csharp
if (AssetManager.Instance.IsLoaded("Config/game_settings"))
{
    var asset = AssetManager.Instance.Get<TextAsset>("Config/game_settings");
    Debug.Log($"引用计数: {AssetManager.Instance.GetRefCount("Config/game_settings")}");
}
```

| 签名 | 说明 |
|---|---|
| `bool IsLoaded(string path)` | 资源是否在缓存中 |
| `bool IsLoading(string path)` | 资源是否正在异步加载中 |
| `T Get<T>(string path)` | 获取缓存中的资源（**不递增引用计数**） |
| `int GetRefCount(string path)` | 获取当前引用计数 |
| `int CacheCount` | 缓存中的资源总数 |
| `int ActiveLoadCount` | 正在进行中的异步加载数 |

---

## 使用注意事项

1. **Resources 目录约定**: `AssetManager` 基于 Unity `Resources` API，所有资源必须放在 `Assets/Resources/` 目录下才能被加载。

2. **路径格式**: 加载路径相对于 Resources 目录，**不包含扩展名**。例如 `Assets/Resources/Prefabs/Enemy.prefab` → `"Prefabs/Enemy"`。

3. **引用计数**: 每次调用 `Load` / `LoadAsync` 会递增计数，释放时需对应调用相同次数的 `Unload`，或使用 `ForceUnload` 直接释放。

4. **避免资源泄漏**: 确保每次 `Load` 最终都有对应的 `Unload` 调用。可通过 `GetRefCount` 检查是否有未释放的引用。

5. **异步加载最小资源**: 在 Editor 中，小资源（如 TextAsset）可能在同一帧内完成异步加载，此时 `IsDone` 立即为 `true`，`IsLoading` 为 `false`。这是正常行为，代码已做兼容处理。

6. **类型安全**: 使用错误的泛型类型加载资源会返回 `null` 并在 Console 输出警告。例如用 `Load<Texture2D>` 加载文本文件。

---

## 典型用例

### 场景加载时预加载配置

```csharp
void Start()
{
    // 同步加载关键配置
    var config = AssetManager.Instance.Load<TextAsset>("Config/game_settings");
    ApplyConfig(config);

    // 异步预加载大体积资源
    StartCoroutine(PreloadAssets());
}

IEnumerator PreloadAssets()
{
    var handle = AssetManager.Instance.LoadAsync<GameObject>("Prefabs/Enemy");
    yield return new WaitUntil(() => handle.IsDone);
    // 资源已缓存，后续 Load 调用将立即返回
}
```

### 关卡切换时清理

```csharp
void OnDestroy()
{
    AssetManager.Instance.Unload("Prefabs/Enemy");
    AssetManager.Instance.Unload("Config/game_settings");
    // 或直接清空全部
    // AssetManager.Instance.ClearCache();
}
```

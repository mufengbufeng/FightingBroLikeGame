# Resrouce 模块说明

## 模块简介
Resrouce 模块基于 YooAssets 对项目资源进行统一管理，提供跨模式初始化、资源与场景加载、句柄生命周期管理等能力。通过 `ResourceModeConfig` ScriptableObject 可以在不改代码的情况下切换资源运行模式并配置多包裹策略。

## 目录结构
- `ResourceMode.cs`：定义资源运行模式枚举及与 YooAssets 播放模式的映射工具。
- `ResourceModeConfig.cs`：资源模块配置 ScriptableObject，描述模式、并发数与包裹列表。
- `DefaultResourceRemoteServices.cs`：默认远端地址服务实现，负责生成主/备 CDN 请求地址。
- `IResourceManager.cs`：资源管理器对外接口。
- `ResourceManager.cs`：资源管理器具体实现，封装 YooAssets 初始化、资源加载、句柄管理与收尾清理。

## ScriptableObject 配置
> 项目默认配置资产位于 `Assets/Resources/EFResourceModeConfig.asset`，可直接在 Unity Inspector 中编辑。

| 字段 | 说明 |
| --- | --- |
| `Mode` | 当前资源运行模式（编辑器、离线、联机、Web）。|
| `PackageVersion` | YooAsset 构建产物版本号。运行时仍从 CDN 的版本文件请求最新资源版本。 |
| `PlatformConfigs` | 标准平台、微信小游戏和抖音小游戏的 CDN 与播放模式策略。 |
| `BundleLoadingMaxConcurrency` | 同时加载 AssetBundle 的最大并发数。|
| `Packages` | 包裹配置列表，至少一个。默认包裹可在此勾选。|

**ResourcePackageEntry 字段**
- `PackageName`：与 YooAssets 构建时的包名保持一致。
- `IsDefault`：是否为默认包裹，未指定包名的加载请求会使用此包。若多个为 true，校验时仅保留第一个。
- `RemoteMainServer`：主资源服务器地址，自动补全 `/` 后缀。
- `RemoteFallbackServer`：备用资源服务器地址，可选。
- `DisableUnityWebCache`：Web 平台禁用 Unity 缓存。

## 平台部署策略
在 Unity Inspector 中打开 `Assets/Resources/EFResourceModeConfig.asset`，在“平台部署策略”列表内编辑对应平台的字段。运行时和资源构建共用这份 ScriptableObject 配置，不需要维护 JSON 文件。

| 字段 | 说明 |
| --- | --- |
| `Platform` | 平台名称：`Standard`、`WechatMiniGame` 或 `TiktokMiniGame`。 |
| `Mode` | YooAsset 播放模式。普通 Player 的 CDN 更新使用 `HostPlay`，小游戏使用 `WebPlay`。 |
| `Main Cdn` | 主 CDN 根地址，目录下应直接包含版本、清单和 Bundle 文件。 |
| `Fallback Cdn` | 可选的备用 CDN 根地址。 |
| `Disable Unity Web Cache` | 是否禁用 Web 平台的 Unity 缓存。 |
| `Wechat Mini Game Resource Delivery Mode` | 仅微信小游戏使用。标准微信转换管线应设为 `RemoteUpdate`；`BuiltinOnly` 仅适用于已实现包内 StreamingAssets 读取能力的自定义转换与文件系统管线。 |

微信转换工具中的“首包资源加载方式：小游戏包内”只控制 Unity 首个 Data 资源包，不会把 YooAsset 的 `StreamingAssets/yoo` 改为包内文件读取。标准转换管线仍会从 `DATA_CDN/StreamingAssets/yoo` 请求 YooAsset 清单与 Bundle，因此必须部署 CDN。开发环境可通过菜单 `微信小游戏/本地 CDN 工具` 启动 `http://127.0.0.1:18081`，并开启微信转换配置中的 `Development Build`；正式发布需改用无显式端口的 HTTPS CDN 地址。

远端构建产物会输出到 `Bundles`，不会自动上传 CDN。请在 Inspector 中填入实际 CDN 地址后再发布。

## 初始化流程
```csharp
var resourceManager = new ResourceManager();
await resourceManager.InitializeAsync(); // 默认从 Resources/EFResourceModeConfig.asset 加载
```

- 若需自定义配置，可手动构建 `ResourceModeConfig` 实例并传入 `InitializeAsync`。
- 初始化过程中会按照配置遍历包裹，逐个创建并执行 YooAssets 初始化操作，支持进度回调。

## 资源加载示例
```csharp
// 异步加载 Prefab
AssetHandle prefabHandle = await resourceManager.LoadAssetAsync<GameObject>("UI/Panel_Main", progress: p =>
{
    Debug.Log($"加载进度: {p:P0}");
});

// 同步加载
AssetHandle syncHandle = resourceManager.LoadAssetSync<GameObject>("UI/Panel_Main");

// 场景加载
SceneHandle sceneHandle = await resourceManager.LoadSceneAsync("Scenes/Example", LoadSceneMode.Single);

// 使用完毕释放
resourceManager.Release(prefabHandle);
resourceManager.UnloadScene(sceneHandle);
```

## 关闭与清理
在游戏退出或模块失效时调用：
```csharp
resourceManager.Shutdown();
```
该过程会释放所有追踪句柄、销毁 YooAssets 包裹，并在必要时调用 `YooAssets.Destroy()`。

## 注意事项
1. Host/Web 模式下必须配置有效的远端地址，否则初始化会抛出异常。
2. EditorSimulate 模式需要在 Unity 编辑器中运行，且需保证模拟构建可生成清单。
3. 建议在初始化阶段提供进度回调，方便 UI 展示资源加载状态。
4. 若外部手动释放句柄，请同步调用 `resourceManager.Release(handle)` 保持内部追踪一致。

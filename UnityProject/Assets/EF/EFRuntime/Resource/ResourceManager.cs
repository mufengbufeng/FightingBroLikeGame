using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using EF.Common;
using EF.Debugger;
using UnityEngine;
using UnityEngine.SceneManagement;
using YooAsset;
using SceneHandle = YooAsset.SceneHandle;

namespace EF.Resource
{
    /// <summary>
    /// 资源管理器，按配置在 YooAssets 与 Resources 直读后端之间切换。
    /// </summary>
    public sealed class ResourceManager : AEFManager, IResourceManager
    {
        #region 字段

        private readonly Dictionary<string, ResourcePackage> _packages = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<HandleBase> _trackedHandles = new();
        private readonly Dictionary<int, List<HandleBase>> _assetHandles = new();
        private readonly Dictionary<HandleBase, int> _assetHandleInstanceIds = new();
        private readonly ResourceBackgroundDownloadService _backgroundDownloads = new();

        private ResourceModeConfig _config;
        private string _defaultPackageName;
        private bool _isInitialized;
        private bool _usesYooAssets;
        private bool _ownsYooAssets;

        #endregion

        #region 属性

        /// <summary>
        /// 当前实际运行模式；未加载配置时默认使用编辑器模拟模式。
        /// </summary>
        public ResourceMode Mode => RuntimeMode;

        /// <summary>
        /// 资源模块是否已完成初始化。
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// 当前是否使用 YooAssets 资源包后端。
        /// </summary>
        public bool UsesYooAssets => _usesYooAssets;

        /// <summary>
        /// 当前默认资源包名称。
        /// </summary>
        public string DefaultPackageName => _defaultPackageName;

        /// <summary>
        /// 当前资源管理器使用的运行配置。
        /// </summary>
        public ResourceModeConfig Configuration => _config;

        /// <summary>
        /// 当前平台的移动端后台下载服务。
        /// </summary>
        public IResourceBackgroundDownloadService BackgroundDownloads => _backgroundDownloads;

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化当前配置的资源后端；Resources 后端不会初始化 YooAssets。
        /// </summary>
        /// <param name="overrideConfig">外部指定的资源配置；为 null 时从默认 Resources 路径加载。</param>
        /// <param name="progress">初始化总进度回调。</param>
        public async UniTask InitializeAsync(ResourceModeConfig overrideConfig = null, IProgress<float> progress = null)
        {
            if (_isInitialized)
            {
                progress?.Report(1f);
                return;
            }

            _config = overrideConfig ?? LoadDefaultConfig();
            if (_config == null)
            {
                throw new InvalidOperationException($"未找到资源配置文件，请确认 Resources/{ResourceModeConfig.DefaultResourcesPath}.asset 是否存在");
            }

            _usesYooAssets = _config.UseYooAssets;
            if (!_usesYooAssets)
            {
                _isInitialized = true;
                progress?.Report(1f);
                Log.Info("资源管理器已启用 Resources 直读后端，跳过 YooAssets 初始化。");
                return;
            }

            EnsureYooAssetsInitialized();

            if (_config.Packages == null || _config.Packages.Count == 0)
            {
                throw new InvalidOperationException("资源配置未包含任何包裹，请至少配置一个包裹信息");
            }

            Log.Info($"已加载资源部署配置，构建版本 {_config.PackageVersion}。");

            _packages.Clear();
            _defaultPackageName = null;

            IReadOnlyList<ResourcePackageEntry> entries = _config.Packages;
            int total = entries.Count;
            for (int index = 0; index < total; index++)
            {
                ResourcePackageEntry entry = entries[index];
                if (entry == null)
                {
                    continue;
                }

                if (!YooAssets.TryGetPackage(entry.PackageName, out ResourcePackage package))
                {
                    package = YooAssets.CreatePackage(entry.PackageName);
                }

                if (entry.IsDefault || string.IsNullOrEmpty(_defaultPackageName))
                {
                    _defaultPackageName = package.PackageName;
                }

                Log.Info($"开始初始化资源包裹 {entry.PackageName}，运行模式 {RuntimeMode}...");

                InitializePackageOptions options = CreateInitializeParameters(entry);
                options.BundleLoadingMaxConcurrency = _config.BundleLoadingMaxConcurrency;

                InitializePackageOperation operation = package.InitializePackageAsync(options);
                await operation;
                if (operation.Status != EOperationStatus.Succeeded)
                {
                    throw new InvalidOperationException($"资源包裹 {entry.PackageName} 初始化失败：{operation.Error}");
                }

                await UpdatePackageAsync(package);

                if (_packages.TryGetValue(package.PackageName, out ResourcePackage existing) && !ReferenceEquals(existing, package))
                {
                    DestroyPackageOperation destroyOperation = existing.DestroyPackageAsync();
                    await destroyOperation;
                    if (destroyOperation.Status == EOperationStatus.Succeeded)
                    {
                        YooAssets.RemovePackage(existing.PackageName);
                    }
                }

                _packages[package.PackageName] = package;
                progress?.Report(CalcProgress(index + 1, total, 0f));
            }

            if (string.IsNullOrEmpty(_defaultPackageName))
            {
                ResourcePackageEntry fallbackEntry = _config.GetDefaultPackage();
                _defaultPackageName = fallbackEntry?.PackageName;
            }

            _isInitialized = true;
            progress?.Report(1f);
        }

        /// <summary>
        /// 更新单个资源包裹，并按配置处理弱联网本地回退。
        /// </summary>
        private async UniTask UpdatePackageAsync(ResourcePackage package)
        {
            var operations = new YooAssetResourcePackageUpdateOperations(
                package,
                _backgroundDownloads,
                _config.UpdateSettings);
            ResourceRuntimeCapabilities capabilities = ResourceRuntimeCapabilities.Current;
            bool usesWechatBuiltinWebServer = ResourceModeResolver.UsesWechatBuiltinWebServer(
                ConfiguredWechatMiniGameResourceDeliveryMode,
                capabilities.Platform);
            ResourcePackageUpdateResult result;
            if (usesWechatBuiltinWebServer)
            {
                Log.Info($"资源包裹 {package.PackageName} 使用微信内置资源模式，跳过 CDN 更新。");
                result = await ResourcePackageUpdateCoordinator.ActivateBuiltinAsync(
                    operations,
                    _config.UpdateSettings);
            }
            else
            {
                bool allowLocalFallback = RuntimeMode == ResourceMode.HostPlay &&
                                          _config.UpdateSettings.EnableWeakNetworkFallback;
                result = await ResourcePackageUpdateCoordinator.UpdateAsync(
                    operations,
                    _config.UpdateSettings,
                    allowLocalFallback);
            }

            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"资源包裹 {package.PackageName} 激活失败：{result.Error}");
            }

            if (result.UsedLocalFallback)
            {
                Log.Warning(
                    $"资源包裹 {package.PackageName} 远端更新失败，已回退到本地版本 {result.ActiveVersion}：{result.RemoteError}");
            }
        }

        #endregion

        #region 包裹管理

        /// <summary>
        /// 获取已初始化并缓存的指定资源包裹。
        /// </summary>
        /// <param name="packageName">资源包裹名称。</param>
        /// <returns>匹配名称的资源包裹。</returns>
        public ResourcePackage GetPackage(string packageName)
        {
            EnsureYooAssetsBackend();

            if (string.IsNullOrWhiteSpace(packageName))
            {
                throw new ArgumentException("包裹名称不能为空", nameof(packageName));
            }

            if (_packages.TryGetValue(packageName, out ResourcePackage package))
            {
                return package;
            }

            throw new KeyNotFoundException($"未找到名称为 {packageName} 的资源包，请检查配置");
        }

        /// <summary>
        /// 获取初始化时选定的默认资源包裹。
        /// </summary>
        /// <returns>默认资源包裹。</returns>
        public ResourcePackage GetDefaultPackage()
        {
            EnsureYooAssetsBackend();

            if (string.IsNullOrEmpty(_defaultPackageName))
            {
                throw new InvalidOperationException("未设置默认资源包，请在配置中勾选默认包裹");
            }

            return GetPackage(_defaultPackageName);
        }

        #endregion

        #region 资源加载

        /// <summary>
        /// 通过当前资源后端加载资源；YooAssets 后端会在释放对象时归还对应句柄。
        /// </summary>
        /// <typeparam name="T">要加载的 Unity 资源类型。</typeparam>
        /// <param name="location">资源定位地址。</param>
        /// <param name="progress">加载进度回调。</param>
        /// <param name="priority">YooAssets 后端的加载优先级。</param>
        /// <returns>加载完成的资源对象。</returns>
        public async UniTask<T> Load<T>(string location, Action<float> progress = null, uint priority = 0)
            where T : UnityEngine.Object
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(location))
            {
                throw new ArgumentException("资源定位地址不能为空", nameof(location));
            }

            if (!_usesYooAssets)
            {
                T resource = Resources.Load<T>(location);
                if (resource == null)
                {
                    throw new InvalidOperationException($"未找到 Resources 资源：{location}");
                }

                progress?.Invoke(1f);
                return resource;
            }

            AssetHandle handle = await LoadAssetAsync<T>(location, progress, priority);
            T asset = handle.AssetObject as T;
            if (asset == null)
            {
                Release(handle);
                throw new InvalidOperationException($"加载资源失败或类型不匹配：{location}");
            }

            RegisterAssetHandle(asset, handle);
            return asset;
        }

        /// <summary>
        /// 从默认资源包裹异步加载资源，并登记返回的资源句柄。
        /// </summary>
        /// <typeparam name="T">要加载的 Unity 资源类型。</typeparam>
        /// <param name="location">资源定位地址。</param>
        /// <param name="progress">加载进度回调。</param>
        /// <param name="priority">加载优先级。</param>
        /// <returns>完成加载的资源句柄。</returns>
        public async UniTask<AssetHandle> LoadAssetAsync<T>(string location, Action<float> progress = null, uint priority = 0)
            where T : UnityEngine.Object
        {
            EnsureYooAssetsBackend();
            if (string.IsNullOrWhiteSpace(location))
            {
                throw new ArgumentException("资源定位地址不能为空", nameof(location));
            }

            ResourcePackage package = GetDefaultPackage();
            AssetHandle handle = package.LoadAssetAsync<T>(location, priority);

            if (progress != null)
            {
                while (!handle.IsDone)
                {
                    progress(handle.Progress);
                    await UniTask.Yield();
                }
            }

            await handle;
            HandleFailureIfNeed(handle, location, "加载资源");
            RegisterHandle(handle);
            progress?.Invoke(1f);
            return handle;
        }

        /// <summary>
        /// 从默认资源包裹同步加载资源，并登记返回的资源句柄。
        /// </summary>
        /// <typeparam name="T">要加载的 Unity 资源类型。</typeparam>
        /// <param name="location">资源定位地址。</param>
        /// <param name="priority">加载优先级。</param>
        /// <returns>完成加载的资源句柄。</returns>
        public AssetHandle LoadAssetSync<T>(string location, uint priority = 0) where T : UnityEngine.Object
        {
            EnsureYooAssetsBackend();
            if (!ResourceRuntimeCapabilities.Current.SupportsSynchronousLoading)
            {
                throw new NotSupportedException("微信和抖音小游戏不支持同步资源加载，请使用 LoadAssetAsync");
            }

            if (string.IsNullOrWhiteSpace(location))
            {
                throw new ArgumentException("资源定位地址不能为空", nameof(location));
            }

            ResourcePackage package = GetDefaultPackage();
            AssetHandle handle = package.LoadAssetSync<T>(location);
            HandleFailureIfNeed(handle, location, "同步加载资源");
            RegisterHandle(handle);
            return handle;
        }

        #endregion

        #region 场景管理

        /// <summary>
        /// 从默认资源包裹异步加载场景，并登记返回的场景句柄。
        /// </summary>
        /// <param name="location">场景定位地址。</param>
        /// <param name="sceneMode">Unity 场景加载模式。</param>
        /// <param name="physicsMode">局部物理模式。</param>
        /// <param name="allowSceneActivation">是否允许场景加载完成后立即激活。</param>
        /// <param name="priority">加载优先级。</param>
        /// <param name="progress">加载进度回调。</param>
        /// <returns>完成加载的场景句柄。</returns>
        public async UniTask<SceneHandle> LoadSceneAsync(
            string location,
            LoadSceneMode sceneMode = LoadSceneMode.Single,
            LocalPhysicsMode physicsMode = LocalPhysicsMode.None,
            bool allowSceneActivation = true,
            uint priority = 0,
            Action<float> progress = null)
        {
            EnsureYooAssetsBackend();
            if (string.IsNullOrWhiteSpace(location))
            {
                throw new ArgumentException("场景定位地址不能为空", nameof(location));
            }

            ResourcePackage package = GetDefaultPackage();
            SceneHandle handle = package.LoadSceneAsync(location, sceneMode, physicsMode, allowSceneActivation, priority);

            if (progress != null)
            {
                while (!handle.IsDone)
                {
                    progress(handle.Progress);
                    await UniTask.Yield();
                }
            }

            await handle;
            HandleFailureIfNeed(handle, location, "加载场景");
            RegisterHandle(handle);
            progress?.Invoke(1f);
            return handle;
        }

        /// <summary>
        /// 取消追踪并异步卸载指定场景句柄。
        /// </summary>
        /// <param name="handle">要卸载的场景句柄；为 null 时不执行操作。</param>
        public void UnloadScene(SceneHandle handle)
        {
            if (handle == null)
            {
                return;
            }

            _trackedHandles.Remove(handle);
            UnloadSceneOperation operation = handle.UnloadSceneAsync();
            operation.Completed += completedOperation =>
            {
                if (completedOperation.Status == EOperationStatus.Failed)
                {
                    Log.Error($"卸载场景失败：{completedOperation.Error}");
                }
            };
        }

        #endregion

        #region 资源释放

        /// <summary>
        /// 取消追踪并释放指定资源句柄。
        /// </summary>
        /// <param name="handle">要释放的资源句柄；为 null 时不执行操作。</param>
        public void Release(HandleBase handle)
        {
            if (handle == null)
            {
                return;
            }

            RemoveAssetHandleReference(handle);
            _trackedHandles.Remove(handle);
            handle.Release();
        }

        /// <summary>
        /// 释放统一 Load 接口为资源登记的一个引用。
        /// </summary>
        /// <param name="asset">要释放的资源对象。</param>
        public void Release(UnityEngine.Object asset)
        {
            if (asset == null || !_assetHandles.TryGetValue(asset.GetInstanceID(), out List<HandleBase> handles)
                || handles.Count == 0)
            {
                return;
            }

            int lastIndex = handles.Count - 1;
            HandleBase handle = handles[lastIndex];
            handles.RemoveAt(lastIndex);
            if (handles.Count == 0)
            {
                _assetHandles.Remove(asset.GetInstanceID());
            }

            _assetHandleInstanceIds.Remove(handle);
            _trackedHandles.Remove(handle);
            handle.Release();
        }

        /// <summary>
        /// 释放所有由资源管理器追踪的资源句柄，并清空追踪记录。
        /// </summary>
        public void ReleaseAll()
        {
            if (_trackedHandles.Count > 0)
            {
                HandleBase[] buffer = new HandleBase[_trackedHandles.Count];
                _trackedHandles.CopyTo(buffer);
                foreach (HandleBase handle in buffer)
                {
                    handle?.Release();
                }
            }

            _trackedHandles.Clear();
            _assetHandles.Clear();
            _assetHandleInstanceIds.Clear();
        }

        #endregion

        #region 生命周期

        /// <summary>
        /// 释放所有追踪的资源并销毁包裹。
        /// </summary>
        public override void Shutdown()
        {
            ReleaseAll();

            if (_packages.Count > 0)
            {
                _packages.Clear();
            }

            if (_ownsYooAssets && YooAssets.IsInitialized)
            {
                YooAssets.Destroy();
            }

            _defaultPackageName = null;
            _config = null;
            _isInitialized = false;
            _usesYooAssets = false;
            _ownsYooAssets = false;
        }

        #endregion

        #region 私有辅助方法

        private void EnsureYooAssetsInitialized()
        {
            if (!YooAssets.IsInitialized)
            {
                YooAssets.Initialize(null);
                _ownsYooAssets = true;
            }
        }

        private static ResourceModeConfig LoadDefaultConfig()
        {
            // 资源后端由该配置决定，初始化前只能从 Resources 读取。
            ResourceModeConfig config = Resources.Load<ResourceModeConfig>(ResourceModeConfig.DefaultResourcesPath);
            return config;
        }

        private void RegisterHandle(HandleBase handle)
        {
            if (handle != null)
            {
                _trackedHandles.Add(handle);
            }
        }

        private void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("资源模块尚未初始化，请先调用 InitializeAsync");
            }
        }

        private void RegisterAssetHandle(UnityEngine.Object asset, HandleBase handle)
        {
            int instanceId = asset.GetInstanceID();
            if (!_assetHandles.TryGetValue(instanceId, out List<HandleBase> handles))
            {
                handles = new List<HandleBase>();
                _assetHandles.Add(instanceId, handles);
            }

            handles.Add(handle);
            _assetHandleInstanceIds[handle] = instanceId;
        }

        private void RemoveAssetHandleReference(HandleBase handle)
        {
            if (!_assetHandleInstanceIds.TryGetValue(handle, out int instanceId))
            {
                return;
            }

            _assetHandleInstanceIds.Remove(handle);
            if (!_assetHandles.TryGetValue(instanceId, out List<HandleBase> handles))
            {
                return;
            }

            handles.Remove(handle);
            if (handles.Count == 0)
            {
                _assetHandles.Remove(instanceId);
            }
        }

        private void EnsureYooAssetsBackend()
        {
            EnsureInitialized();
            if (!_usesYooAssets)
            {
                throw new NotSupportedException("当前资源配置未启用 YooAssets，请改用 Load<T> 读取 Resources 资源。");
            }
        }

        /// <summary>
        /// Editor 始终通过模拟文件系统加载，Player 使用配置的发布模式。
        /// 微信小游戏基于 WebGL，统一解析为 WebPlay，再由资源交付方式选择内置或 CDN 文件系统。
        /// </summary>
        private ResourceMode RuntimeMode
        {
            get
            {
#if UNITY_EDITOR
                return ResourceMode.EditorSimulate;
#else
                return ResourceModeResolver.ResolvePlayerMode(
                    ConfiguredMode,
                    ResourceRuntimeCapabilities.Current.Platform);
#endif
            }
        }

        /// <summary>
        /// 返回当前平台在部署配置中声明的发布模式。
        /// </summary>
        private ResourceMode ConfiguredMode
        {
            get
            {
                ResourceDeploymentPlatformConfig platformConfig = GetCurrentDeploymentPlatformConfig();
                return platformConfig != null
                    ? platformConfig.Mode
                    : _config != null
                        ? _config.Mode
                        : ResourceMode.EditorSimulate;
            }
        }

        /// <summary>
        /// 返回微信小游戏在部署配置中声明的资源交付方式。
        /// </summary>
        private WechatMiniGameResourceDeliveryMode ConfiguredWechatMiniGameResourceDeliveryMode
        {
            get
            {
                ResourceDeploymentPlatformConfig platformConfig = GetCurrentDeploymentPlatformConfig();
                return platformConfig != null
                    ? platformConfig.WechatMiniGameResourceDeliveryMode
                    : _config != null
                        ? _config.WechatMiniGameResourceDeliveryMode
                        : WechatMiniGameResourceDeliveryMode.RemoteUpdate;
            }
        }

        /// <summary>
        /// 获取当前编译平台对应的资源部署策略。
        /// </summary>
        private ResourceDeploymentPlatformConfig GetCurrentDeploymentPlatformConfig()
        {
            if (_config == null)
            {
                return null;
            }

            return _config.GetPlatformConfig(ResourceRuntimeCapabilities.Current.Platform);
        }

        /// <summary>
        /// 根据当前运行模式创建 YooAsset 初始化参数。
        /// </summary>
        private InitializePackageOptions CreateInitializeParameters(ResourcePackageEntry entry)
        {
            return RuntimeMode switch
            {
                ResourceMode.EditorSimulate => CreateEditorSimulateParameters(entry),
                ResourceMode.OfflinePlay => CreateOfflineParameters(),
                ResourceMode.HostPlay => CreateHostParameters(entry),
                ResourceMode.WebPlay => CreateWebParameters(entry),
                _ => CreateEditorSimulateParameters(entry)
            };
        }

#if UNITY_EDITOR
        private static InitializePackageOptions CreateEditorSimulateParameters(ResourcePackageEntry entry)
        {
            PackageBuildResult buildResult = EditorSimulateBuildInvoker.Build(entry.PackageName, (int)EBundleType.VirtualAssetBundle);
            string packageRoot = buildResult.PackageRootDirectory;
            FileSystemParameters fileSystemParams = FileSystemParameters.CreateDefaultEditorFileSystemParameters(packageRoot);

            return new EditorSimulateModeOptions
            {
                EditorFileSystemParameters = fileSystemParams
            };
        }
#else
        private static InitializePackageOptions CreateEditorSimulateParameters(ResourcePackageEntry entry)
        {
            throw new InvalidOperationException("编辑器模拟模式仅支持在 Unity 编辑器环境下运行");
        }
#endif

        private static InitializePackageOptions CreateOfflineParameters()
        {
            return new OfflinePlayModeOptions
            {
                BuiltinFileSystemParameters = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters()
            };
        }

        private InitializePackageOptions CreateHostParameters(ResourcePackageEntry entry)
        {
            ResourceDeploymentPlatformConfig platformConfig = GetCurrentDeploymentPlatformConfig();
            if (platformConfig != null)
            {
                platformConfig.ValidateRemoteCdn();
            }

            string defaultHostServer = platformConfig != null
                ? platformConfig.MainCdn
                : entry.GetSanitizedMainServer();
            string fallbackHostServer = platformConfig != null
                ? platformConfig.FallbackCdn
                : entry.GetSanitizedFallbackServer();
            Log.Info("资源主服务器地址：" + defaultHostServer);

            IRemoteService remoteService = new DefaultResourceRemoteServices(defaultHostServer, fallbackHostServer);
            FileSystemParameters cacheFileSystemParams = FileSystemParameters.CreateDefaultSandboxFileSystemParameters(remoteService);
            FileSystemParameters builtinFileSystemParams = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters();

            if (_config.UpdateSettings.EnableWeakNetworkFallback)
            {
                builtinFileSystemParams.AddParameter(EFileSystemParameter.CopyBuiltinPackageManifest, true);
                cacheFileSystemParams.AddParameter(EFileSystemParameter.InstallCleanupMode, EInstallCleanupMode.None);
            }

            return new HostPlayModeOptions
            {
                BuiltinFileSystemParameters = builtinFileSystemParams,
                CacheFileSystemParameters = cacheFileSystemParams
            };
        }

        private InitializePackageOptions CreateWebParameters(ResourcePackageEntry entry)
        {
            ResourceRuntimeCapabilities capabilities = ResourceRuntimeCapabilities.Current;
            ResourceDeploymentPlatformConfig platformConfig = GetCurrentDeploymentPlatformConfig();
            bool disableUnityWebCache = platformConfig != null
                ? platformConfig.DisableUnityWebCache
                : entry.DisableUnityWebCache;
            if (ResourceModeResolver.UsesWechatBuiltinWebServer(
                    ConfiguredWechatMiniGameResourceDeliveryMode,
                    capabilities.Platform))
            {
                FileSystemParameters wechatBuiltinFileSystemParams =
                    EF.MiniGame.MiniGameFileSystemFactory.CreateWechatBuiltinWebServer(disableUnityWebCache);
                return new WebPlayModeOptions
                {
                    WebServerFileSystemParameters = wechatBuiltinFileSystemParams
                };
            }

            if (platformConfig != null)
            {
                platformConfig.ValidateRemoteCdn();
            }

            string defaultHostServer = platformConfig != null
                ? platformConfig.MainCdn
                : entry.GetSanitizedMainServer();
            string fallbackHostServer = platformConfig != null
                ? platformConfig.FallbackCdn
                : entry.GetSanitizedFallbackServer();

            IRemoteService remoteService = new DefaultResourceRemoteServices(defaultHostServer, fallbackHostServer);
            FileSystemParameters webNetworkFileSystemParams =
                FileSystemParameters.CreateDefaultWebNetworkFileSystemParameters(remoteService, disableUnityWebCache);

            if (capabilities.IsMiniGame)
            {
                bool allowDevelopmentLoopbackWithPort = UnityEngine.Debug.isDebugBuild;
                MiniGameRemoteUrlValidator.Validate(defaultHostServer, allowDevelopmentLoopbackWithPort);
                if (!string.IsNullOrEmpty(fallbackHostServer))
                {
                    MiniGameRemoteUrlValidator.Validate(fallbackHostServer, allowDevelopmentLoopbackWithPort);
                }

                webNetworkFileSystemParams = capabilities.Platform == ResourceRuntimePlatform.WechatMiniGame
                    ? EF.MiniGame.MiniGameFileSystemFactory.CreateWechat(remoteService, disableUnityWebCache)
                    : EF.MiniGame.MiniGameFileSystemFactory.CreateTiktok(remoteService, disableUnityWebCache);
                return new WebPlayModeOptions
                {
                    WebNetworkFileSystemParameters = webNetworkFileSystemParams
                };
            }

            FileSystemParameters webServerFileSystemParams =
                FileSystemParameters.CreateDefaultWebServerFileSystemParameters(disableUnityWebCache);

            return new WebPlayModeOptions
            {
                WebServerFileSystemParameters = webServerFileSystemParams,
                WebNetworkFileSystemParameters = webNetworkFileSystemParams
            };
        }

        private static void HandleFailureIfNeed(HandleBase handle, string location, string action)
        {
            if (handle == null)
            {
                throw new InvalidOperationException($"{action}失败：句柄为空，定位地址 {location}");
            }

            if (handle.Status == EOperationStatus.Failed)
            {
                string error = string.IsNullOrEmpty(handle.Error) ? "未知错误" : handle.Error;
                handle.Release();
                throw new InvalidOperationException($"{action}失败：{location}，错误信息：{error}");
            }
        }

        private static float CalcProgress(int index, int total, float step)
        {
            if (total <= 0)
            {
                return 1f;
            }

            float baseValue = Mathf.Clamp01((float)index / total);
            float stepValue = Mathf.Clamp01(step) / total;
            return Mathf.Clamp01(baseValue + stepValue);
        }

        #endregion
    }
}

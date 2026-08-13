using System;
using System.Collections.Generic;
using UnityEngine;

namespace EF.Resource
{
    /// <summary>
    /// 资源模块的基础配置，通过 ScriptableObject 控制运行模式与包裹信息。
    /// </summary>
    [CreateAssetMenu(menuName = "EF/资源/资源模块配置", fileName = "EFResourceModeConfig")]
    public sealed class ResourceModeConfig : ScriptableObject
    {
        /// <summary>
        /// Resources.Load 时约定的默认路径。
        /// </summary>
        public const string DefaultResourcesPath = "EFResourceModeConfig";

        [SerializeField]
        [Header("资源加载后端")]
        [InspectorName("启用 YooAssets")]
        [Tooltip("启用时通过 YooAssets 加载资源包；关闭时直接使用 Resources.Load，并跳过 YooAssets 初始化。")]
        private bool _useYooAssets = true;

        [SerializeField]
        private ResourceMode _mode = ResourceMode.EditorSimulate;

        [SerializeField]
        [Header("资源部署")]
        [InspectorName("资源包构建版本")]
        [Tooltip("YooAsset 构建资源包时使用的版本号。运行时仍会从 CDN 请求最新版本文件。")]
        private string _packageVersion = "1.0.0";

        [SerializeField]
        [InspectorName("平台部署策略")]
        [Tooltip("为标准平台、微信小游戏和抖音小游戏分别配置 CDN 地址与运行模式。")]
        private List<ResourceDeploymentPlatformConfig> _platformConfigs = new();

        // 旧版本曾在顶层重复暴露该选项。保留序列化字段以兼容已有资产，
        // 新配置统一从 _platformConfigs 的微信平台条目读取。
        [SerializeField, HideInInspector]
        private WechatMiniGameResourceDeliveryMode _wechatMiniGameResourceDeliveryMode =
            WechatMiniGameResourceDeliveryMode.RemoteUpdate;

        [SerializeField]
        [Tooltip("同时加载 AssetBundle 的最大并发数，合理设置可避免 IO 峰值")]
        private int _bundleLoadingMaxConcurrency = 8;

        [SerializeField]
        [Tooltip("资源包配置列表，至少需要配置一个包裹")]
        private List<ResourcePackageEntry> _packages = new();

        [SerializeField]
        [Tooltip("版本请求、弱联网回退、下载与导入参数")]
        private ResourceUpdateSettings _updateSettings = new();

        /// <summary>
        /// 当前资源运行模式。
        /// </summary>
        public ResourceMode Mode => _mode;

        /// <summary>
        /// 是否使用 YooAssets 资源包后端；关闭时资源由 Resources.Load 直接读取。
        /// </summary>
        public bool UseYooAssets => _useYooAssets;

        /// <summary>
        /// 当前资源包构建版本号。
        /// </summary>
        public string PackageVersion
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_packageVersion))
                {
                    throw new InvalidOperationException("资源包构建版本不能为空。");
                }

                return _packageVersion.Trim();
            }
        }

        /// <summary>
        /// 微信小游戏的资源交付方式。
        /// </summary>
        public WechatMiniGameResourceDeliveryMode WechatMiniGameResourceDeliveryMode =>
            _wechatMiniGameResourceDeliveryMode;

        /// <summary>
        /// AssetBundle 并发加载上限。
        /// </summary>
        public int BundleLoadingMaxConcurrency => Mathf.Clamp(_bundleLoadingMaxConcurrency, 1, 1024);

        /// <summary>
        /// 所有包裹配置。
        /// </summary>
        public IReadOnlyList<ResourcePackageEntry> Packages => _packages;

        /// <summary>
        /// 资源版本更新与文件下载参数。
        /// </summary>
        public ResourceUpdateSettings UpdateSettings => _updateSettings ??= new ResourceUpdateSettings();

        /// <summary>
        /// 判断指定平台是否需要将资源复制到 Player 内置目录。
        /// </summary>
        /// <param name="platform">目标运行时平台。</param>
        /// <returns>需要内置资源时返回 true。</returns>
        public bool RequiresBuiltinPackage(ResourceRuntimePlatform platform)
        {
            ResourceDeploymentPlatformConfig platformConfig = GetPlatformConfig(platform);
            return platformConfig.Mode == ResourceMode.OfflinePlay ||
                   ResourceModeResolver.UsesWechatBuiltinWebServer(
                       platformConfig.WechatMiniGameResourceDeliveryMode,
                       platform);
        }

        /// <summary>
        /// 返回默认包裹配置，若未显式标记则取列表第一项。
        /// </summary>
        public ResourcePackageEntry GetDefaultPackage()
        {
            if (_packages == null || _packages.Count == 0)
            {
                return null;
            }

            foreach (ResourcePackageEntry entry in _packages)
            {
                if (entry != null && entry.IsDefault)
                {
                    return entry;
                }
            }

            return _packages[0];
        }

        /// <summary>
        /// 获取指定运行时平台的 CDN 与播放模式策略。
        /// </summary>
        internal ResourceDeploymentPlatformConfig GetPlatformConfig(ResourceRuntimePlatform platform)
        {
            if (_platformConfigs == null)
            {
                throw new InvalidOperationException("资源部署配置平台列表未初始化。");
            }

            ResourceDeploymentPlatformConfig result = null;
            foreach (ResourceDeploymentPlatformConfig config in _platformConfigs)
            {
                if (config == null || config.Platform != platform)
                {
                    continue;
                }

                if (result != null)
                {
                    throw new InvalidOperationException($"资源部署配置重复声明平台：{platform}。");
                }

                result = config;
            }

            if (result == null)
            {
                throw new InvalidOperationException($"资源部署配置未声明平台：{platform}。");
            }

            return result;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_packages == null)
            {
                _packages = new List<ResourcePackageEntry>();
            }

            if (_platformConfigs == null)
            {
                _platformConfigs = new List<ResourceDeploymentPlatformConfig>();
            }

            EnsureDeploymentPlatformConfigs();

            bool hasDefault = false;
            for (int i = 0; i < _packages.Count; i++)
            {
                ResourcePackageEntry entry = _packages[i];
                if (entry == null)
                {
                    continue;
                }

                if (entry.IsDefault)
                {
                    if (!hasDefault)
                    {
                        hasDefault = true;
                    }
                    else
                    {
                        entry.SetDefault(false);
                    }
                }
            }

            if (!hasDefault && _packages.Count > 0 && _packages[0] != null)
            {
                _packages[0].SetDefault(true);
            }
        }

        /// <summary>
        /// 为新建或升级后的配置补齐三种当前支持的平台策略。
        /// </summary>
        private void EnsureDeploymentPlatformConfigs()
        {
            EnsureDeploymentPlatform(
                ResourceRuntimePlatform.Standard,
                ResourceMode.HostPlay,
                WechatMiniGameResourceDeliveryMode.RemoteUpdate);
            EnsureDeploymentPlatform(
                ResourceRuntimePlatform.WechatMiniGame,
                ResourceMode.WebPlay,
                WechatMiniGameResourceDeliveryMode.RemoteUpdate);
            EnsureDeploymentPlatform(
                ResourceRuntimePlatform.TiktokMiniGame,
                ResourceMode.WebPlay,
                WechatMiniGameResourceDeliveryMode.RemoteUpdate);
        }

        /// <summary>
        /// 缺失时新增指定平台的默认部署策略。
        /// </summary>
        private void EnsureDeploymentPlatform(
            ResourceRuntimePlatform platform,
            ResourceMode mode,
            WechatMiniGameResourceDeliveryMode wechatDeliveryMode)
        {
            foreach (ResourceDeploymentPlatformConfig config in _platformConfigs)
            {
                if (config != null && config.Platform == platform)
                {
                    return;
                }
            }

            _platformConfigs.Add(new ResourceDeploymentPlatformConfig(platform, mode, wechatDeliveryMode));
        }
#endif
    }

    /// <summary>
    /// 单个资源包裹的配置项。
    /// </summary>
    [Serializable]
    public sealed class ResourcePackageEntry
    {
        [SerializeField]
        [Tooltip("资源包名称，需要与 YooAssets 构建时的包裹名称保持一致")]
        private string _packageName = "DefaultPackage";

        [SerializeField]
        [Tooltip("是否作为默认包裹，用于未指定包名的加载请求")]
        private bool _isDefault = true;

        [SerializeField]
        [Tooltip("主资源服地址，例如 https://cdn.example.com/bundles")]
        private string _remoteMainServer = string.Empty;

        [SerializeField]
        [Tooltip("备用资源服地址，可选项，用于主服异常时回退")]
        private string _remoteFallbackServer = string.Empty;

        [SerializeField]
        [Tooltip("在 Web 平台上禁用 Unity 自带缓存，避免部分浏览器的缓存问题")]
        private bool _disableUnityWebCache;

        /// <summary>
        /// 包裹名称。
        /// </summary>
        public string PackageName => string.IsNullOrWhiteSpace(_packageName) ? "DefaultPackage" : _packageName.Trim();

        /// <summary>
        /// 是否默认包裹。
        /// </summary>
        public bool IsDefault => _isDefault;

        /// <summary>
        /// 主资源服地址。
        /// </summary>
        public string RemoteMainServer => _remoteMainServer;

        /// <summary>
        /// 备用资源服地址。
        /// </summary>
        public string RemoteFallbackServer => _remoteFallbackServer;

        /// <summary>
        /// 是否禁用 Unity Web 缓存。
        /// </summary>
        public bool DisableUnityWebCache => _disableUnityWebCache;

        /// <summary>
        /// 归一化后的主资源服地址。
        /// </summary>
        public string GetSanitizedMainServer()
        {
            return SanitizeUrl(_remoteMainServer);
        }

        /// <summary>
        /// 归一化后的备用资源服地址。
        /// </summary>
        public string GetSanitizedFallbackServer()
        {
            return SanitizeUrl(_remoteFallbackServer);
        }

        internal void SetDefault(bool value)
        {
            _isDefault = value;
        }

        private static string SanitizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            string trimmed = url.Trim();
            return trimmed.EndsWith("/", StringComparison.Ordinal) ? trimmed : trimmed + "/";
        }
    }

    /// <summary>
    /// 单个平台的资源部署策略，由 ResourceModeConfig 在 Inspector 中序列化。
    /// </summary>
    [Serializable]
    public sealed class ResourceDeploymentPlatformConfig
    {
        [SerializeField]
        [Tooltip("当前策略生效的平台。")]
        private ResourceRuntimePlatform _platform = ResourceRuntimePlatform.Standard;

        [SerializeField]
        [Tooltip("当前平台使用的 YooAsset 播放模式。")]
        private ResourceMode _mode = ResourceMode.HostPlay;

        [SerializeField]
        [Tooltip("主 CDN 根地址，目录下应直接包含版本、清单和 Bundle 文件。")]
        private string _mainCdn = string.Empty;

        [SerializeField]
        [Tooltip("备用 CDN 根地址，可选。")]
        private string _fallbackCdn = string.Empty;

        [SerializeField]
        [Tooltip("是否禁用该平台上的 Unity Web Cache。")]
        private bool _disableUnityWebCache;

        [SerializeField]
        [Tooltip("仅微信小游戏使用。标准微信转换管线应选择 RemoteUpdate；BuiltinOnly 需要自定义包内 StreamingAssets 读取管线。")]
        private WechatMiniGameResourceDeliveryMode _wechatMiniGameResourceDeliveryMode =
            WechatMiniGameResourceDeliveryMode.RemoteUpdate;

        /// <summary>
        /// 供 Unity 序列化系统创建平台策略实例。
        /// </summary>
        public ResourceDeploymentPlatformConfig()
        {
        }

        /// <summary>
        /// 创建具有指定平台、模式和微信交付方式的部署策略。
        /// </summary>
        public ResourceDeploymentPlatformConfig(
            ResourceRuntimePlatform platform,
            ResourceMode mode,
            WechatMiniGameResourceDeliveryMode wechatMiniGameResourceDeliveryMode)
        {
            _platform = platform;
            _mode = mode;
            _wechatMiniGameResourceDeliveryMode = wechatMiniGameResourceDeliveryMode;
        }

        /// <summary>
        /// 创建包含 CDN 地址的临时平台策略，供运行时校验和测试使用。
        /// </summary>
        internal ResourceDeploymentPlatformConfig(
            ResourceRuntimePlatform platform,
            ResourceMode mode,
            string mainCdn,
            string fallbackCdn,
            bool disableUnityWebCache,
            WechatMiniGameResourceDeliveryMode wechatMiniGameResourceDeliveryMode)
            : this(platform, mode, wechatMiniGameResourceDeliveryMode)
        {
            _mainCdn = mainCdn;
            _fallbackCdn = fallbackCdn;
            _disableUnityWebCache = disableUnityWebCache;
        }

        /// <summary>
        /// 当前策略生效的平台。
        /// </summary>
        public ResourceRuntimePlatform Platform => _platform;

        /// <summary>
        /// 当前平台的 YooAsset 播放模式。
        /// </summary>
        public ResourceMode Mode => _mode;

        /// <summary>
        /// 归一化后的主 CDN 地址。
        /// </summary>
        public string MainCdn => SanitizeUrl(_mainCdn);

        /// <summary>
        /// 归一化后的备用 CDN 地址。
        /// </summary>
        public string FallbackCdn => SanitizeUrl(_fallbackCdn);

        /// <summary>
        /// 是否禁用 Unity Web Cache。
        /// </summary>
        public bool DisableUnityWebCache => _disableUnityWebCache;

        /// <summary>
        /// 微信小游戏资源交付方式。
        /// </summary>
        public WechatMiniGameResourceDeliveryMode WechatMiniGameResourceDeliveryMode =>
            _wechatMiniGameResourceDeliveryMode;

        /// <summary>
        /// 验证远端加载模式已经配置主 CDN 地址。
        /// </summary>
        internal void ValidateRemoteCdn()
        {
            bool usesWechatBuiltinResources =
                _platform == ResourceRuntimePlatform.WechatMiniGame &&
                _wechatMiniGameResourceDeliveryMode == WechatMiniGameResourceDeliveryMode.BuiltinOnly;
            bool requiresRemoteCdn = !usesWechatBuiltinResources &&
                                     (_mode == ResourceMode.HostPlay || _mode == ResourceMode.WebPlay);
            if (requiresRemoteCdn && string.IsNullOrWhiteSpace(_mainCdn))
            {
                throw new InvalidOperationException($"平台 {_platform} 的远端资源模式必须配置主 CDN 地址。");
            }
        }

        private static string SanitizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            string trimmed = url.Trim();
            return trimmed.EndsWith("/", StringComparison.Ordinal) ? trimmed : trimmed + "/";
        }
    }
}

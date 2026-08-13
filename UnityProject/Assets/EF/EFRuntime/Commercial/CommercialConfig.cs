using System;
using System.Collections.Generic;
using UnityEngine;

namespace EF.Commercial
{
    /// <summary>
    /// 商业化平台与广告位配置。该资产只保存 ID 和开关，禁止存放支付密钥或签名私钥。
    /// </summary>
    [CreateAssetMenu(menuName = "EF/商业化/商业化配置", fileName = "CommercialConfig")]
    public sealed class CommercialConfig : ScriptableObject
    {
        /// <summary>
        /// Resources.Load 使用的默认资源路径。
        /// </summary>
        public const string DefaultResourcesPath = "CommercialConfig";

        [SerializeField]
        [Tooltip("各运行平台的商业化开关、广告位与商品映射。")]
        private List<CommercialPlatformConfig> _platforms = new();

        /// <summary>
        /// 获取指定平台的商业化配置。
        /// </summary>
        /// <param name="platform">目标平台。</param>
        /// <returns>配置不存在时返回 null。</returns>
        public CommercialPlatformConfig GetPlatformConfig(CommercialPlatform platform)
        {
            if (_platforms == null)
            {
                return null;
            }

            foreach (CommercialPlatformConfig config in _platforms)
            {
                if (config != null && config.Platform == platform)
                {
                    return config;
                }
            }

            return null;
        }

#if UNITY_EDITOR
        private void Reset()
        {
            EnsurePlatformConfigs();
        }

        private void OnValidate()
        {
            EnsurePlatformConfigs();
        }

        /// <summary>
        /// 保证 Inspector 新建资产后可直接看到所有当前支持的平台。
        /// </summary>
        private void EnsurePlatformConfigs()
        {
            _platforms ??= new List<CommercialPlatformConfig>();
            foreach (CommercialPlatform platform in Enum.GetValues(typeof(CommercialPlatform)))
            {
                if (GetPlatformConfig(platform) != null)
                {
                    continue;
                }

                _platforms.Add(new CommercialPlatformConfig(platform));
            }
        }
#endif
    }

    /// <summary>
    /// 单个平台的商业化开关和渠道映射。
    /// </summary>
    [Serializable]
    public sealed class CommercialPlatformConfig
    {
        [SerializeField]
        private CommercialPlatform _platform;

        [SerializeField]
        [Tooltip("关闭后该平台所有广告、购买和埋点调用都会降级为不可用。")]
        private bool _enabled;

        [SerializeField]
        [Tooltip("广告位映射。placementId 由游戏业务传入，广告单元 ID 由渠道后台配置。")]
        private List<CommercialPlacementConfig> _placements = new();

        [SerializeField]
        [Tooltip("商品 ID 到渠道商品 ID 的映射。支付签名仍必须由服务端生成。")]
        private List<CommercialProductConfig> _products = new();

        [SerializeField]
        [Range(30, 120)]
        [Tooltip("Banner 自动刷新的最小间隔，微信渠道要求不少于 30 秒。")]
        private int _bannerRefreshIntervalSeconds = 30;

        /// <summary>
        /// 供 Unity 序列化创建实例。
        /// </summary>
        public CommercialPlatformConfig()
        {
        }

        /// <summary>
        /// 创建指定平台的默认配置。
        /// </summary>
        public CommercialPlatformConfig(CommercialPlatform platform)
        {
            _platform = platform;
        }

        /// <summary>
        /// 配置所属的平台。
        /// </summary>
        public CommercialPlatform Platform => _platform;

        /// <summary>
        /// 平台商业化总开关。
        /// </summary>
        public bool Enabled => _enabled;

        /// <summary>
        /// Banner 刷新间隔。
        /// </summary>
        public int BannerRefreshIntervalSeconds => Mathf.Max(30, _bannerRefreshIntervalSeconds);

        /// <summary>
        /// 是否至少存在一个激励视频广告位。
        /// </summary>
        public bool HasRewardedVideo => HasPlacementId(config => config.RewardedVideoAdUnitId);

        /// <summary>
        /// 是否至少存在一个插屏广告位。
        /// </summary>
        public bool HasInterstitial => HasPlacementId(config => config.InterstitialAdUnitId);

        /// <summary>
        /// 是否至少存在一个 Banner 广告位。
        /// </summary>
        public bool HasBanner => HasPlacementId(config => config.BannerAdUnitId);

        /// <summary>
        /// 查找指定广告位。
        /// </summary>
        /// <param name="placementId">广告位标识。</param>
        /// <returns>未配置时返回 null。</returns>
        public CommercialPlacementConfig GetPlacement(string placementId)
        {
            if (string.IsNullOrWhiteSpace(placementId) || _placements == null)
            {
                return null;
            }

            foreach (CommercialPlacementConfig config in _placements)
            {
                if (config != null && string.Equals(config.PlacementId, placementId, StringComparison.Ordinal))
                {
                    return config;
                }
            }

            return null;
        }

        /// <summary>
        /// 查找指定游戏商品的渠道映射。
        /// </summary>
        /// <param name="productId">游戏内商品标识。</param>
        /// <returns>未配置时返回 null。</returns>
        public CommercialProductConfig GetProduct(string productId)
        {
            if (string.IsNullOrWhiteSpace(productId) || _products == null)
            {
                return null;
            }

            foreach (CommercialProductConfig config in _products)
            {
                if (config != null && string.Equals(config.ProductId, productId, StringComparison.Ordinal))
                {
                    return config;
                }
            }

            return null;
        }

        private bool HasPlacementId(Func<CommercialPlacementConfig, string> selector)
        {
            if (_placements == null)
            {
                return false;
            }

            foreach (CommercialPlacementConfig config in _placements)
            {
                if (config != null && !string.IsNullOrWhiteSpace(selector(config)))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// 一个游戏广告位在当前渠道对应的广告单元 ID。
    /// </summary>
    [Serializable]
    public sealed class CommercialPlacementConfig
    {
        [SerializeField]
        [Tooltip("业务侧使用的稳定广告位标识，例如 revive、game_over。")]
        private string _placementId = string.Empty;

        [SerializeField]
        [Tooltip("激励视频广告单元 ID。")]
        private string _rewardedVideoAdUnitId = string.Empty;

        [SerializeField]
        [Tooltip("插屏广告单元 ID。")]
        private string _interstitialAdUnitId = string.Empty;

        [SerializeField]
        [Tooltip("Banner 广告单元 ID。")]
        private string _bannerAdUnitId = string.Empty;

        /// <summary>
        /// 业务广告位标识。
        /// </summary>
        public string PlacementId => _placementId;

        /// <summary>
        /// 激励视频广告单元 ID。
        /// </summary>
        public string RewardedVideoAdUnitId => _rewardedVideoAdUnitId;

        /// <summary>
        /// 插屏广告单元 ID。
        /// </summary>
        public string InterstitialAdUnitId => _interstitialAdUnitId;

        /// <summary>
        /// Banner 广告单元 ID。
        /// </summary>
        public string BannerAdUnitId => _bannerAdUnitId;
    }

    /// <summary>
    /// 游戏商品到渠道商品的映射，不保存订单签名或支付密钥。
    /// </summary>
    [Serializable]
    public sealed class CommercialProductConfig
    {
        [SerializeField]
        private string _productId = string.Empty;

        [SerializeField]
        private string _providerProductId = string.Empty;

        /// <summary>
        /// 游戏内商品标识。
        /// </summary>
        public string ProductId => _productId;

        /// <summary>
        /// 渠道后台的商品标识。
        /// </summary>
        public string ProviderProductId => _providerProductId;
    }
}

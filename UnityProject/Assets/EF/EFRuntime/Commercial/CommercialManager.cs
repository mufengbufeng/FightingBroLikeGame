using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using EF.Common;
using UnityEngine;

namespace EF.Commercial
{
    /// <summary>
    /// EF 模块化商业化服务，统一向游戏逻辑暴露广告、购买和埋点能力。
    /// </summary>
    public sealed class CommercialManager : AEFManager, ICommercialService
    {
        private ICommercialProvider _provider;

        /// <summary>
        /// 从 Resources 中加载商业化配置并创建当前平台 Provider。
        /// </summary>
        public CommercialManager()
            : this(CommercialProviderFactory.Create(LoadConfig()))
        {
        }

        /// <summary>
        /// 使用指定 Provider 创建服务，主要用于渠道扩展和自动化测试。
        /// </summary>
        /// <param name="provider">平台 Provider。</param>
        public CommercialManager(ICommercialProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <inheritdoc />
        public CommercialPlatform Platform => Provider.Platform;

        /// <inheritdoc />
        public CommercialCapabilities Capabilities => Provider.Capabilities;

        /// <inheritdoc />
        public UniTask<CommercialOperationResult> ShowRewardedVideoAsync(string placementId)
        {
            if (string.IsNullOrWhiteSpace(placementId))
            {
                return UniTask.FromResult(CommercialOperationResult.InvalidRequest("广告位标识不能为空。"));
            }

            return Provider.ShowRewardedVideoAsync(placementId);
        }

        /// <inheritdoc />
        public UniTask<CommercialOperationResult> ShowInterstitialAsync(string placementId)
        {
            if (string.IsNullOrWhiteSpace(placementId))
            {
                return UniTask.FromResult(CommercialOperationResult.InvalidRequest("广告位标识不能为空。"));
            }

            return Provider.ShowInterstitialAsync(placementId);
        }

        /// <inheritdoc />
        public void ShowBanner(string placementId)
        {
            if (!string.IsNullOrWhiteSpace(placementId))
            {
                Provider.ShowBanner(placementId);
            }
        }

        /// <inheritdoc />
        public void HideBanner()
        {
            Provider.HideBanner();
        }

        /// <inheritdoc />
        public UniTask<CommercialPurchaseResult> PurchaseAsync(CommercialPurchaseRequest request)
        {
            if (!request.IsValid)
            {
                return UniTask.FromResult(CommercialPurchaseResult.InvalidRequest(
                    request.OrderId,
                    "购买请求缺少商品、订单号或有效数量。"));
            }

            return Provider.PurchaseAsync(request);
        }

        /// <inheritdoc />
        public void TrackEvent(string eventName, IReadOnlyDictionary<string, string> parameters = null)
        {
            if (!string.IsNullOrWhiteSpace(eventName))
            {
                Provider.TrackEvent(eventName, parameters);
            }
        }

        /// <inheritdoc />
        public override void Shutdown()
        {
            ICommercialProvider provider = _provider;
            _provider = new NullCommercialProvider(CommercialPlatformResolver.Current, "商业化服务已关闭。");
            provider?.Shutdown();
        }

        private ICommercialProvider Provider => _provider ??=
            new NullCommercialProvider(CommercialPlatformResolver.Current, "商业化服务未初始化。");

        /// <summary>
        /// 加载资源系统初始化前即可使用的可选配置。缺少配置时仍返回安全的空 Provider。
        /// </summary>
        private static CommercialConfig LoadConfig()
        {
            return Resources.Load<CommercialConfig>(CommercialConfig.DefaultResourcesPath);
        }
    }
}

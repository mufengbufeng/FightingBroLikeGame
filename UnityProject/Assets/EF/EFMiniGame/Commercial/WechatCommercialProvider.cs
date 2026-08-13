using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using EF.Commercial;
#if (WEIXINMINIGAME || UNITY_WEBGL) && !UNITY_EDITOR
using WeChatWASM;
#endif

namespace EF.Monetization.Wechat
{
    /// <summary>
    /// 微信小游戏商业化适配器。广告和埋点直接桥接 SDK，支付始终等待服务端签名与验签接入。
    /// </summary>
    public sealed class WechatCommercialProvider : ICommercialProvider
    {
        private const int BannerHeight = 100;
        private readonly CommercialPlatformConfig _config;
        private readonly CommercialCapabilities _capabilities;

#if (WEIXINMINIGAME || UNITY_WEBGL) && !UNITY_EDITOR
        private WXBannerAd _activeBannerAd;
#endif

        /// <summary>
        /// 使用微信平台配置创建 Provider。
        /// </summary>
        /// <param name="config">微信平台商业化配置。</param>
        public WechatCommercialProvider(CommercialPlatformConfig config)
        {
            _config = config;
            _capabilities = BuildCapabilities(config);
        }

        /// <inheritdoc />
        public CommercialPlatform Platform => CommercialPlatform.WechatMiniGame;

        /// <inheritdoc />
        public CommercialCapabilities Capabilities => _capabilities;

        /// <inheritdoc />
        public UniTask<CommercialOperationResult> ShowRewardedVideoAsync(string placementId)
        {
            if (!TryGetPlacement(placementId, out CommercialPlacementConfig placement) ||
                string.IsNullOrWhiteSpace(placement.RewardedVideoAdUnitId))
            {
                return UniTask.FromResult(CommercialOperationResult.Unavailable("未配置微信激励视频广告位。"));
            }

#if (WEIXINMINIGAME || UNITY_WEBGL) && !UNITY_EDITOR
            return ShowRewardedVideoInternal(placement.RewardedVideoAdUnitId);
#else
            return UniTask.FromResult(CommercialOperationResult.Unavailable("当前不是微信小游戏运行环境。"));
#endif
        }

        /// <inheritdoc />
        public UniTask<CommercialOperationResult> ShowInterstitialAsync(string placementId)
        {
            if (!TryGetPlacement(placementId, out CommercialPlacementConfig placement) ||
                string.IsNullOrWhiteSpace(placement.InterstitialAdUnitId))
            {
                return UniTask.FromResult(CommercialOperationResult.Unavailable("未配置微信插屏广告位。"));
            }

#if (WEIXINMINIGAME || UNITY_WEBGL) && !UNITY_EDITOR
            return ShowInterstitialInternal(placement.InterstitialAdUnitId);
#else
            return UniTask.FromResult(CommercialOperationResult.Unavailable("当前不是微信小游戏运行环境。"));
#endif
        }

        /// <inheritdoc />
        public void ShowBanner(string placementId)
        {
            if (!TryGetPlacement(placementId, out CommercialPlacementConfig placement) ||
                string.IsNullOrWhiteSpace(placement.BannerAdUnitId))
            {
                return;
            }

#if (WEIXINMINIGAME || UNITY_WEBGL) && !UNITY_EDITOR
            HideBanner();
            try
            {
                WXBannerAd banner = WX.CreateFixedBottomMiddleBannerAd(
                    placement.BannerAdUnitId,
                    _config.BannerRefreshIntervalSeconds,
                    BannerHeight);
                _activeBannerAd = banner;
                banner.OnError(_ =>
                {
                    if (ReferenceEquals(_activeBannerAd, banner))
                    {
                        HideBanner();
                    }
                });
                banner.OnLoad(_ =>
                {
                    if (ReferenceEquals(_activeBannerAd, banner))
                    {
                        banner.Show(null, null);
                    }
                });
            }
            catch (Exception)
            {
                HideBanner();
            }
#endif
        }

        /// <inheritdoc />
        public void HideBanner()
        {
#if (WEIXINMINIGAME || UNITY_WEBGL) && !UNITY_EDITOR
            WXBannerAd banner = _activeBannerAd;
            _activeBannerAd = null;
            if (banner == null)
            {
                return;
            }

            try
            {
                banner.Hide();
                banner.Destroy();
            }
            catch (Exception)
            {
            }
#endif
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

            return UniTask.FromResult(CommercialPurchaseResult.Unavailable(
                request.OrderId,
                "微信支付尚未接入服务端签名和验签流程，客户端拒绝直接发起支付。"));
        }

        /// <inheritdoc />
        public void TrackEvent(string eventName, IReadOnlyDictionary<string, string> parameters)
        {
            if (_config == null || !_config.Enabled || string.IsNullOrWhiteSpace(eventName))
            {
                return;
            }

#if (WEIXINMINIGAME || UNITY_WEBGL) && !UNITY_EDITOR
            try
            {
                Dictionary<string, string> data = parameters != null
                    ? new Dictionary<string, string>(parameters)
                    : new Dictionary<string, string>();
                WX.ReportEvent(eventName, data);
            }
            catch (Exception)
            {
                // 埋点失败不能阻断游戏流程。
            }
#endif
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            HideBanner();
        }

#if (WEIXINMINIGAME || UNITY_WEBGL) && !UNITY_EDITOR
        /// <summary>
        /// 等待微信激励视频关闭，并严格以 isEnded 决定奖励资格。
        /// </summary>
        private static UniTask<CommercialOperationResult> ShowRewardedVideoInternal(string adUnitId)
        {
            var completion = new UniTaskCompletionSource<CommercialOperationResult>();
            WXRewardedVideoAd ad = null;
            Action<WXRewardedVideoAdOnCloseResponse> closeCallback = null;
            Action<WXADErrorResponse> errorCallback = null;

            void Complete(CommercialOperationResult result)
            {
                if (!completion.TrySetResult(result))
                {
                    return;
                }

                if (ad == null)
                {
                    return;
                }

                ad.OffClose(closeCallback);
                ad.OffError(errorCallback);
                ad.Destroy();
            }

            try
            {
                ad = WX.CreateRewardedVideoAd(new WXCreateRewardedVideoAdParam
                {
                    adUnitId = adUnitId,
                });
                closeCallback = response =>
                {
                    Complete(response != null && response.isEnded
                        ? CommercialOperationResult.Succeeded()
                        : CommercialOperationResult.Cancelled("用户未完整观看激励视频。"));
                };
                errorCallback = response => Complete(CommercialOperationResult.Failed(
                    string.IsNullOrWhiteSpace(response.errMsg) ? "微信激励视频广告加载失败。" : response.errMsg));
                ad.OnClose(closeCallback);
                ad.OnError(errorCallback);
                ad.Show(
                    _ => { },
                    response => Complete(CommercialOperationResult.Failed(
                        string.IsNullOrWhiteSpace(response.errMsg) ? "微信激励视频广告展示失败。" : response.errMsg)));
            }
            catch (Exception exception)
            {
                Complete(CommercialOperationResult.Failed("微信激励视频广告调用失败：" + exception.Message));
            }

            return completion.Task;
        }

        /// <summary>
        /// 等待微信插屏广告关闭后再返回成功结果。
        /// </summary>
        private static UniTask<CommercialOperationResult> ShowInterstitialInternal(string adUnitId)
        {
            var completion = new UniTaskCompletionSource<CommercialOperationResult>();
            WXInterstitialAd ad = null;
            Action closeCallback = null;
            Action<WXADErrorResponse> errorCallback = null;

            void Complete(CommercialOperationResult result)
            {
                if (!completion.TrySetResult(result))
                {
                    return;
                }

                if (ad == null)
                {
                    return;
                }

                ad.OffClose(closeCallback);
                ad.OffError(errorCallback);
                ad.Destroy();
            }

            try
            {
                ad = WX.CreateInterstitialAd(new WXCreateInterstitialAdParam
                {
                    adUnitId = adUnitId,
                });
                closeCallback = () => Complete(CommercialOperationResult.Succeeded());
                errorCallback = response => Complete(CommercialOperationResult.Failed(
                    string.IsNullOrWhiteSpace(response.errMsg) ? "微信插屏广告加载失败。" : response.errMsg));
                ad.OnClose(closeCallback);
                ad.OnError(errorCallback);
                ad.Show(
                    _ => { },
                    response => Complete(CommercialOperationResult.Failed(
                        string.IsNullOrWhiteSpace(response.errMsg) ? "微信插屏广告展示失败。" : response.errMsg)));
            }
            catch (Exception exception)
            {
                Complete(CommercialOperationResult.Failed("微信插屏广告调用失败：" + exception.Message));
            }

            return completion.Task;
        }
#endif

        /// <summary>
        /// 获取启用配置中的广告位。
        /// </summary>
        private bool TryGetPlacement(string placementId, out CommercialPlacementConfig placement)
        {
            placement = _config != null && _config.Enabled
                ? _config.GetPlacement(placementId)
                : null;
            return placement != null;
        }

        /// <summary>
        /// 根据实际配置暴露能力，未填写广告单元 ID 时不会对业务声明可用。
        /// </summary>
        private static CommercialCapabilities BuildCapabilities(CommercialPlatformConfig config)
        {
            if (config == null || !config.Enabled)
            {
                return new CommercialCapabilities(CommercialCapability.None);
            }

            CommercialCapability capabilities = CommercialCapability.Analytics;
            if (config.HasRewardedVideo)
            {
                capabilities |= CommercialCapability.RewardedVideo;
            }

            if (config.HasInterstitial)
            {
                capabilities |= CommercialCapability.Interstitial;
            }

            if (config.HasBanner)
            {
                capabilities |= CommercialCapability.Banner;
            }

            return new CommercialCapabilities(capabilities);
        }
    }
}

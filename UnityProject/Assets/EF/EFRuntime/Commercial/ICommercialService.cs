using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using EF.Common;

namespace EF.Commercial
{
    /// <summary>
    /// 为游戏逻辑提供平台无关的广告、购买和埋点能力。
    /// </summary>
    public interface ICommercialService : IEFManager
    {
        /// <summary>
        /// 当前运行的平台。
        /// </summary>
        CommercialPlatform Platform { get; }

        /// <summary>
        /// 当前平台已启用的能力。
        /// </summary>
        CommercialCapabilities Capabilities { get; }

        /// <summary>
        /// 展示指定广告位的激励视频，并以完整观看结果决定是否可发奖励。
        /// </summary>
        /// <param name="placementId">配置中的广告位标识。</param>
        /// <returns>广告展示结果。</returns>
        UniTask<CommercialOperationResult> ShowRewardedVideoAsync(string placementId);

        /// <summary>
        /// 展示指定广告位的插屏广告。
        /// </summary>
        /// <param name="placementId">配置中的广告位标识。</param>
        /// <returns>广告展示结果。</returns>
        UniTask<CommercialOperationResult> ShowInterstitialAsync(string placementId);

        /// <summary>
        /// 显示指定广告位的 Banner 广告。
        /// </summary>
        /// <param name="placementId">配置中的广告位标识。</param>
        void ShowBanner(string placementId);

        /// <summary>
        /// 隐藏并释放当前显示的 Banner 广告。
        /// </summary>
        void HideBanner();

        /// <summary>
        /// 发起由服务端签名的支付请求。客户端不得据此直接发货。
        /// </summary>
        /// <param name="request">服务端生成的支付请求。</param>
        /// <returns>支付客户端状态。</returns>
        UniTask<CommercialPurchaseResult> PurchaseAsync(CommercialPurchaseRequest request);

        /// <summary>
        /// 上报不影响游戏流程的商业化分析事件。
        /// </summary>
        /// <param name="eventName">已在平台后台登记的事件名称。</param>
        /// <param name="parameters">事件参数。</param>
        void TrackEvent(string eventName, IReadOnlyDictionary<string, string> parameters = null);
    }

    /// <summary>
    /// 平台 SDK 适配器的扩展点，第三方渠道只需实现该接口即可接入商业化服务。
    /// </summary>
    public interface ICommercialProvider
    {
        /// <summary>
        /// Provider 所属的平台。
        /// </summary>
        CommercialPlatform Platform { get; }

        /// <summary>
        /// Provider 当前可用的能力。
        /// </summary>
        CommercialCapabilities Capabilities { get; }

        /// <summary>
        /// 展示激励视频广告。
        /// </summary>
        UniTask<CommercialOperationResult> ShowRewardedVideoAsync(string placementId);

        /// <summary>
        /// 展示插屏广告。
        /// </summary>
        UniTask<CommercialOperationResult> ShowInterstitialAsync(string placementId);

        /// <summary>
        /// 显示 Banner 广告。
        /// </summary>
        void ShowBanner(string placementId);

        /// <summary>
        /// 隐藏 Banner 广告。
        /// </summary>
        void HideBanner();

        /// <summary>
        /// 发起支付请求。
        /// </summary>
        UniTask<CommercialPurchaseResult> PurchaseAsync(CommercialPurchaseRequest request);

        /// <summary>
        /// 上报分析事件。
        /// </summary>
        void TrackEvent(string eventName, IReadOnlyDictionary<string, string> parameters);

        /// <summary>
        /// 释放平台 SDK 创建的商业化对象。
        /// </summary>
        void Shutdown();
    }
}

using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace EF.Commercial
{
    /// <summary>
    /// 未接入渠道 SDK 或未启用商业化时使用的安全降级 Provider。
    /// </summary>
    public sealed class NullCommercialProvider : ICommercialProvider
    {
        private readonly CommercialPlatform _platform;
        private readonly string _reason;

        /// <summary>
        /// 创建指定平台的降级 Provider。
        /// </summary>
        /// <param name="platform">当前平台。</param>
        /// <param name="reason">不可用原因。</param>
        public NullCommercialProvider(CommercialPlatform platform, string reason)
        {
            _platform = platform;
            _reason = string.IsNullOrWhiteSpace(reason) ? "当前平台未接入商业化服务。" : reason;
        }

        /// <inheritdoc />
        public CommercialPlatform Platform => _platform;

        /// <inheritdoc />
        public CommercialCapabilities Capabilities => new(CommercialCapability.None);

        /// <inheritdoc />
        public UniTask<CommercialOperationResult> ShowRewardedVideoAsync(string placementId)
        {
            return UniTask.FromResult(CommercialOperationResult.Unavailable(_reason));
        }

        /// <inheritdoc />
        public UniTask<CommercialOperationResult> ShowInterstitialAsync(string placementId)
        {
            return UniTask.FromResult(CommercialOperationResult.Unavailable(_reason));
        }

        /// <inheritdoc />
        public void ShowBanner(string placementId)
        {
        }

        /// <inheritdoc />
        public void HideBanner()
        {
        }

        /// <inheritdoc />
        public UniTask<CommercialPurchaseResult> PurchaseAsync(CommercialPurchaseRequest request)
        {
            return UniTask.FromResult(CommercialPurchaseResult.Unavailable(request.OrderId, _reason));
        }

        /// <inheritdoc />
        public void TrackEvent(string eventName, IReadOnlyDictionary<string, string> parameters)
        {
        }

        /// <inheritdoc />
        public void Shutdown()
        {
        }
    }
}

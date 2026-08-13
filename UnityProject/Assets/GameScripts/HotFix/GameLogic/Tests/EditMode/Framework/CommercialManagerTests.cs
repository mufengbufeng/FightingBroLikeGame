using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using EF.Commercial;
using NUnit.Framework;

namespace GameLogic.Tests.EditMode.Framework
{
    /// <summary>
    /// 验证商业化服务的跨平台 Provider 委派和安全降级行为。
    /// </summary>
    [TestFixture]
    public sealed class CommercialManagerTests
    {
        /// <summary>
        /// 服务应原样透传 Provider 的广告、埋点与生命周期调用。
        /// </summary>
        [Test]
        public void Service_DelegatesToProvider()
        {
            var provider = new TestCommercialProvider();
            var manager = new CommercialManager(provider);

            CommercialOperationResult result = manager.ShowRewardedVideoAsync("revive")
                .GetAwaiter()
                .GetResult();
            manager.ShowBanner("home");
            manager.TrackEvent("game_end", new Dictionary<string, string> { { "score", "100" } });
            manager.Shutdown();

            Assert.AreEqual(CommercialOperationStatus.Succeeded, result.Status);
            Assert.IsTrue(provider.ShowRewardedCalled);
            Assert.IsTrue(provider.ShowBannerCalled);
            Assert.IsTrue(provider.TrackEventCalled);
            Assert.IsTrue(provider.ShutdownCalled);
        }

        /// <summary>
        /// 缺少订单关键字段时服务必须在进入 Provider 前拒绝请求。
        /// </summary>
        [Test]
        public void Purchase_InvalidRequest_IsRejectedBeforeProvider()
        {
            var provider = new TestCommercialProvider();
            var manager = new CommercialManager(provider);

            CommercialPurchaseResult result = manager.PurchaseAsync(
                    new CommercialPurchaseRequest("", "", ""))
                .GetAwaiter()
                .GetResult();

            Assert.AreEqual(CommercialOperationStatus.InvalidRequest, result.Status);
            Assert.IsFalse(provider.PurchaseCalled);
        }

        /// <summary>
        /// 用于验证服务委派行为的最小 Provider。
        /// </summary>
        private sealed class TestCommercialProvider : ICommercialProvider
        {
            /// <summary>
            /// 是否调用过激励广告。
            /// </summary>
            public bool ShowRewardedCalled { get; private set; }

            /// <summary>
            /// 是否调用过 Banner 广告。
            /// </summary>
            public bool ShowBannerCalled { get; private set; }

            /// <summary>
            /// 是否调用过支付。
            /// </summary>
            public bool PurchaseCalled { get; private set; }

            /// <summary>
            /// 是否调用过埋点。
            /// </summary>
            public bool TrackEventCalled { get; private set; }

            /// <summary>
            /// 是否调用过关闭。
            /// </summary>
            public bool ShutdownCalled { get; private set; }

            /// <inheritdoc />
            public CommercialPlatform Platform => CommercialPlatform.Editor;

            /// <inheritdoc />
            public CommercialCapabilities Capabilities => new(CommercialCapability.RewardedVideo);

            /// <inheritdoc />
            public UniTask<CommercialOperationResult> ShowRewardedVideoAsync(string placementId)
            {
                ShowRewardedCalled = true;
                return UniTask.FromResult(CommercialOperationResult.Succeeded());
            }

            /// <inheritdoc />
            public UniTask<CommercialOperationResult> ShowInterstitialAsync(string placementId)
            {
                return UniTask.FromResult(CommercialOperationResult.Unavailable("未测试插屏广告。"));
            }

            /// <inheritdoc />
            public void ShowBanner(string placementId)
            {
                ShowBannerCalled = true;
            }

            /// <inheritdoc />
            public void HideBanner()
            {
            }

            /// <inheritdoc />
            public UniTask<CommercialPurchaseResult> PurchaseAsync(CommercialPurchaseRequest request)
            {
                PurchaseCalled = true;
                return UniTask.FromResult(CommercialPurchaseResult.PendingServerVerification(request.OrderId));
            }

            /// <inheritdoc />
            public void TrackEvent(string eventName, IReadOnlyDictionary<string, string> parameters)
            {
                TrackEventCalled = true;
            }

            /// <inheritdoc />
            public void Shutdown()
            {
                ShutdownCalled = true;
            }
        }
    }
}

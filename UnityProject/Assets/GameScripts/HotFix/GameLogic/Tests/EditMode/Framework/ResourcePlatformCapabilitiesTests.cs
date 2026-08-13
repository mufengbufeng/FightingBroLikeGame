using System;
using EF.Resource;
using NUnit.Framework;

namespace GameLogic.Tests.EditMode.Framework
{
    /// <summary>
    /// 验证小游戏资源能力与远端 URL 约束。
    /// </summary>
    [TestFixture]
    public sealed class ResourcePlatformCapabilitiesTests
    {
        /// <summary>
        /// 微信和抖音小游戏都不允许 YooAsset 同步加载及批量下载器。
        /// </summary>
        [TestCase(ResourceRuntimePlatform.WechatMiniGame)]
        [TestCase(ResourceRuntimePlatform.TiktokMiniGame)]
        public void MiniGamePlatform_DisablesSyncLoadingAndDownloader(ResourceRuntimePlatform platform)
        {
            ResourceRuntimeCapabilities capabilities = ResourceRuntimeCapabilities.ForPlatform(platform);

            Assert.IsFalse(capabilities.SupportsSynchronousLoading);
            Assert.IsFalse(capabilities.SupportsResourceDownloader);
        }

        /// <summary>
        /// 普通 Unity 平台应继续支持现有同步加载和批量下载行为。
        /// </summary>
        [Test]
        public void StandardPlatform_PreservesExistingCapabilities()
        {
            ResourceRuntimeCapabilities capabilities =
                ResourceRuntimeCapabilities.ForPlatform(ResourceRuntimePlatform.Standard);

            Assert.IsTrue(capabilities.SupportsSynchronousLoading);
            Assert.IsTrue(capabilities.SupportsResourceDownloader);
        }

        /// <summary>
        /// 远端服务拼接文件路径时不得生成小游戏 SDK 无法处理的双斜杠。
        /// </summary>
        [Test]
        public void RemoteService_LeadingSeparators_AreNormalized()
        {
            var service = new DefaultResourceRemoteServices(
                "https://cdn.example.com/content/",
                "https://fallback.example.com/content/");

            var urls = service.GetRemoteUrls("\\bundles/hero.bundle");

            Assert.AreEqual("https://cdn.example.com/content/bundles/hero.bundle", urls[0]);
            Assert.AreEqual("https://fallback.example.com/content/bundles/hero.bundle", urls[1]);
        }

        /// <summary>
        /// 小游戏地址包含端口或反斜杠时应在初始化阶段给出明确错误。
        /// </summary>
        [TestCase("http://cdn.example.com:80/content/")]
        [TestCase("https://cdn.example.com:443/content/")]
        [TestCase("https://cdn.example.com:8443/content/")]
        [TestCase("https://cdn.example.com/content\\bundles/")]
        public void MiniGameRemoteUrl_InvalidFormat_Throws(string remoteUrl)
        {
            Assert.Throws<InvalidOperationException>(() => MiniGameRemoteUrlValidator.Validate(remoteUrl));
        }
    }
}

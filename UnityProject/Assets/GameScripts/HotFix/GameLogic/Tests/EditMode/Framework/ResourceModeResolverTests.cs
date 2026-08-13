using EF.Resource;
using NUnit.Framework;

namespace GameLogic.Tests.EditMode.Framework
{
    /// <summary>
    /// 验证微信小游戏资源交付开关与实际 YooAsset 运行模式的映射。
    /// </summary>
    [TestFixture]
    public sealed class ResourceModeResolverTests
    {
        /// <summary>
        /// 微信小游戏必须使用 WebPlay，避免 YooAsset OfflinePlay 在 WebGL 上初始化失败。
        /// </summary>
        [Test]
        public void 微信小游戏_Player模式始终使用WebPlay()
        {
            ResourceMode mode = ResourceModeResolver.ResolvePlayerMode(
                ResourceMode.OfflinePlay,
                ResourceRuntimePlatform.WechatMiniGame);

            Assert.AreEqual(ResourceMode.WebPlay, mode);
        }

        /// <summary>
        /// 开启内置资源交付时，应选择本地 WebServer 文件系统。
        /// </summary>
        [Test]
        public void 微信小游戏内置资源模式_选择WebServer文件系统()
        {
            bool usesBuiltinWebServer = ResourceModeResolver.UsesWechatBuiltinWebServer(
                WechatMiniGameResourceDeliveryMode.BuiltinOnly,
                ResourceRuntimePlatform.WechatMiniGame);

            Assert.IsTrue(usesBuiltinWebServer);
        }

        /// <summary>
        /// 选择远端更新时，应保留微信 CDN 文件系统路径。
        /// </summary>
        [Test]
        public void 微信小游戏远端更新模式_不选择内置WebServer文件系统()
        {
            bool usesBuiltinWebServer = ResourceModeResolver.UsesWechatBuiltinWebServer(
                WechatMiniGameResourceDeliveryMode.RemoteUpdate,
                ResourceRuntimePlatform.WechatMiniGame);

            Assert.IsFalse(usesBuiltinWebServer);
        }

        /// <summary>
        /// 非微信平台保持资源配置声明的运行模式。
        /// </summary>
        [Test]
        public void 非微信平台_保持配置运行模式()
        {
            ResourceMode mode = ResourceModeResolver.ResolvePlayerMode(
                ResourceMode.HostPlay,
                ResourceRuntimePlatform.Standard);

            Assert.AreEqual(ResourceMode.HostPlay, mode);
        }
    }
}

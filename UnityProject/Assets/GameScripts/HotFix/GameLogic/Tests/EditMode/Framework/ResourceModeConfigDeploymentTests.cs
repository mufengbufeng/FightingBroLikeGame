using System;
using EF.Resource;
using NUnit.Framework;
using UnityEngine;

namespace GameLogic.Tests.EditMode.Framework
{
    /// <summary>
    /// 验证 Inspector 可编辑的资源部署 ScriptableObject 策略。
    /// </summary>
    [TestFixture]
    public sealed class ResourceModeConfigDeploymentTests
    {
        /// <summary>
        /// 项目配置资产应包含所有当前支持平台，并为小游戏开发构建配置本地远端资源服务。
        /// </summary>
        [Test]
        public void DeploymentAsset_DeclaresAllSupportedPlatformsWithRemoteUpdate()
        {
            ResourceModeConfig config = Resources.Load<ResourceModeConfig>(
                ResourceModeConfig.DefaultResourcesPath);

            Assert.IsNotNull(config);
            Assert.IsTrue(config.UseYooAssets, "现有部署配置应默认继续使用 YooAssets。");
            Assert.IsNotEmpty(config.PackageVersion);
            Assert.AreEqual(
                ResourceMode.HostPlay,
                config.GetPlatformConfig(ResourceRuntimePlatform.Standard).Mode);
            Assert.AreEqual(
                ResourceMode.WebPlay,
                config.GetPlatformConfig(ResourceRuntimePlatform.WechatMiniGame).Mode);
            Assert.AreEqual(
                ResourceMode.WebPlay,
                config.GetPlatformConfig(ResourceRuntimePlatform.TiktokMiniGame).Mode);
            Assert.AreEqual(
                WechatMiniGameResourceDeliveryMode.RemoteUpdate,
                config.GetPlatformConfig(ResourceRuntimePlatform.WechatMiniGame)
                    .WechatMiniGameResourceDeliveryMode);
            Assert.IsFalse(config.RequiresBuiltinPackage(ResourceRuntimePlatform.WechatMiniGame));
            Assert.AreEqual(
                "http://127.0.0.1:18081/",
                config.GetPlatformConfig(ResourceRuntimePlatform.WechatMiniGame).MainCdn);
        }

        /// <summary>
        /// Inspector 填写的 CDN 地址应统一补齐尾部斜杠。
        /// </summary>
        [Test]
        public void PlatformConfig_CdnUrls_NormalizeTrailingSlash()
        {
            var config = new ResourceDeploymentPlatformConfig(
                ResourceRuntimePlatform.WechatMiniGame,
                ResourceMode.WebPlay,
                "https://cdn.example.com/wechat",
                "https://backup.example.com/wechat",
                true,
                WechatMiniGameResourceDeliveryMode.RemoteUpdate);

            Assert.AreEqual("https://cdn.example.com/wechat/", config.MainCdn);
            Assert.AreEqual("https://backup.example.com/wechat/", config.FallbackCdn);
            Assert.IsTrue(config.DisableUnityWebCache);
        }

        /// <summary>
        /// 微信选择远端更新但未填写主 CDN 时必须明确失败。
        /// </summary>
        [Test]
        public void PlatformConfig_RemoteWechatWithoutMainCdn_Throws()
        {
            var config = new ResourceDeploymentPlatformConfig(
                ResourceRuntimePlatform.WechatMiniGame,
                ResourceMode.WebPlay,
                string.Empty,
                string.Empty,
                true,
                WechatMiniGameResourceDeliveryMode.RemoteUpdate);

            Assert.Throws<InvalidOperationException>(() => config.ValidateRemoteCdn());
        }

        /// <summary>
        /// 微信显式选择内置资源时允许不填写 CDN。
        /// </summary>
        [Test]
        public void PlatformConfig_BuiltinWechatAllowsEmptyCdn()
        {
            var config = new ResourceDeploymentPlatformConfig(
                ResourceRuntimePlatform.WechatMiniGame,
                ResourceMode.WebPlay,
                string.Empty,
                string.Empty,
                true,
                WechatMiniGameResourceDeliveryMode.BuiltinOnly);

            Assert.DoesNotThrow(config.ValidateRemoteCdn);
        }
    }
}

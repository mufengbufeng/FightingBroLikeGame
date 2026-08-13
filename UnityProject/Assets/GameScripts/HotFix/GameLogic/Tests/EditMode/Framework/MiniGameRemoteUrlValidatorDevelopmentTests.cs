using System;
using EF.Resource;
using NUnit.Framework;

namespace GameLogic.Tests.EditMode.Framework
{
    /// <summary>
    /// 验证开发构建可使用 loopback 临时 CDN，同时保留生产 URL 约束。
    /// </summary>
    [TestFixture]
    public sealed class MiniGameRemoteUrlValidatorDevelopmentTests
    {
        /// <summary>
        /// 开发构建允许同机微信开发者工具访问带端口的 loopback CDN。
        /// </summary>
        [Test]
        public void DevelopmentLoopbackWithPort_IsAllowed()
        {
            Assert.DoesNotThrow(() => MiniGameRemoteUrlValidator.Validate(
                "http://127.0.0.1:18081/",
                allowDevelopmentLoopbackWithPort: true));
        }

        /// <summary>
        /// 开发构建开关不能放宽公共网络地址的端口限制。
        /// </summary>
        [Test]
        public void DevelopmentMode_PublicEndpointWithPort_IsRejected()
        {
            Assert.Throws<InvalidOperationException>(() => MiniGameRemoteUrlValidator.Validate(
                "https://cdn.example.com:8443/content/",
                allowDevelopmentLoopbackWithPort: true));
        }
    }
}

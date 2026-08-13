using System;

namespace EF.Commercial
{
    /// <summary>
    /// 选择并创建当前平台的商业化 Provider。
    /// </summary>
    internal static class CommercialProviderFactory
    {
        private const string WechatProviderTypeName =
            "EF.Monetization.Wechat.WechatCommercialProvider, EF.Monetization.Wechat";

        /// <summary>
        /// 使用资源配置创建当前运行平台的 Provider。
        /// </summary>
        /// <param name="config">商业化配置资产。</param>
        /// <returns>始终返回可安全调用的 Provider。</returns>
        public static ICommercialProvider Create(CommercialConfig config)
        {
            CommercialPlatform platform = CommercialPlatformResolver.Current;
            CommercialPlatformConfig platformConfig = config != null
                ? config.GetPlatformConfig(platform)
                : null;

            if (platformConfig == null)
            {
                return new NullCommercialProvider(platform, "当前平台未配置商业化策略。");
            }

            if (!platformConfig.Enabled)
            {
                return new NullCommercialProvider(platform, "当前平台的商业化功能已关闭。");
            }

            if (platform == CommercialPlatform.WechatMiniGame)
            {
                return CreateWechatProvider(platformConfig) ??
                       new NullCommercialProvider(platform, "微信商业化 Provider 未随当前包体加载。");
            }

            if (platform == CommercialPlatform.TiktokMiniGame)
            {
                return new NullCommercialProvider(platform, "尚未安装抖音小游戏商业化 SDK。");
            }

            return new NullCommercialProvider(platform, "当前平台未接入商业化 SDK。");
        }

        /// <summary>
        /// 通过反射加载可选的微信 Provider，避免标准平台依赖微信程序集。
        /// </summary>
        private static ICommercialProvider CreateWechatProvider(CommercialPlatformConfig config)
        {
            try
            {
                Type providerType = Type.GetType(WechatProviderTypeName, throwOnError: false);
                if (providerType == null || !typeof(ICommercialProvider).IsAssignableFrom(providerType))
                {
                    return null;
                }

                return Activator.CreateInstance(providerType, config) as ICommercialProvider;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

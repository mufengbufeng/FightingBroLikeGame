namespace EF.Resource
{
    /// <summary>
    /// 集中处理 Player 平台的资源运行模式与微信资源交付策略。
    /// </summary>
    internal static class ResourceModeResolver
    {
        /// <summary>
        /// 解析 Player 实际使用的 YooAsset 运行模式。
        /// 微信小游戏基于 WebGL，必须使用 WebPlay 模式而不能使用 OfflinePlay。
        /// </summary>
        public static ResourceMode ResolvePlayerMode(
            ResourceMode configuredMode,
            ResourceRuntimePlatform platform)
        {
            return platform == ResourceRuntimePlatform.WechatMiniGame
                ? ResourceMode.WebPlay
                : configuredMode;
        }

        /// <summary>
        /// 判断微信小游戏是否应使用仅含包体资源的 WebServer 文件系统。
        /// </summary>
        public static bool UsesWechatBuiltinWebServer(
            WechatMiniGameResourceDeliveryMode deliveryMode,
            ResourceRuntimePlatform platform)
        {
            return platform == ResourceRuntimePlatform.WechatMiniGame &&
                   deliveryMode == WechatMiniGameResourceDeliveryMode.BuiltinOnly;
        }
    }
}

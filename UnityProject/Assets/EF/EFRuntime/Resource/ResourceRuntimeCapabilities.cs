namespace EF.Resource
{
    /// <summary>
    /// 当前资源运行时所处的平台类型。
    /// </summary>
    public enum ResourceRuntimePlatform
    {
        Standard = 0,
        WechatMiniGame = 1,
        TiktokMiniGame = 2
    }

    /// <summary>
    /// 描述平台对 YooAsset 下载器和同步加载的支持能力。
    /// </summary>
    public readonly struct ResourceRuntimeCapabilities
    {
        /// <summary>
        /// 创建指定平台的资源能力快照。
        /// </summary>
        private ResourceRuntimeCapabilities(
            ResourceRuntimePlatform platform,
            bool supportsSynchronousLoading,
            bool supportsResourceDownloader)
        {
            Platform = platform;
            SupportsSynchronousLoading = supportsSynchronousLoading;
            SupportsResourceDownloader = supportsResourceDownloader;
        }

        /// <summary>
        /// 当前编译目标对应的资源平台能力。
        /// </summary>
        public static ResourceRuntimeCapabilities Current
        {
            get
            {
#if UNITY_WEBGL && (WEIXINMINIGAME || UNITY_WECHATMINIGAME)
                return ForPlatform(ResourceRuntimePlatform.WechatMiniGame);
#elif UNITY_WEBGL && DOUYINMINIGAME
                return ForPlatform(ResourceRuntimePlatform.TiktokMiniGame);
#else
                return ForPlatform(ResourceRuntimePlatform.Standard);
#endif
            }
        }

        public ResourceRuntimePlatform Platform { get; }

        public bool SupportsSynchronousLoading { get; }

        public bool SupportsResourceDownloader { get; }

        public bool IsMiniGame => Platform != ResourceRuntimePlatform.Standard;

        /// <summary>
        /// 返回指定平台的固定 YooAsset 能力矩阵。
        /// </summary>
        public static ResourceRuntimeCapabilities ForPlatform(ResourceRuntimePlatform platform)
        {
            switch (platform)
            {
                case ResourceRuntimePlatform.WechatMiniGame:
                case ResourceRuntimePlatform.TiktokMiniGame:
                    return new ResourceRuntimeCapabilities(platform, false, false);
                default:
                    return new ResourceRuntimeCapabilities(ResourceRuntimePlatform.Standard, true, true);
            }
        }
    }
}

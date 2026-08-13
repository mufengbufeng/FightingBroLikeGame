namespace EF.Resource
{
    /// <summary>
    /// 资源运行模式，对齐 YooAssets 的播放模式枚举。
    /// </summary>
    public enum ResourceMode
    {
        /// <summary>
        /// 编辑器模拟模式，便于在 Unity 编辑器环境下调试资源。
        /// </summary>
        EditorSimulate,

        /// <summary>
        /// 离线模式，从本地内置资源中加载内容。
        /// </summary>
        OfflinePlay,

        /// <summary>
        /// 联机模式，通过远程服务器拉取资源并支持缓存。
        /// </summary>
        HostPlay,

        /// <summary>
        /// Web 模式，面向 WebGL 等无本地文件系统的平台。
        /// </summary>
        WebPlay
    }

    /// <summary>
    /// 微信小游戏的资源交付方式。
    /// </summary>
    public enum WechatMiniGameResourceDeliveryMode
    {
        /// <summary>
        /// 仅读取构建进小游戏包体的资源，不访问 CDN。
        /// 仅适用于已提供包内 StreamingAssets 读取能力的自定义小游戏转换与文件系统管线。
        /// </summary>
        BuiltinOnly,

        /// <summary>
        /// 从远端 CDN 获取版本、清单和资源文件。
        /// </summary>
        RemoteUpdate
    }

}

namespace EF.Commercial
{
    /// <summary>
    /// 根据编译目标解析商业化 Provider 所需的平台标识。
    /// </summary>
    internal static class CommercialPlatformResolver
    {
        /// <summary>
        /// 当前运行平台。
        /// </summary>
        public static CommercialPlatform Current
        {
            get
            {
#if UNITY_EDITOR
                return CommercialPlatform.Editor;
#elif WEIXINMINIGAME
                return CommercialPlatform.WechatMiniGame;
#elif DOUYINMINIGAME
                return CommercialPlatform.TiktokMiniGame;
#elif UNITY_WEBGL
                return CommercialPlatform.Web;
#else
                return CommercialPlatform.Native;
#endif
            }
        }
    }
}

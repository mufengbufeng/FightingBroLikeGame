namespace GameProto
{
    /// <summary>
    /// GameProto 热更新程序集占位类型，用于保留 HybridCLR 加载链路。
    /// </summary>
    public static class GameProtoAssemblyMarker
    {
        /// <summary>
        /// 标记程序集已被保留。
        /// </summary>
        public static bool IsPresent()
        {
            return true;
        }
    }
}


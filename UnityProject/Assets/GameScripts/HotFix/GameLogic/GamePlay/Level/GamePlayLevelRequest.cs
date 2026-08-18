namespace GameLogic.GamePlay
{
    /// <summary>
    /// GamePlay 关卡加载请求。
    /// </summary>
    public sealed class GamePlayLevelRequest
    {
        /// <summary>
        /// 创建关卡请求，空地址回退到 Level_01。
        /// </summary>
        public GamePlayLevelRequest(string address)
        {
            Address = string.IsNullOrWhiteSpace(address) ? "Level_01" : address;
        }

        /// <summary>
        /// YooAsset 文件名地址。
        /// </summary>
        public string Address { get; }
    }
}

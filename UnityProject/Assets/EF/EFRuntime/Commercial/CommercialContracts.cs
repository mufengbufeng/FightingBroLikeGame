using System;

namespace EF.Commercial
{
    /// <summary>
    /// 商业化服务当前所运行的平台。
    /// </summary>
    public enum CommercialPlatform
    {
        Editor,
        WechatMiniGame,
        TiktokMiniGame,
        Web,
        Native
    }

    /// <summary>
    /// 平台商业化 Provider 已启用的能力集合。
    /// </summary>
    [Flags]
    public enum CommercialCapability
    {
        None = 0,
        RewardedVideo = 1 << 0,
        Interstitial = 1 << 1,
        Banner = 1 << 2,
        Purchase = 1 << 3,
        Analytics = 1 << 4,
    }

    /// <summary>
    /// 商业化操作的统一状态。
    /// </summary>
    public enum CommercialOperationStatus
    {
        Succeeded,
        Cancelled,
        Unavailable,
        Failed,
        PendingServerVerification,
        InvalidRequest,
    }

    /// <summary>
    /// 描述一个平台 Provider 对外可用的商业化能力。
    /// </summary>
    public readonly struct CommercialCapabilities
    {
        /// <summary>
        /// 创建指定能力集合。
        /// </summary>
        public CommercialCapabilities(CommercialCapability enabledCapabilities)
        {
            EnabledCapabilities = enabledCapabilities;
        }

        /// <summary>
        /// 已启用的能力集合。
        /// </summary>
        public CommercialCapability EnabledCapabilities { get; }

        /// <summary>
        /// 判断是否包含指定能力。
        /// </summary>
        /// <param name="capability">待判断的能力。</param>
        /// <returns>已启用时返回 true。</returns>
        public bool Supports(CommercialCapability capability)
        {
            return (EnabledCapabilities & capability) == capability;
        }
    }

    /// <summary>
    /// 广告与埋点操作的统一结果。
    /// </summary>
    public readonly struct CommercialOperationResult
    {
        private CommercialOperationResult(CommercialOperationStatus status, string message)
        {
            Status = status;
            Message = message ?? string.Empty;
        }

        /// <summary>
        /// 操作状态。
        /// </summary>
        public CommercialOperationStatus Status { get; }

        /// <summary>
        /// 供日志或 UI 展示的补充信息。
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 是否可以继续执行成功分支。
        /// </summary>
        public bool IsSuccessful => Status == CommercialOperationStatus.Succeeded;

        /// <summary>
        /// 创建成功结果。
        /// </summary>
        public static CommercialOperationResult Succeeded(string message = null)
        {
            return new CommercialOperationResult(CommercialOperationStatus.Succeeded, message);
        }

        /// <summary>
        /// 创建用户取消结果。
        /// </summary>
        public static CommercialOperationResult Cancelled(string message = null)
        {
            return new CommercialOperationResult(CommercialOperationStatus.Cancelled, message);
        }

        /// <summary>
        /// 创建当前平台不可用结果。
        /// </summary>
        public static CommercialOperationResult Unavailable(string message)
        {
            return new CommercialOperationResult(CommercialOperationStatus.Unavailable, message);
        }

        /// <summary>
        /// 创建执行失败结果。
        /// </summary>
        public static CommercialOperationResult Failed(string message)
        {
            return new CommercialOperationResult(CommercialOperationStatus.Failed, message);
        }

        /// <summary>
        /// 创建无效请求结果。
        /// </summary>
        public static CommercialOperationResult InvalidRequest(string message)
        {
            return new CommercialOperationResult(CommercialOperationStatus.InvalidRequest, message);
        }
    }

    /// <summary>
    /// 由业务服务端签发的购买请求。
    /// </summary>
    public readonly struct CommercialPurchaseRequest
    {
        /// <summary>
        /// 创建购买请求。
        /// </summary>
        /// <param name="productId">游戏内商品标识。</param>
        /// <param name="orderId">服务端生成且全局唯一的订单号。</param>
        /// <param name="providerPayload">支付平台所需的服务端签名载荷。</param>
        /// <param name="quantity">购买数量。</param>
        public CommercialPurchaseRequest(string productId, string orderId, string providerPayload, int quantity = 1)
        {
            ProductId = productId ?? string.Empty;
            OrderId = orderId ?? string.Empty;
            ProviderPayload = providerPayload ?? string.Empty;
            Quantity = quantity;
        }

        /// <summary>
        /// 游戏内商品标识。
        /// </summary>
        public string ProductId { get; }

        /// <summary>
        /// 服务端订单号。
        /// </summary>
        public string OrderId { get; }

        /// <summary>
        /// 服务端签名的支付平台载荷。
        /// </summary>
        public string ProviderPayload { get; }

        /// <summary>
        /// 购买数量。
        /// </summary>
        public int Quantity { get; }

        /// <summary>
        /// 判断请求是否具备发起支付的最低条件。
        /// </summary>
        public bool IsValid => !string.IsNullOrWhiteSpace(ProductId) &&
                               !string.IsNullOrWhiteSpace(OrderId) &&
                               Quantity > 0;
    }

    /// <summary>
    /// 支付操作的客户端结果。成功付款不等同于游戏内发货。
    /// </summary>
    public readonly struct CommercialPurchaseResult
    {
        private CommercialPurchaseResult(CommercialOperationStatus status, string orderId, string message)
        {
            Status = status;
            OrderId = orderId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        /// <summary>
        /// 支付客户端状态。
        /// </summary>
        public CommercialOperationStatus Status { get; }

        /// <summary>
        /// 对应的服务端订单号。
        /// </summary>
        public string OrderId { get; }

        /// <summary>
        /// 供日志或 UI 展示的补充信息。
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 是否必须等待服务端验签和幂等发货确认。
        /// </summary>
        public bool RequiresServerVerification =>
            Status == CommercialOperationStatus.PendingServerVerification;

        /// <summary>
        /// 创建等待服务端确认的结果。
        /// </summary>
        public static CommercialPurchaseResult PendingServerVerification(string orderId, string message = null)
        {
            return new CommercialPurchaseResult(
                CommercialOperationStatus.PendingServerVerification,
                orderId,
                message);
        }

        /// <summary>
        /// 创建当前平台不可用结果。
        /// </summary>
        public static CommercialPurchaseResult Unavailable(string orderId, string message)
        {
            return new CommercialPurchaseResult(CommercialOperationStatus.Unavailable, orderId, message);
        }

        /// <summary>
        /// 创建无效请求结果。
        /// </summary>
        public static CommercialPurchaseResult InvalidRequest(string orderId, string message)
        {
            return new CommercialPurchaseResult(CommercialOperationStatus.InvalidRequest, orderId, message);
        }

        /// <summary>
        /// 创建支付失败结果。
        /// </summary>
        public static CommercialPurchaseResult Failed(string orderId, string message)
        {
            return new CommercialPurchaseResult(CommercialOperationStatus.Failed, orderId, message);
        }
    }
}

using UnityEngine;

namespace GameLogic.GamePlay
{
    /// <summary>
    /// 授权同对象非触发碰撞器参与边缘登台的标记。
    /// </summary>
    public sealed class PlatformLedge : MonoBehaviour
    {
    }

    /// <summary>
    /// 玩家预制体提供的脚部卡边校正范围。
    /// </summary>
    internal readonly struct PlatformFootCatchConfig
    {
        /// <summary>
        /// 初始化脚部卡边校正范围。
        /// </summary>
        /// <param name="footCatchDepth">脚部最大卡入深度。</param>
        /// <param name="maxSnapCorrection">单轴最大校正距离。</param>
        /// <param name="landingInset">脚部站立间隙。</param>
        /// <param name="queryPadding">候选碰撞器查询扩展。</param>
        internal PlatformFootCatchConfig(
            float footCatchDepth,
            float maxSnapCorrection,
            float landingInset,
            Vector2 queryPadding)
        {
            FootCatchDepth = footCatchDepth;
            MaxSnapCorrection = maxSnapCorrection;
            LandingInset = landingInset;
            QueryPadding = queryPadding;
        }

        internal float FootCatchDepth { get; }

        internal float MaxSnapCorrection { get; }

        internal float LandingInset { get; }

        internal Vector2 QueryPadding { get; }
    }

    /// <summary>
    /// 集中判定边缘登台资格并计算站立位置。
    /// </summary>
    internal static class PlatformLedgeResolver
    {
        private const float InputDeadZone = 0.1f;

        /// <summary>
        /// 尝试将脚部卡在平台顶缘的空中玩家小幅校正到站立位置。
        /// </summary>
        /// <param name="playerBounds">玩家胶囊的世界 Bounds。</param>
        /// <param name="platformBounds">候选平台碰撞器的世界 Bounds。</param>
        /// <param name="moveX">当前水平输入。</param>
        /// <param name="config">玩家预制体配置的脚部卡边范围。</param>

        /// <param name="targetBoundsCenter">成功时的玩家 Bounds 目标中心。</param>
        /// <returns>仅在脚部卡边且校正距离很小时为 true。</returns>
        internal static bool TryResolveLanding(
            Bounds playerBounds,
            Bounds platformBounds,
            float moveX,
            in PlatformFootCatchConfig config,
            out Vector2 targetBoundsCenter)
        {
            targetBoundsCenter = Vector2.zero;
            float footCatchDepth = platformBounds.max.y - playerBounds.min.y;
            if (Mathf.Abs(moveX) <= InputDeadZone
                || platformBounds.size.x < playerBounds.size.x + config.LandingInset * 2f
                || footCatchDepth < -config.LandingInset
                || footCatchDepth > config.FootCatchDepth)
            {
                return false;
            }

            bool enteringFromLeft = moveX > 0f;
            float targetX = enteringFromLeft
                ? platformBounds.min.x + playerBounds.extents.x + config.LandingInset
                : platformBounds.max.x - playerBounds.extents.x - config.LandingInset;
            float horizontalCorrection = targetX - playerBounds.center.x;
            if (horizontalCorrection * moveX < 0f
                || Mathf.Abs(horizontalCorrection) > config.MaxSnapCorrection)
            {
                return false;
            }

            targetBoundsCenter = new Vector2(
                targetX,
                platformBounds.max.y + playerBounds.extents.y + config.LandingInset);
            return true;
        }
    }
}

namespace GameLogic.GamePlay
{
    /// <summary>
    /// 玩家落地移动状态。
    /// </summary>
    internal sealed class PlayerGroundedFsmState : PlayerMovementFsmState
    {
        internal PlayerGroundedFsmState()
            : base(PlayerMoveState.Grounded)
        {
        }
    }
}

namespace GameLogic.GamePlay
{
    /// <summary>
    /// 玩家攀爬移动状态。
    /// </summary>
    internal sealed class PlayerClimbingFsmState : PlayerMovementFsmState
    {
        internal PlayerClimbingFsmState()
            : base(PlayerMoveState.Climbing)
        {
        }
    }
}

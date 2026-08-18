namespace GameLogic.GamePlay
{
    /// <summary>
    /// 玩家冲刺移动状态。
    /// </summary>
    internal sealed class PlayerDashingFsmState : PlayerMovementFsmState
    {
        internal PlayerDashingFsmState()
            : base(PlayerMoveState.Dashing)
        {
        }
    }
}

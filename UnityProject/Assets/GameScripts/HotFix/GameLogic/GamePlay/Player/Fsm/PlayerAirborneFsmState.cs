namespace GameLogic.GamePlay
{
    /// <summary>
    /// 玩家空中移动状态。
    /// </summary>
    internal sealed class PlayerAirborneFsmState : PlayerMovementFsmState
    {
        internal PlayerAirborneFsmState()
            : base(PlayerMoveState.Airborne)
        {
        }
    }
}

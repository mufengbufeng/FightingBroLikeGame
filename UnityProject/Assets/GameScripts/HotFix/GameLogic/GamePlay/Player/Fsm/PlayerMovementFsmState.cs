using EF.Fsm;

namespace GameLogic.GamePlay
{
    /// <summary>
    /// 玩家移动状态机的状态基类，进入状态时同步实体的移动状态。
    /// </summary>
    internal abstract class PlayerMovementFsmState : FsmState<PlayerEntity>
    {
        private readonly PlayerMoveState _movementState;

        protected PlayerMovementFsmState(PlayerMoveState movementState)
        {
            _movementState = movementState;
        }

        protected override void OnEnter(IFsm<PlayerEntity> fsm)
        {
            fsm.Owner.SetMovementState(_movementState);
        }
    }
}

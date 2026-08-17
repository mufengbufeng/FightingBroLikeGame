using UnityEngine;

namespace GameLogic.GamePlay
{
    /// <summary>
    /// 玩家移动互斥状态。
    /// </summary>
    public enum PlayerMoveState
    {
        Grounded,
        Airborne,
        Dashing,
        Climbing
    }

    /// <summary>
    /// 单帧电机输入。
    /// </summary>
    public struct PlayerMotorInput
    {
        public Vector2 Move;
        public bool JumpPressed;
        public bool JumpReleased;
        public bool DashPressed;
    }

    /// <summary>
    /// 单帧接触结果。
    /// </summary>
    public struct PlayerMotorContacts
    {
        public bool Grounded;
        public bool OnLadder;
    }

    /// <summary>
    /// 玩家电机默认数值。
    /// </summary>
    public sealed class PlayerMotorConfig
    {
        public float MoveSpeed = 6f;
        public float JumpVelocity = 10f;
        public float Gravity = 28f;
        public float JumpCutMultiplier = 0.45f;
        public float CoyoteTime = 0.08f;
        public float JumpBuffer = 0.10f;
        public float DashSpeed = 16f;
        public float DashDuration = 0.15f;
        public float DashCooldown = 0.40f;
        public float ClimbSpeed = 4f;
        public float ClimbEnterThreshold = 0.25f;
        public float ClimbHorizontalScale = 0.5f;
    }

    /// <summary>
    /// 可单测的 2D 移动电机。
    /// </summary>
    public sealed class PlayerMotor
    {
        private const float MoveDeadZone = 0.1f;

        private readonly PlayerMotorConfig _config;
        private float _coyoteRemaining;
        private float _jumpBufferRemaining;
        private float _dashRemaining;
        private float _dashCooldownRemaining;

        public PlayerMotor()
            : this(null)
        {
        }

        public PlayerMotor(PlayerMotorConfig config)
        {
            _config = config ?? new PlayerMotorConfig();
            Reset(Vector2.zero);
        }

        public Vector2 Velocity { get; private set; }

        public PlayerMoveState State { get; private set; }

        public int Facing { get; private set; }

        public bool AirDashAvailable { get; private set; }

        /// <summary>
        /// 清空冷却、缓冲、土狼和冲刺计时。
        /// </summary>
        public void Reset(Vector2 velocity)
        {
            Velocity = velocity;
            Facing = 1;
            State = PlayerMoveState.Airborne;
            AirDashAvailable = true;
            _coyoteRemaining = 0f;
            _jumpBufferRemaining = 0f;
            _dashRemaining = 0f;
            _dashCooldownRemaining = 0f;
        }

        /// <summary>
        /// 按冲刺、攀爬、跳跃、水平移动、重力的优先级推进一拍。
        /// </summary>
        public void Tick(float deltaTime, PlayerMotorInput input, PlayerMotorContacts contacts)
        {
            if (deltaTime < 0f)
            {
                deltaTime = 0f;
            }

            UpdateGroundedTimers(deltaTime, contacts);
            UpdateJumpBuffer(deltaTime, input.JumpPressed);

            if (TickDash(deltaTime, input, contacts))
            {
                return;
            }

            if (TickClimb(input, contacts))
            {
                return;
            }
            bool jumped = TryJump(input, contacts);
            ApplyHorizontal(input);
            bool jumpCut = ApplyJumpCut(input);

            if (!jumped && !jumpCut)
            {
                ApplyGravity(deltaTime, contacts);
            }

            if (State != PlayerMoveState.Dashing && State != PlayerMoveState.Climbing)
            {
                State = jumped || !contacts.Grounded
                    ? PlayerMoveState.Airborne
                    : PlayerMoveState.Grounded;
            }
        }

        private void UpdateGroundedTimers(float deltaTime, PlayerMotorContacts contacts)
        {
            if (contacts.Grounded)
            {
                _coyoteRemaining = _config.CoyoteTime;
                AirDashAvailable = true;
                return;
            }

            _coyoteRemaining = Mathf.Max(0f, _coyoteRemaining - deltaTime);
        }

        private void UpdateJumpBuffer(float deltaTime, bool jumpPressed)
        {
            if (jumpPressed)
            {
                _jumpBufferRemaining = _config.JumpBuffer;
                return;
            }

            _jumpBufferRemaining = Mathf.Max(0f, _jumpBufferRemaining - deltaTime);
        }

        private bool TickDash(float deltaTime, PlayerMotorInput input, PlayerMotorContacts contacts)
        {
            if (_dashCooldownRemaining > 0f)
            {
                _dashCooldownRemaining = Mathf.Max(0f, _dashCooldownRemaining - deltaTime);
            }

            if (State == PlayerMoveState.Dashing)
            {
                _dashRemaining -= deltaTime;
                if (_dashRemaining > 0f)
                {
                    Velocity = new Vector2(Facing * _config.DashSpeed, 0f);
                    State = PlayerMoveState.Dashing;
                    return true;
                }

                _dashCooldownRemaining = _config.DashCooldown;
                State = contacts.Grounded ? PlayerMoveState.Grounded : PlayerMoveState.Airborne;
            }

            if (!input.DashPressed || _dashCooldownRemaining > 0f)
            {
                return false;
            }

            if (!contacts.Grounded && !AirDashAvailable)
            {
                return false;
            }

            if (Mathf.Abs(input.Move.x) >= MoveDeadZone)
            {
                Facing = input.Move.x > 0f ? 1 : -1;
            }
            else if (Facing == 0)
            {
                Facing = 1;
            }

            Velocity = new Vector2(Facing * _config.DashSpeed, 0f);
            State = PlayerMoveState.Dashing;
            _dashRemaining = _config.DashDuration;
            if (!contacts.Grounded)
            {
                AirDashAvailable = false;
            }

            return true;
        }

        private bool TickClimb(PlayerMotorInput input, PlayerMotorContacts contacts)
        {
            bool wantsClimb = contacts.OnLadder && Mathf.Abs(input.Move.y) >= _config.ClimbEnterThreshold;
            if (State != PlayerMoveState.Climbing && !wantsClimb)
            {
                return false;
            }

            if (!contacts.OnLadder)
            {
                State = contacts.Grounded ? PlayerMoveState.Grounded : PlayerMoveState.Airborne;
                return false;
            }

            if (input.JumpPressed)
            {
                Velocity = new Vector2(ResolveHorizontal(input.Move.x), _config.JumpVelocity);
                _coyoteRemaining = 0f;
                _jumpBufferRemaining = 0f;
                State = PlayerMoveState.Airborne;
                return true;
            }

            if (!wantsClimb && State != PlayerMoveState.Climbing)
            {
                return false;
            }

            Velocity = new Vector2(
                ResolveHorizontal(input.Move.x) * _config.ClimbHorizontalScale,
                input.Move.y * _config.ClimbSpeed);
            State = PlayerMoveState.Climbing;
            return true;
        }

        private bool TryJump(PlayerMotorInput input, PlayerMotorContacts contacts)
        {
            bool buffered = _jumpBufferRemaining > 0f;
            if (!input.JumpPressed && !buffered)
            {
                return false;
            }

            bool canJump = contacts.Grounded || _coyoteRemaining > 0f || State == PlayerMoveState.Climbing;
            if (!canJump)
            {
                return false;
            }

            Velocity = new Vector2(Velocity.x, _config.JumpVelocity);
            _coyoteRemaining = 0f;
            _jumpBufferRemaining = 0f;
            State = PlayerMoveState.Airborne;
            return true;
        }

        private void ApplyHorizontal(PlayerMotorInput input)
        {
            float moveX = ResolveHorizontal(input.Move.x);
            if (Mathf.Abs(moveX) >= MoveDeadZone)
            {
                Facing = moveX > 0f ? 1 : -1;
            }

            Velocity = new Vector2(moveX * _config.MoveSpeed, Velocity.y);
        }

        private void ApplyGravity(float deltaTime, PlayerMotorContacts contacts)
        {
            float vertical = Velocity.y - _config.Gravity * deltaTime;
            if (contacts.Grounded && vertical < 0f)
            {
                vertical = 0f;
            }

            Velocity = new Vector2(Velocity.x, vertical);
        }

        private bool ApplyJumpCut(PlayerMotorInput input)
        {
            if (!input.JumpReleased || Velocity.y <= 0f)
            {
                return false;
            }

            Velocity = new Vector2(Velocity.x, Velocity.y * _config.JumpCutMultiplier);
            return true;
        }

        private static float ResolveHorizontal(float moveX)
        {
            return Mathf.Abs(moveX) < MoveDeadZone ? 0f : moveX;
        }
    }
}

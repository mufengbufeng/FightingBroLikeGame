using EF.Debugger;
using EF.Entity;
using EF.Fsm;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameLogic.GamePlay
{
    /// <summary>
    /// 玩家实体的出生参数。
    /// </summary>
    public readonly struct PlayerEntitySpawnData
    {
        /// <summary>
        /// 初始化玩家实体的出生参数。
        /// </summary>
        /// <param name="position">玩家世界坐标。</param>
        /// <param name="rotation">玩家世界旋转。</param>
        /// <param name="scale">玩家本地缩放。</param>
        public PlayerEntitySpawnData(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }

        /// <summary>
        /// 玩家世界坐标。
        /// </summary>
        public Vector3 Position { get; }

        /// <summary>
        /// 玩家世界旋转。
        /// </summary>
        public Quaternion Rotation { get; }

        /// <summary>
        /// 玩家本地缩放。
        /// </summary>
        public Vector3 Scale { get; }
    }

    /// <summary>
    /// 负责输入、物理检测和移动状态的玩家实体。
    /// </summary>
    public sealed class PlayerEntity : EntityBase
    {
        /// <summary>
        /// 玩家实体组名称。
        /// </summary>
        public const string GroupName = "Player";

        /// <summary>
        /// 玩家实体资源地址。
        /// </summary>
        public const string AssetName = "GamePlayPlayer_01";

        private const string MovementFsmNamePrefix = "PlayerMovement.";

        private static readonly Vector2 GroundCheckSize = new Vector2(0.55f, 0.12f);
        private static readonly Vector2 GroundCheckOffset = new Vector2(0f, -0.82f);
        private static readonly ContactFilter2D GroundFilter = new ContactFilter2D
        {
            useTriggers = false,
            useLayerMask = true,
            layerMask = 1
        };
        private static readonly ContactFilter2D LadderFilter = new ContactFilter2D
        {
            useTriggers = true,
            useLayerMask = true,
            layerMask = 1
        };

        private readonly PlayerMotor _motor = new PlayerMotor();
        private readonly Collider2D[] _overlapHits = new Collider2D[8];

        private GameObject _handle;
        private Rigidbody2D _body;
        private CapsuleCollider2D _capsule;
        private InputActionMap _playerMap;
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _dashAction;
        private Vector2 _move;
        private bool _jumpPressed;
        private bool _jumpReleased;
        private bool _dashPressed;
        private bool _inputReady;
        private IFsmManager _fsmManager;
        private IFsm<PlayerEntity> _movementFsm;

        /// <summary>
        /// 玩家关联的 Unity GameObject。
        /// </summary>
        public override GameObject Handle
        {
            get => _handle;
            set => _handle = value;
        }

        /// <summary>
        /// EF 状态机当前管理的玩家移动状态。
        /// </summary>
        public PlayerMoveState MovementState { get; private set; }

        /// <summary>
        /// 显示玩家并初始化出生位置、物理组件和输入映射。
        /// </summary>
        /// <param name="userData">玩家出生参数。</param>
        public override void OnShow(object userData)
        {
            if (Handle == null)
            {
                Log.Error("[PlayerEntity] 玩家实体缺少 GameObject。");
                return;
            }

            if (userData is PlayerEntitySpawnData spawnData)
            {
                Transform playerTransform = Handle.transform;
                playerTransform.SetPositionAndRotation(spawnData.Position, spawnData.Rotation);
                playerTransform.localScale = spawnData.Scale;
            }

            _body = Handle.GetComponent<Rigidbody2D>();
            _capsule = Handle.GetComponent<CapsuleCollider2D>();
            Handle.SetActive(true);
            _motor.Reset(Vector2.zero);
            InitializeMovementFsm();
            ResetInput();
            InitializeInput();
        }

        /// <summary>
        /// 停止玩家输入和物理运动，并隐藏实体视图。
        /// </summary>
        /// <param name="isShutdown">是否由框架关闭触发。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnHide(bool isShutdown, object userData)
        {
            DestroyMovementFsm();
            DisableInput();
            _move = Vector2.zero;
            _jumpPressed = false;
            _jumpReleased = false;
            _dashPressed = false;

            if (_body != null)
            {
                _body.linearVelocity = Vector2.zero;
            }

            if (Handle != null)
            {
                Handle.SetActive(false);
            }
        }

        /// <summary>
        /// 清理回收实体持有的 Unity 组件引用。
        /// </summary>
        public override void OnRecycle()
        {
            _body = null;
            _capsule = null;
            DestroyMovementFsm();
        }

        /// <summary>
        /// 在帧更新中读取输入，并更新实体特性。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间（秒）。</param>
        /// <param name="realElapseSeconds">真实流逝时间（秒）。</param>
        public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(elapseSeconds, realElapseSeconds);

            if (!_inputReady)
            {
                return;
            }

            _move = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
            _jumpPressed |= _jumpAction != null && _jumpAction.WasPressedThisFrame();
            _jumpReleased |= _jumpAction != null && _jumpAction.WasReleasedThisFrame();
            _dashPressed |= _dashAction != null && _dashAction.WasPressedThisFrame();
        }

        /// <summary>
        /// 在物理帧中驱动玩家移动状态机和 Rigidbody2D。
        /// </summary>
        /// <param name="fixedDeltaTime">物理帧间隔（秒）。</param>
        public override void OnFixedUpdate(float fixedDeltaTime)
        {
            if (_body == null || _capsule == null || _movementFsm == null)
            {
                return;
            }

            var input = new PlayerMotorInput
            {
                Move = _inputReady ? _move : Vector2.zero,
                JumpPressed = _inputReady && _jumpPressed,
                JumpReleased = _inputReady && _jumpReleased,
                DashPressed = _inputReady && _dashPressed
            };
            _jumpPressed = false;
            _jumpReleased = false;
            _dashPressed = false;

            var contacts = new PlayerMotorContacts
            {
                Grounded = IsGrounded(),
                OnLadder = IsOnLadder()
            };

            PlayerMoveState nextState = _motor.Tick(fixedDeltaTime, MovementState, input, contacts);
            ChangeMovementState(nextState);
            _body.linearVelocity = _motor.Velocity;

            if (_motor.Facing != 0)
            {
                Vector3 scale = Handle.transform.localScale;
                scale.x = _motor.Facing * Mathf.Abs(scale.x);
                Handle.transform.localScale = scale;
            }
        }

        private void InitializeMovementFsm()
        {
            DestroyMovementFsm();
            _fsmManager = GameLogicEntry.Fsm;
            if (_fsmManager == null)
            {
                Log.Error("[PlayerEntity] 未获取到状态机管理器。");
                return;
            }

            _movementFsm = _fsmManager.CreateFsm(
                $"{MovementFsmNamePrefix}{Id}",
                this,
                new PlayerGroundedFsmState(),
                new PlayerAirborneFsmState(),
                new PlayerDashingFsmState(),
                new PlayerClimbingFsmState());
            _movementFsm.Start<PlayerAirborneFsmState>();
        }

        private void DestroyMovementFsm()
        {
            if (_movementFsm != null)
            {
                _fsmManager?.DestroyFsm(_movementFsm);
                _movementFsm = null;
            }

            _fsmManager = null;
        }

        private void ChangeMovementState(PlayerMoveState nextState)
        {
            switch (nextState)
            {
                case PlayerMoveState.Grounded:
                    _movementFsm.ChangeState<PlayerGroundedFsmState>();
                    break;
                case PlayerMoveState.Airborne:
                    _movementFsm.ChangeState<PlayerAirborneFsmState>();
                    break;
                case PlayerMoveState.Dashing:
                    _movementFsm.ChangeState<PlayerDashingFsmState>();
                    break;
                case PlayerMoveState.Climbing:
                    _movementFsm.ChangeState<PlayerClimbingFsmState>();
                    break;
            }
        }

        private void SetMovementState(PlayerMoveState movementState)
        {
            MovementState = movementState;
        }

        private void InitializeInput()
        {
            InputActionAsset asset = InputSystem.actions;
            if (asset == null)
            {
                InputActionAsset[] loadedAssets = Resources.FindObjectsOfTypeAll<InputActionAsset>();
                for (int i = 0; i < loadedAssets.Length; i++)
                {
                    if (loadedAssets[i] != null && loadedAssets[i].name == "InputSystem_Actions")
                    {
                        asset = loadedAssets[i];
                        break;
                    }
                }
            }

            if (asset == null)
            {
                Log.Error("[PlayerEntity] 未找到 InputActionAsset。");
                return;
            }

            _playerMap = asset.FindActionMap("Player");
            if (_playerMap == null)
            {
                Log.Error("[PlayerEntity] 未找到 Player 输入映射。");
                return;
            }

            _playerMap.Enable();
            _moveAction = _playerMap.FindAction("Move");
            _jumpAction = _playerMap.FindAction("Jump");
            _dashAction = _playerMap.FindAction("Dash") ?? _playerMap.FindAction("Sprint");
            _inputReady = true;
        }

        private void ResetInput()
        {
            _move = Vector2.zero;
            _jumpPressed = false;
            _jumpReleased = false;
            _dashPressed = false;
            _inputReady = false;
        }

        private void DisableInput()
        {
            if (_playerMap != null)
            {
                _playerMap.Disable();
            }

            _moveAction = null;
            _jumpAction = null;
            _dashAction = null;
            _playerMap = null;
            _inputReady = false;
        }

        private bool IsGrounded()
        {
            Vector2 center = (Vector2)Handle.transform.position + GroundCheckOffset;
            int hitCount = Physics2D.OverlapBox(center, GroundCheckSize, 0f, GroundFilter, _overlapHits);
            return HasForeignHit(hitCount);
        }

        private bool IsOnLadder()
        {
            Bounds bounds = _capsule.bounds;
            int hitCount = Physics2D.OverlapBox(bounds.center, bounds.size, 0f, LadderFilter, _overlapHits);
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = _overlapHits[i];
                if (hit != null && hit.GetComponent<LadderVolume>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasForeignHit(int hitCount)
        {
            Transform playerTransform = Handle.transform;
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = _overlapHits[i];
                if (hit != null && hit.transform != playerTransform && !hit.transform.IsChildOf(playerTransform))
                {
                    return true;
                }
            }

            return false;
        }
        private abstract class PlayerMovementFsmState : FsmState<PlayerEntity>
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

        private sealed class PlayerGroundedFsmState : PlayerMovementFsmState
        {
            public PlayerGroundedFsmState()
                : base(PlayerMoveState.Grounded)
            {
            }
        }

        private sealed class PlayerAirborneFsmState : PlayerMovementFsmState
        {
            public PlayerAirborneFsmState()
                : base(PlayerMoveState.Airborne)
            {
            }
        }

        private sealed class PlayerDashingFsmState : PlayerMovementFsmState
        {
            public PlayerDashingFsmState()
                : base(PlayerMoveState.Dashing)
            {
            }
        }

        private sealed class PlayerClimbingFsmState : PlayerMovementFsmState
        {
            public PlayerClimbingFsmState()
                : base(PlayerMoveState.Climbing)
            {
            }
        }
    }
}

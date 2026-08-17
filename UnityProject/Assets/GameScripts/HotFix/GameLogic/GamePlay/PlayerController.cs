using EF.Debugger;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameLogic.GamePlay
{
    /// <summary>
    /// 读取 Input System 并驱动 PlayerMotor。
    /// </summary>
    public sealed class PlayerController : MonoBehaviour
    {
        private static readonly Vector2 GroundCheckSize = new Vector2(0.55f, 0.12f);
        private static readonly Vector2 GroundCheckOffset = new Vector2(0f, -0.82f);

        private readonly PlayerMotor _motor = new PlayerMotor();
        private readonly Collider2D[] _overlapHits = new Collider2D[8];

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

        public PlayerMotor Motor => _motor;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _capsule = GetComponent<CapsuleCollider2D>();
        }

        private void OnEnable()
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
                Log.Error("[PlayerController] 未找到 InputActionAsset。");
                _inputReady = false;
                return;
            }

            _playerMap = asset.FindActionMap("Player");
            if (_playerMap == null)
            {
                Log.Error("[PlayerController] 未找到 Player 输入映射。");
                _inputReady = false;
                return;
            }

            _playerMap.Enable();
            _moveAction = _playerMap.FindAction("Move");
            _jumpAction = _playerMap.FindAction("Jump");
            _dashAction = _playerMap.FindAction("Dash") ?? _playerMap.FindAction("Sprint");
            _inputReady = true;
        }

        private void OnDisable()
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

        private void Update()
        {
            if (!_inputReady)
            {
                return;
            }

            _move = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
            _jumpPressed |= _jumpAction != null && _jumpAction.WasPressedThisFrame();
            _jumpReleased |= _jumpAction != null && _jumpAction.WasReleasedThisFrame();
            _dashPressed |= _dashAction != null && _dashAction.WasPressedThisFrame();
        }

        private void FixedUpdate()
        {
            if (_body == null || _capsule == null)
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

            _motor.Tick(Time.fixedDeltaTime, input, contacts);
            _body.linearVelocity = _motor.Velocity;

            if (_motor.Facing != 0)
            {
                Vector3 scale = transform.localScale;
                scale.x = _motor.Facing * Mathf.Abs(scale.x);
                transform.localScale = scale;
            }
        }

        private bool IsGrounded()
        {
            var filter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = true,
                layerMask = 1
            };

            Vector2 center = (Vector2)transform.position + GroundCheckOffset;
            int hitCount = Physics2D.OverlapBox(center, GroundCheckSize, 0f, filter, _overlapHits);
            return HasForeignHit(hitCount);
        }

        private bool IsOnLadder()
        {
            var filter = new ContactFilter2D
            {
                useTriggers = true,
                useLayerMask = true,
                layerMask = 1
            };

            Bounds bounds = _capsule.bounds;
            int hitCount = Physics2D.OverlapBox(bounds.center, bounds.size, 0f, filter, _overlapHits);
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
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = _overlapHits[i];
                if (hit != null && hit.transform != transform && !hit.transform.IsChildOf(transform))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

using GameLogic.GamePlay;
using NUnit.Framework;
using UnityEngine;

namespace GameLogic.Tests
{
    /// <summary>
    /// 锁定 PlayerMotor 默认数值下的移动契约。
    /// </summary>
    [TestFixture]
    public sealed class PlayerMotorTests
    {
        private const float DeltaTime = 0.02f;

        [Test]
        public void 地面右移_速度与朝向正确()
        {
            var motor = new PlayerMotor();
            PlayerMoveState state = motor.Tick(
                DeltaTime,
                PlayerMoveState.Airborne,
                new PlayerMotorInput { Move = Vector2.right },
                new PlayerMotorContacts { Grounded = true });

            Assert.That(motor.Velocity.x, Is.EqualTo(6f));
            Assert.That(state, Is.EqualTo(PlayerMoveState.Grounded));
            Assert.That(motor.Facing, Is.EqualTo(1));
        }

        [Test]
        public void 地面起跳_进入空中()
        {
            var motor = new PlayerMotor();
            PlayerMoveState state = motor.Tick(
                DeltaTime,
                PlayerMoveState.Airborne,
                new PlayerMotorInput { JumpPressed = true, JumpHeld = true },
                new PlayerMotorContacts { Grounded = true });

            Assert.That(motor.Velocity.y, Is.EqualTo(11.5f));
            Assert.That(state, Is.EqualTo(PlayerMoveState.Airborne));
        }

        [Test]
        public void 满按跳跃_轨迹符合默认数值()
        {
            MeasureFullJumpTrajectory(out float peakHeight, out float peakTime, out float descentDuration);

            Assert.That(peakHeight, Is.InRange(1.90f, 2.10f));
            Assert.That(peakTime, Is.InRange(0.34f, 0.38f));
            Assert.That(descentDuration, Is.InRange(0.26f, 0.30f));
        }

        [Test]
        public void 离地后土狼时间内仍可跳()
        {
            var motor = new PlayerMotor();
            PlayerMoveState state = motor.Tick(
                DeltaTime,
                PlayerMoveState.Airborne,
                default,
                new PlayerMotorContacts { Grounded = true });
            state = TickFor(motor, state, 0.119f, default, default);
            state = motor.Tick(
                0f,
                state,
                new PlayerMotorInput { JumpPressed = true, JumpHeld = true },
                default);

            Assert.That(motor.Velocity.y, Is.EqualTo(11.5f));
            Assert.That(state, Is.EqualTo(PlayerMoveState.Airborne));
        }

        [Test]
        public void 土狼过期后不能跳()
        {
            var motor = new PlayerMotor();
            PlayerMoveState state = motor.Tick(
                DeltaTime,
                PlayerMoveState.Airborne,
                default,
                new PlayerMotorContacts { Grounded = true });
            state = TickFor(motor, state, 0.121f, default, default);
            state = motor.Tick(
                0f,
                state,
                new PlayerMotorInput { JumpPressed = true, JumpHeld = true },
                default);

            Assert.That(motor.Velocity.y, Is.Not.EqualTo(11.5f));
            Assert.That(state, Is.EqualTo(PlayerMoveState.Airborne));
        }

        [Test]
        public void 空中松开跳跃_使用松开重力()
        {
            var motor = new PlayerMotor();
            PlayerMoveState state = motor.Tick(
                DeltaTime,
                PlayerMoveState.Airborne,
                new PlayerMotorInput { JumpPressed = true, JumpHeld = true },
                new PlayerMotorContacts { Grounded = true });

            state = motor.Tick(
                DeltaTime,
                state,
                default,
                default);

            Assert.That(motor.Velocity.y, Is.EqualTo(9.7f).Within(0.0001f));
        }

        [Test]
        public void 空中按住跳跃_使用上升重力()
        {
            var motor = new PlayerMotor();
            PlayerMoveState state = motor.Tick(
                DeltaTime,
                PlayerMoveState.Airborne,
                new PlayerMotorInput { JumpPressed = true, JumpHeld = true },
                new PlayerMotorContacts { Grounded = true });

            state = motor.Tick(
                DeltaTime,
                state,
                new PlayerMotorInput { JumpHeld = true },
                default);

            Assert.That(motor.Velocity.y, Is.EqualTo(10.804f).Within(0.0001f));
        }

        [Test]
        public void 下落时_使用下落重力()
        {
            var motor = new PlayerMotor();
            motor.Reset(new Vector2(0f, -0.5f));

            motor.Tick(DeltaTime, PlayerMoveState.Airborne, default, default);

            Assert.That(motor.Velocity.y, Is.EqualTo(-1.52f).Within(0.0001f));
        }

        [Test]
        public void 地面冲刺_持续后离开冲刺()
        {
            var motor = new PlayerMotor();
            PlayerMoveState state = motor.Tick(
                DeltaTime,
                PlayerMoveState.Airborne,
                new PlayerMotorInput { DashPressed = true },
                new PlayerMotorContacts { Grounded = true });

            Assert.That(state, Is.EqualTo(PlayerMoveState.Dashing));
            Assert.That(motor.Velocity.x, Is.EqualTo(16f));

            state = TickFor(motor, state, 0.16f, default, new PlayerMotorContacts { Grounded = true });
            Assert.That(state, Is.Not.EqualTo(PlayerMoveState.Dashing));
        }

        [Test]
        public void 冲刺刚结束立刻再按不进入冲刺()
        {
            var motor = new PlayerMotor();
            PlayerMoveState state = motor.Tick(
                DeltaTime,
                PlayerMoveState.Airborne,
                new PlayerMotorInput { DashPressed = true },
                new PlayerMotorContacts { Grounded = true });
            state = TickFor(motor, state, 0.16f, default, new PlayerMotorContacts { Grounded = true });

            state = motor.Tick(
                DeltaTime,
                state,
                new PlayerMotorInput { DashPressed = true },
                new PlayerMotorContacts { Grounded = true });

            Assert.That(state, Is.Not.EqualTo(PlayerMoveState.Dashing));
        }

        [Test]
        public void 空中只能冲刺一次()
        {
            var motor = new PlayerMotor();
            PlayerMoveState state = motor.Tick(
                DeltaTime,
                PlayerMoveState.Airborne,
                default,
                new PlayerMotorContacts { Grounded = true });
            state = motor.Tick(
                DeltaTime,
                state,
                default,
                new PlayerMotorContacts { Grounded = false });

            state = motor.Tick(
                DeltaTime,
                state,
                new PlayerMotorInput { DashPressed = true },
                new PlayerMotorContacts { Grounded = false });
            Assert.That(state, Is.EqualTo(PlayerMoveState.Dashing));

            state = TickFor(motor, state, 0.16f, default, new PlayerMotorContacts { Grounded = false });
            state = TickFor(motor, state, 0.40f, default, new PlayerMotorContacts { Grounded = false });

            state = motor.Tick(
                DeltaTime,
                state,
                new PlayerMotorInput { DashPressed = true },
                new PlayerMotorContacts { Grounded = false });
            Assert.That(state, Is.Not.EqualTo(PlayerMoveState.Dashing));
        }

        [Test]
        public void 梯子向上进入攀爬()
        {
            var motor = new PlayerMotor();
            PlayerMoveState state = motor.Tick(
                DeltaTime,
                PlayerMoveState.Airborne,
                new PlayerMotorInput { Move = Vector2.up },
                new PlayerMotorContacts { OnLadder = true });

            Assert.That(state, Is.EqualTo(PlayerMoveState.Climbing));
            Assert.That(motor.Velocity.y, Is.EqualTo(4f));
            Assert.That(motor.Velocity.x, Is.EqualTo(0f));
        }

        [Test]
        public void 攀爬中跳跃离开梯子()
        {
            var motor = new PlayerMotor();
            PlayerMoveState state = motor.Tick(
                DeltaTime,
                PlayerMoveState.Airborne,
                new PlayerMotorInput { Move = Vector2.up },
                new PlayerMotorContacts { OnLadder = true });

            state = motor.Tick(
                DeltaTime,
                state,
                new PlayerMotorInput { JumpPressed = true, Move = Vector2.up },
                new PlayerMotorContacts { OnLadder = true });

            Assert.That(state, Is.Not.EqualTo(PlayerMoveState.Climbing));
            Assert.That(motor.Velocity.y, Is.EqualTo(11.5f));
        }

        [Test]
        public void 顶头时_立即进入下落()
        {
            var motor = new PlayerMotor();
            motor.Reset(new Vector2(0f, 2f));

            PlayerMoveState state = motor.Tick(
                DeltaTime,
                PlayerMoveState.Airborne,
                default,
                new PlayerMotorContacts { CeilingHit = true });

            Assert.That(state, Is.EqualTo(PlayerMoveState.Airborne));
            Assert.That(motor.Velocity.y, Is.EqualTo(-1.02f).Within(0.0001f));
        }

        [Test]
        public void 空中向右撞墙_停止水平移动且可反向离墙()
        {
            var motor = new PlayerMotor();
            motor.Reset(new Vector2(6f, 2f));

            PlayerMoveState state = motor.Tick(
                DeltaTime,
                PlayerMoveState.Airborne,
                new PlayerMotorInput { Move = Vector2.right },
                new PlayerMotorContacts { TouchingWallRight = true });

            Assert.That(motor.Velocity.x, Is.Zero);
            Assert.That(motor.Velocity.y, Is.EqualTo(-1.02f).Within(0.0001f));

            motor.Tick(
                DeltaTime,
                state,
                new PlayerMotorInput { Move = Vector2.left },
                new PlayerMotorContacts { TouchingWallRight = true });

            Assert.That(motor.Velocity.x, Is.EqualTo(-6f));
        }

        [Test]
        public void 空中冲刺撞墙_停止冲刺且进入冷却()
        {
            var motor = new PlayerMotor();
            PlayerMoveState state = motor.Tick(
                DeltaTime,
                PlayerMoveState.Airborne,
                default,
                new PlayerMotorContacts { Grounded = true });
            state = motor.Tick(DeltaTime, state, default, default);
            state = motor.Tick(
                DeltaTime,
                state,
                new PlayerMotorInput { DashPressed = true },
                default);

            Assert.That(state, Is.EqualTo(PlayerMoveState.Dashing));

            state = motor.Tick(
                DeltaTime,
                state,
                new PlayerMotorInput { Move = Vector2.left, JumpPressed = true },
                new PlayerMotorContacts { TouchingWallRight = true });

            Assert.That(state, Is.EqualTo(PlayerMoveState.Airborne));
            Assert.That(motor.Velocity.x, Is.Zero);
            Assert.That(motor.Velocity.y, Is.EqualTo(-1.02f).Within(0.0001f));

            state = motor.Tick(
                DeltaTime,
                state,
                new PlayerMotorInput { DashPressed = true },
                default);

            Assert.That(state, Is.Not.EqualTo(PlayerMoveState.Dashing));
        }

        private static void MeasureFullJumpTrajectory(
            out float peakHeight,
            out float peakTime,
            out float descentDuration)
        {
            var motor = new PlayerMotor();
            PlayerMoveState state = motor.Tick(
                DeltaTime,
                PlayerMoveState.Airborne,
                new PlayerMotorInput { JumpPressed = true, JumpHeld = true },
                new PlayerMotorContacts { Grounded = true });
            float height = motor.Velocity.y * DeltaTime;
            float elapsed = DeltaTime;
            peakHeight = height;
            peakTime = elapsed;
            descentDuration = 0f;

            for (int i = 0; i < 100; i++)
            {
                state = motor.Tick(
                    DeltaTime,
                    state,
                    new PlayerMotorInput { JumpHeld = true },
                    default);
                elapsed += DeltaTime;
                height += motor.Velocity.y * DeltaTime;
                if (height > peakHeight)
                {
                    peakHeight = height;
                    peakTime = elapsed;
                }

                if (elapsed > peakTime && height <= 0f)
                {
                    descentDuration = elapsed - peakTime;
                    return;
                }
            }

            Assert.Fail("跳跃未在预期时间内回到起跳高度。");
        }
        private static PlayerMoveState TickFor(
            PlayerMotor motor,
            PlayerMoveState state,
            float duration,
            PlayerMotorInput input,
            PlayerMotorContacts contacts)
        {
            float remaining = duration;
            while (remaining > 0.0001f)
            {
                float step = Mathf.Min(DeltaTime, remaining);
                state = motor.Tick(step, state, input, contacts);
                remaining -= step;
            }

            return state;
        }
    }
}

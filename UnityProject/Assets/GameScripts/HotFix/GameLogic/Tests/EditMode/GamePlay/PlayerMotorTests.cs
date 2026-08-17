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
                new PlayerMotorInput { JumpPressed = true },
                new PlayerMotorContacts { Grounded = true });

            Assert.That(motor.Velocity.y, Is.EqualTo(10f));
            Assert.That(state, Is.EqualTo(PlayerMoveState.Airborne));
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
            state = TickFor(motor, state, 0.04f, default, new PlayerMotorContacts { Grounded = false });
            state = motor.Tick(
                DeltaTime,
                state,
                new PlayerMotorInput { JumpPressed = true },
                new PlayerMotorContacts { Grounded = false });

            Assert.That(motor.Velocity.y, Is.EqualTo(10f));
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
            state = TickFor(motor, state, 0.10f, default, new PlayerMotorContacts { Grounded = false });
            state = motor.Tick(
                DeltaTime,
                state,
                new PlayerMotorInput { JumpPressed = true },
                new PlayerMotorContacts { Grounded = false });

            Assert.That(motor.Velocity.y, Is.Not.EqualTo(10f));
            Assert.That(state, Is.EqualTo(PlayerMoveState.Airborne));
        }

        [Test]
        public void 空中松跳_截断上升速度()
        {
            var motor = new PlayerMotor();
            PlayerMoveState state = motor.Tick(
                DeltaTime,
                PlayerMoveState.Airborne,
                new PlayerMotorInput { JumpPressed = true },
                new PlayerMotorContacts { Grounded = true });
            float risingVelocity = motor.Velocity.y;

            state = motor.Tick(
                DeltaTime,
                state,
                new PlayerMotorInput { JumpReleased = true },
                new PlayerMotorContacts { Grounded = false });

            Assert.That(motor.Velocity.y, Is.EqualTo(risingVelocity * 0.45f).Within(0.0001f));
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
            Assert.That(motor.Velocity.y, Is.EqualTo(10f));
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

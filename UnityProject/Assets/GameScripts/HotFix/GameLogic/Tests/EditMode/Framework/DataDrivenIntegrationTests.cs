using System;
using EF.DataDriven;
using NUnit.Framework;

namespace GameLogic.Tests
{
    /// <summary>
    /// 验证导入 EF 的数据驱动运行时保留事件、绑定和观察行为。
    /// </summary>
    [TestFixture]
    public sealed class DataDrivenIntegrationTests
    {
        /// <summary>
        /// 值节点提交变更后，绑定回调应收到当前值与上一个值。
        /// </summary>
        [Test]
        public void ValueBind_提交变更_提供当前值和前值()
        {
            var node = new RamDataInt(null, out IRamDataCtrl controller);
            int callbackCount = 0;
            int currentValue = -1;
            int previousValue = -1;
            IDisposable binding = node.Bind((current, previous) =>
            {
                callbackCount++;
                currentValue = current;
                previousValue = previous;
            });

            try
            {
                Assert.That(callbackCount, Is.EqualTo(1));
                Assert.That(currentValue, Is.EqualTo(0));
                Assert.That(previousValue, Is.EqualTo(0));

                node.Value = 8;
                node.CheckAndNotifyChanged();

                Assert.That(callbackCount, Is.EqualTo(2));
                Assert.That(currentValue, Is.EqualTo(8));
                Assert.That(previousValue, Is.EqualTo(0));
            }
            finally
            {
                binding.Dispose();
                controller.Dispose();
            }
        }

        /// <summary>
        /// 观察依赖节点后，提交节点变更应重新执行观察回调。
        /// </summary>
        [Test]
        public void Watch_依赖节点变更_重新执行回调()
        {
            var node = new RamDataInt(null, out IRamDataCtrl controller);
            int callbackCount = 0;
            int observedValue = -1;
            IDisposable watch = RamDataNodeBase.Watch(() =>
            {
                callbackCount++;
                observedValue = node.Value;
            });

            try
            {
                node.Value = 16;
                node.CheckAndNotifyChanged();

                Assert.That(callbackCount, Is.EqualTo(2));
                Assert.That(observedValue, Is.EqualTo(16));
            }
            finally
            {
                watch.Dispose();
                controller.Dispose();
            }
        }
    }
}

using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

namespace EF.UI.WFramework.Utils {

	public sealed class DefaultTimer : TimerCore<float, float> {
		public DefaultTimer(ITimerCalculator<float, float> calculator) : base(calculator) { }
	}

	public sealed class RealtimeTimer : TimerCore<DateTime, TimeSpan> {
		public RealtimeTimer(ITimerCalculator<DateTime, TimeSpan> calculator) : base(calculator) { }
	}

	public static class Timer {

		public static DefaultTimer Default {
			get {
				if (s_default == null) {
					DefaultTimerCalculator calc = new DefaultTimerCalculator();
					s_default = new DefaultTimer(calc);
					Action loop = async () => {
						while (true) {
							await UniTask.NextFrame();
							calc.Tick(Time.deltaTime);
							s_default.Tick();
						}
					};
					loop();
				}
				return s_default;
			}
		}

		public static RealtimeTimer DefaultRealTime {
			get {
				if (s_default_realtime == null) {
					DateTime mark = DateTime.UtcNow;
					s_default_realtime = new RealtimeTimer(new RealtimeTimerCalculator());
					Action loop = async () => {
						while (true) {
							await UniTask.NextFrame();
							s_default_realtime.Tick();
						}
					};
					loop();
				}
				return s_default_realtime;
			}
		}

		private static DefaultTimer s_default;
		private static RealtimeTimer s_default_realtime;

		private class DefaultTimerCalculator : ITimerCalculator<float, float> {
			private float mTimer = 0f;
			float ITimerCalculator<float, float>.Add(float time, float delta) {
				return time + delta;
			}
			float ITimerCalculator<float, float>.Subtract(float time, float delta) {
				return time - delta;
			}
			int ITimerCalculator<float, float>.Compare(float a, float b) {
				if (a == b) { return 0; }
				return a < b ? -1 : 1;
			}
			float ITimerCalculator<float, float>.GetNow() {
				return mTimer;
			}
			public void Tick(float delta) {
				mTimer += delta;
			}
		}

		private class RealtimeTimerCalculator : ITimerCalculator<DateTime, TimeSpan> {
			DateTime ITimerCalculator<DateTime, TimeSpan>.Add(DateTime time, TimeSpan delta) {
				return time + delta;
			}
			TimeSpan ITimerCalculator<DateTime, TimeSpan>.Subtract(DateTime time, DateTime delta) {
				return time - delta;
			}
			int ITimerCalculator<DateTime, TimeSpan>.Compare(DateTime a, DateTime b) {
				return DateTime.Compare(a, b);
			}
			DateTime ITimerCalculator<DateTime, TimeSpan>.GetNow() {
				return DateTime.Now;
			}
		}

	}

}

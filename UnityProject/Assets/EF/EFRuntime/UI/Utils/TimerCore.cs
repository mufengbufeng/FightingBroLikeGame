using EF.UI.WFramework.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace EF.UI.WFramework.Utils {

	public delegate void TimerDelegate();

	public interface ITimerCalculator<T, D> {
		T GetNow();
		T Add(T time, D delta);
		D Subtract(T time, T delta);
		int Compare(T a, T b);
	}

	public struct TimerHandler : IDisposable {
		public uint _timer_id;
		public Func<uint, bool> _check_running;
		public Action<uint> _unregister;
		public void Dispose() {
			if (_unregister == null) { return; }
			_unregister(_timer_id);
			_unregister = null;
			_check_running = null;

		}
		public bool IsRunning() {
			if (_check_running == null) { return false; }
			return _check_running(_timer_id);
		}
	}

	/// <summary>
	/// Core logic of a timer.
	/// </summary>
	/// <typeparam name="T">Type for Time.</typeparam>
	/// <typeparam name="D">Type for DeltaTime.</typeparam>
	public abstract class TimerCore<T, D> {

		public delegate void TimerDelegateWithOverflow(D overflow);

		public struct Param {

			public static Param Delay(D delay, TimerDelegate onTimer) {
				return new Param() {
					_mode_delay = true,
					_delay_time = delay,
					_times = 1,
					_on_timer_1 = onTimer
				};
			}

			public static Param Delay(D delay, TimerDelegateWithOverflow onTimer) {
				return new Param() {
					_mode_delay = true,
					_delay_time = delay,
					_times = 1,
					_on_timer_2 = onTimer
				};
			}

			public Param SetInterval(D interval) {
				_interval = interval;
				_times = -1;
				return this;
			}

			public Param SetInterval(D interval, int times) {
				_interval = interval;
				_times = times;
				return this;
			}

			/// <summary>
			/// Only works when using string key.<br />
			/// 'onCanceled' will be invoked when a pending timer is removed because of a timer-adding with the same key.
			/// </summary>
			/// <param name="onCanceled">Callback when a timer is removed because of a same-key timer-adding.</param>
			/// <returns>The Param object itself.</returns>
			public Param SetOnCanceled(TimerDelegate onCanceled) {
				_on_canceled = onCanceled;
				return this;
			}

			#region parameters
			public bool _mode_delay;
			public T _target_time;
			public D _delay_time;
			public D _interval;
			public int _times;
			public TimerDelegate _on_timer_1;
			public TimerDelegateWithOverflow _on_timer_2;
			public TimerDelegate _on_canceled;
			#endregion
		}

		public TimerCore(ITimerCalculator<T, D> calculator) {
			if (calculator == null) { throw new ArgumentNullException(nameof(calculator)); }
			mCalculator = calculator;
			mCheckRunning = CheckRunning;
			mUnregister = Unregister;
		}

		public void Tick() {
			DoUpdate(mCalculator.GetNow());
		}

		public TimerHandler Add(D delay, TimerDelegate onTimer) {
			uint id = AddInternal(null, Param.Delay(delay, onTimer));
			return new TimerHandler() {
				_timer_id = id,
				_check_running = mCheckRunning,
				_unregister = mUnregister
			};
		}

		public TimerHandler Add(Param param) {
			uint id = AddInternal(null, param);
			return new TimerHandler() {
				_timer_id = id,
				_check_running = mCheckRunning,
				_unregister = mUnregister
			};
		}

		public void Add(string key, Param param) {
			if (string.IsNullOrEmpty(key)) {
				throw new ArgumentNullException(nameof(key));
			}
			AddInternal(key, param);
		}

		public bool IsRunning(string key) {
			if (string.IsNullOrEmpty(key)) { return false; }
			if (!mKeySet.TryGetValue(key, out int id)) { return false; }
			return mQueue.Contains(id);
		}

		public bool Remove(string key) {
			if (string.IsNullOrEmpty(key)) { return false; }
			if (!mKeySet.TryGetValue(key, out int id)) { return false; }
			return mQueue.RemoveFromQueue(id);
		}

		private ITimerCalculator<T, D> mCalculator;

		private Func<uint, bool> mCheckRunning;
		private Action<uint> mUnregister;

		private bool CheckRunning(uint id) {
			return mQueue.Contains((int)id);
		}

		private void Unregister(uint id) {
			mQueue.RemoveFromQueue((int)id);
		}

		private uint mIdGen;

		private KeyedPriorityQueue<int, Param, T> mQueue = new KeyedPriorityQueue<int, Param, T>();
		private Dictionary<string, int> mKeySet = new Dictionary<string, int>();

		private uint AddInternal(string key, Param param) {
			int id;
			if (key == null) {
				id = (int)(++mIdGen);
			} else {
				if (!mKeySet.TryGetValue(key, out id)) {
					id = -1 - mKeySet.Count;
					mKeySet.Add(key, id);
				}
				if (mQueue.RemoveFromQueue(id, out Param d, out T p)) {
					if (d._on_canceled != null) {
						try {
							d._on_canceled();
						} catch (Exception e) {
							Debug.LogException(e);
						}
					}
				}
			}
			T target = param._mode_delay ?
				mCalculator.Add(mCalculator.GetNow(), param._delay_time) :
				param._target_time;
			mQueue.Enqueue(id, param, target);
			return id <= 0 ? 0 : (uint)id;
		}

		private void DoUpdate(T now) {
			while (mQueue.Count > 0) {
				mQueue.Peek(out int id, out T p);
				if (mCalculator.Compare(p, now) > 0) { break; }
				Param data = mQueue.Dequeue(out id, out p);
				if (data._times != 1) {
					if (data._times > 0) { data._times--; }
					mQueue.Enqueue(id, data, mCalculator.Add(p, data._interval));
				}
				if (data._on_timer_1 != null) {
					try {
						data._on_timer_1();
					} catch (Exception e) {
						Debug.LogException(e);
					}
				}
				if (data._on_timer_2 != null) {
					try {
						data._on_timer_2(mCalculator.Subtract(now, p));
					} catch (Exception e) {
						Debug.LogException(e);
					}
				}
			}
		}

	}

}

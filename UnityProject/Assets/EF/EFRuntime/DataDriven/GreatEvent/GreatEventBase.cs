using System;
using System.Collections.Generic;

namespace EF.DataDriven {

	public abstract class GreatEventBase<T> where T : Delegate {

		private List<T> mHandlers;
		private List<Invoking> mInvokings;
		private Action<Invoking> mOnInvoked;
		private bool mDisposed = false;

		public GreatEventBase(out IGreatEventCtrl ctrl) {
			mHandlers = s_cached_handlers.Count > 0 ? s_cached_handlers.Dequeue() : new List<T>();
			mInvokings = s_cached_invokings.Count > 0 ? s_cached_invokings.Dequeue() : new List<Invoking>();
			mHandlers.Clear();
			mInvokings.Clear();
			mOnInvoked = OnInvoked;
			ctrl = new Ctrl(this);
		}

		public void Add(T handler) {
			if (mDisposed) { return; }
			if (handler == null) { return; }
			mHandlers.Add(handler);
		}

		public bool Remove(T handler) {
			if (mHandlers == null) { return false; }
			if (handler == null) { return false; }
			if (mInvokings.Count > 0) {
				int i = mHandlers.IndexOf(handler);
				if (i < 0) { return false; }
				mHandlers[i] = null;
				return true;
			}
			return mHandlers.Remove(handler);
		}

		protected Invoking BeginInvoke() {
			if (mDisposed) { return null; }
			Invoking ret = Invoking.Get(mHandlers, mOnInvoked);
			mInvokings.Add(ret);
			return ret;
		}

		private void OnInvoked(Invoking invoking) {
			bool flag = mInvokings.Remove(invoking);
			Invoking.Cache(invoking);
			if (!flag || mInvokings.Count > 0) { return; }
			if (mDisposed) {
				DoDispose();
			} else {
				for (int i = mHandlers.Count - 1; i >= 0; i--) {
					if (mHandlers[i] == null) {
						mHandlers.RemoveAt(i);
					}
				}
			}
		}

		private void RemoveAll() {
			if (mHandlers == null) { return; }
			if (mInvokings.Count <= 0) {
				mHandlers.Clear();
				return;
			}
			for (int i = mHandlers.Count - 1; i >= 0; i--) {
				mHandlers[i] = null;
			}
		}

		private void Dispose() {
			if (mDisposed) { return; }
			mDisposed = true;
			if (mInvokings.Count <= 0) { DoDispose(); }
		}

		private void DoDispose() {
			if (mHandlers != null) {
				mHandlers.Clear();
				s_cached_handlers.Enqueue(mHandlers);
				mHandlers = null;
			}
			if (mInvokings != null) {
				mInvokings.Clear();
				s_cached_invokings.Enqueue(mInvokings);
				mInvokings = null;
			}
		}

		private static Queue<List<T>> s_cached_handlers = new Queue<List<T>>();
		private static Queue<List<Invoking>> s_cached_invokings = new Queue<List<Invoking>>();

		protected class Invoking {
			private List<T> mHandlers;
			private int mIndex;
			private int mCount;
			private Action<Invoking> mOnFinish;
			public T Next() {
				if (mCount < 0) { return null; }
				while (++mIndex < mCount) {
					T ret = mHandlers[mIndex];
					if (ret != null) { return ret; }
				}
				mOnFinish(this);
				return null;
			}
			private Invoking() { }
			private static Queue<Invoking> s_cached = new Queue<Invoking>();
			public static Invoking Get(List<T> handlers, Action<Invoking> onFinish) {
				Invoking ret = s_cached.Count > 0 ? s_cached.Dequeue() : new Invoking();
				ret.mHandlers = handlers;
				ret.mIndex = -1;
				ret.mCount = handlers.Count;
				ret.mOnFinish = onFinish;
				return ret;
			}
			public static void Cache(Invoking ins) {
				if (ins == null) { return; }
				ins.mHandlers = null;
				ins.mIndex = -1;
				ins.mCount = -1;
				ins.mOnFinish = null;
				s_cached.Enqueue(ins);
			}
		}

		private class Ctrl : IGreatEventCtrl {
			private GreatEventBase<T> mIns;
			public Ctrl(GreatEventBase<T> ins) { mIns = ins; }
			void IGreatEventCtrl.RemoveAll() { if (mIns != null) { mIns.RemoveAll(); } }
			void IDisposable.Dispose() { if (mIns != null) { mIns.Dispose(); } }
		}

	}

	public interface IGreatEventCtrl : IDisposable {
		void RemoveAll();
	}

}
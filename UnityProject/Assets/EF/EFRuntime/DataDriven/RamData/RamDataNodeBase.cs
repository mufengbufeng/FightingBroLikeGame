using System;
using System.Collections.Generic;
using UnityEngine;

namespace EF.DataDriven {

	public enum eRamDataNodeChangedType { None, Init, Changed }

	public interface IRamDataCtrl : IDisposable {
		eRamDataNodeChangedType CollectAndNotifyChanged();
		void Reset();
	}

	public interface IRamDataStructCtrl {
		void HandleEventBubbling();
	}

	public abstract class RamDataNodeBase {

		public RamDataNodeBase(IRamDataStructCtrl parent, out IRamDataCtrl ctrl) {
			mParent = parent;
			ctrl = new RamDataCtrl(this);
		}

		public void CheckAndNotifyChanged() {
			eRamDataNodeChangedType t = CollectAndNotifyChanged();
			if (t == eRamDataNodeChangedType.None) { return; }
			if (mParent != null) { mParent.HandleEventBubbling(); }
			TryInvokeWatches();
		}

		protected IRamDataStructCtrl mParent { get; private set; }

		protected abstract eRamDataNodeChangedType CollectAndNotifyChanged();

		protected abstract void Reset();

		protected abstract void Dispose();

		private class RamDataCtrl : IRamDataCtrl {

			private RamDataNodeBase mTarget;

			public RamDataCtrl(RamDataNodeBase target) {
				mTarget = target;
			}

			public void Reset() { mTarget.Reset(); }

			public eRamDataNodeChangedType CollectAndNotifyChanged() {
				return mTarget.CollectAndNotifyChanged();
			}

			public void Dispose() {
				if (mTarget.mInternalOnChangedCtrl != null) {
					mTarget.mInternalOnChangedCtrl.Dispose();
					mTarget.mInternalOnChanged = null;
					mTarget.mInternalOnChangedCtrl = null;
				}
				mTarget.Dispose();
			}

		}

		protected void TryDispatchInternalChanged() {
			if (mInternalOnChanged == null) { return; }
			mInternalOnChanged.Invoke();
		}

		private GreatEvent mInternalOnChanged;
		private IGreatEventCtrl mInternalOnChangedCtrl;

		protected void CollectUsage() {
			if (s_watch_nodes.Count <= 0) { return; }
			List<RamDataNodeBase> nodes = s_watch_nodes.Peek();
			if (!nodes.Contains(this)) { nodes.Add(this); }
		}

		// watching

		public static IDisposable Watch(Action callback) {
			if (callback == null) { throw new ArgumentNullException(nameof(callback)); }
			s_watch_nodes.Push(GetWatchingNodes());
			bool success = true;
			try { callback.Invoke(); } catch (Exception e) { Debug.LogException(e); success = false; }
			List<RamDataNodeBase> nodes = s_watch_nodes.Pop();
			IDisposable ret = s_fake;
			int n = nodes.Count;
			if (success && n > 0) {
				ret = new W(nodes, callback);
			} else {
				CacheWatchingNodes(nodes);
			}
			return ret;
		}

		private static List<W> s_watchings = new List<W>();
		private static List<W> s_watchings_copy = new List<W>();

		private static Stack<List<RamDataNodeBase>> s_watch_nodes = new Stack<List<RamDataNodeBase>>();

		private static void TryInvokeWatches() {
			s_watchings_copy.Clear();
			s_watchings_copy.AddRange(s_watchings);
			for (int i = s_watchings_copy.Count - 1; i >= 0; i--) {
				W watching = s_watchings_copy[i];
				if (!s_watchings.Contains(watching)) { continue; }
				watching.TryInvokeChanged();
			}
			s_watchings_copy.Clear();
		}

		private class W : IDisposable {

			private int mCollectChanged = 0;
			private Action mOnChanged;
			private List<RamDataNodeBase> mNodes;
			private Action mCallback;

			public W(List<RamDataNodeBase> nodes, Action callback) {
				mNodes = nodes;
				mCallback = callback;
				int n = nodes.Count;
				mOnChanged = OnChanged;
				for (int i = 0; i < n; i++) {
					RamDataNodeBase node = nodes[i];
					if (node.mInternalOnChanged == null) {
						node.mInternalOnChanged = new GreatEvent(out node.mInternalOnChangedCtrl);
					}
					node.mInternalOnChanged.Add(mOnChanged);
				}
				s_watchings.Add(this);
			}

			public void TryInvokeChanged() {
				if (mCollectChanged <= 0 || mCallback == null) { return; }
				mCollectChanged = 0;
				try { mCallback.Invoke(); } catch (Exception e) { Debug.LogException(e); }
			}

			void IDisposable.Dispose() {
				if (mNodes == null) { return; }
				for (int i = mNodes.Count - 1; i >= 0; i--) {
					RamDataNodeBase node = mNodes[i];
					if (node.mInternalOnChanged != null) {
						node.mInternalOnChanged.Remove(mOnChanged);
					}
				}
				s_watchings.Remove(this);
				CacheWatchingNodes(mNodes);
				mNodes = null;
				mCallback = null;
			}

			private void OnChanged() {
				mCollectChanged++;
			}

		}

		private static Queue<List<RamDataNodeBase>> s_cached_watchings = new Queue<List<RamDataNodeBase>>();

		public static List<RamDataNodeBase> GetWatchingNodes() {
			return s_cached_watchings.Count > 0 ? s_cached_watchings.Dequeue() : new List<RamDataNodeBase>();
		}

		public static void CacheWatchingNodes(List<RamDataNodeBase> list) {
			list.Clear();
			s_cached_watchings.Enqueue(list);
		}

		private class F : IDisposable { void IDisposable.Dispose() { } }

		private static IDisposable s_fake = new F();

	}

}

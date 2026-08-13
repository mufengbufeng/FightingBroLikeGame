using EF.UI.WFramework.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace EF.UI.WFramework {

	public partial class UIManager {

		private class FocusMgr {

			public IUIFocusable Current { get; private set; }

			private int mHoldingFocusChange = 0;

			private KeyedPriorityQueue<IUIFocusable, IUIFocusable, int> mFocuses;
			private Dictionary<IUIDynamicFocusable, DynamicFocusAgent> mDynamicAgents = new Dictionary<IUIDynamicFocusable, DynamicFocusAgent>();

			public FocusMgr() {
				mFocuses = new KeyedPriorityQueue<IUIFocusable, IUIFocusable, int>(
					(int p1, int p2) => { return Mathf.Clamp(p2 - p1, -1, 1); }
				);
				Current = null;
			}

			public bool AddFocusable(IUIFocusable obj, int order) {
				if (obj == null) { return false; }
				IUIDynamicFocusable dyn = obj as IUIDynamicFocusable;
				if (dyn == null) { return RequestFocusable(obj, order); }
				if (mDynamicAgents.ContainsKey(dyn)) { return false; }
				DynamicFocusAgent agent = new DynamicFocusAgent(this, dyn, order);
				mDynamicAgents.Add(dyn, agent);
				dyn.SetDynamicFocusAgent(agent);
				return true;
			}

			public bool RemoveFocusable(IUIFocusable obj) {
				if (obj == null) { return false; }
				IUIDynamicFocusable dyn = obj as IUIDynamicFocusable;
				bool ret = false;
				if (dyn != null) {
					ret = mDynamicAgents.Remove(dyn);
				}
				if (ReleaseFocusable(obj)) { ret = true; }
				return ret;
			}

			public void HoldDispatchFocusChange() {
				mHoldingFocusChange++;
			}

			public void TryDispatchFocusChange() {
				if (mHoldingFocusChange <= 0) { return; }
				mHoldingFocusChange--;
				if (mHoldingFocusChange > 0) { return; }
				IUIFocusable current = mFocuses.Count > 0 ? mFocuses.Peek() : null;
				if (Current == current) { return; }
				if (Current != null) {
					try { Current.OnLoseFocus(); } catch (Exception e) { Debug.LogException(e); }
				}
				if (current != null) {
					try { current.OnGetFocus(); } catch (Exception e) { Debug.LogException(e); }
				}
				Current = current;
			}

			private bool RequestFocusable(IUIFocusable obj, int order) {
				if (obj == null) { return false; }
				if (mFocuses.Contains(obj)) { return false; }
				mFocuses.Enqueue(obj, obj, order);
				return true;
			}

			private bool ReleaseFocusable(IUIFocusable obj) {
				if (mFocuses.Count <= 0) { return false; }
				if (!mFocuses.RemoveFromQueue(obj)) { return false; }
				return true;
			}

			private class DynamicFocusAgent : IUILogicDynamicFocusAgent {
				private FocusMgr mFocusMgr;
				private IUIDynamicFocusable mFocusable;
				private int mOrder;
				public DynamicFocusAgent(FocusMgr focusMgr, IUIDynamicFocusable focusable, int order) {
					mFocusMgr = focusMgr;
					mFocusable = focusable;
					mOrder = order;
				}
				public bool RequireFocus() {
					if (!mFocusMgr.mDynamicAgents.ContainsKey(mFocusable)) { return false; }
					mFocusMgr.HoldDispatchFocusChange();
					bool ret = mFocusMgr.RequestFocusable(mFocusable, mOrder);
					mFocusMgr.TryDispatchFocusChange();
					return ret;
				}
				public bool ReleaseFocus() {
					if (!mFocusMgr.mDynamicAgents.ContainsKey(mFocusable)) { return false; }
					mFocusMgr.HoldDispatchFocusChange();
					bool ret = mFocusMgr.ReleaseFocusable(mFocusable);
					mFocusMgr.TryDispatchFocusChange();
					return ret;
				}
			}

		}

	}

}
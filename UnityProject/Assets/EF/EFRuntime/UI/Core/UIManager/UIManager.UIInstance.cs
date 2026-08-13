using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace EF.UI.WFramework {

	public partial class UIManager {

		private const float DEFAULT_UI_PREPARE_TIMEOUT = 1.0f;

		private abstract class UIInstanceBase { }

		private abstract class UIInstanceBase<T, U> : UIInstanceBase where T : UIInstanceBase<T, U> where U : IUILogicBase {

			public string Id { get; private set; }

			public U Logic { get; private set; }

			protected UIInstanceBase() { }

			public bool Showing { get { return mShowing; } }

			public bool Opened { get { return mState == eUIState.Opened; } }

			public async void Start(int baseSortingOrder, float posZ, IUIEventHandler handler, Action<T> onShown) {
				IUILoader loader = s_uiloader;
				if (loader == null) {
					return;
				}

				mEventHandler = handler;
				mOnShown = onShown;
				BeginLoading();
				mState = eUIState.Preparing;
				float timeout = DEFAULT_UI_PREPARE_TIMEOUT;
				bool closeWhenTimeout = false;
				if (Logic.OnPrepareCheck(ref timeout, ref closeWhenTimeout)) {
					DoPrepareTimeoutCheck(timeout);
					DoPrepare();
				} else {
					PrepareDone(ePrepareResult.Success);
				}
				GameObject go = await loader.LoadUIObject(mPrefabPath);
				if (mClearedForShutdown) {
					EndLoading();
					if (go != null) { loader.UnloadUIObject(go); }
					return;
				}
				if (go == null) {
					mLoadResult = -1;
					EndLoading();
					if (mState != eUIState.Closed) { UIManager.CloseGroup(Logic); }
					return;
				}
				if (mState != eUIState.Preparing) {
					EndLoading();
					loader.UnloadUIObject(go);
					return;
				}
				mLoadResult = 1;
				RectTransform rt = go.transform as RectTransform;
				if (rt == null) { rt = go.AddComponent<RectTransform>(); }
				Transform parent = UIParent;
				rt.SetParent(parent);
				Vector3 pos = Vector3.zero;
				Vector3 wp = Root.ParentForUI.TransformPoint(new Vector3(0f, 0f, 10000f));
				Vector3 lp = parent.InverseTransformPoint(wp);
				pos.z = lp.z;
				rt.localRotation = Quaternion.identity;
				rt.localScale = Vector3.one;
				rt.anchorMin = Vector2.zero;
				rt.anchorMax = Vector2.one;
				rt.anchoredPosition3D = pos;
				rt.sizeDelta = Vector2.zero;
				mUI.Init(go, Logic.VisibleOperateType);
				mUI.SetBaseSortingOrder(baseSortingOrder);
				mUI.SetPosZ(posZ);
				if (TryOpenUI() == 0) {
					mUI.DoHide();
				}
			}

			public void ResetPositionZ(float z) {
				mUI.SetPosZ(z);
			}

			public void Hide() {
				if (!mShowing) { return; }
				mShowing = false;
				if (mState == eUIState.Opened) {
					mUI.DoHide();
					try {
						Logic.OnHide();
					} catch (Exception ex) {
						Debug.LogException(ex, mUI.ui);
					}
					if (mEventHandler != null) {
						try { mEventHandler.OnHided(); } catch (Exception ex) { Debug.LogException(ex); }
					}
				}
			}

			public void Resume() {
				if (mShowing) { return; }
				mShowing = true;
				if (mState == eUIState.Opened) {
					mUI.DoShow(false);
					try {
						Logic.OnShow();
					} catch (Exception ex) {
						Debug.LogException(ex, mUI.ui);
					}
					if (mEventHandler != null) {
						try { mEventHandler.OnShown(); } catch (Exception ex) { Debug.LogException(ex); }
					}
				}
			}

			public bool Close() {
				if (mState == eUIState.Closed) { return false; }
				EndLoading();
				if (mShowing) {
					mUI.DoHide();
					if (mState == eUIState.Opened) {
						try {
							Logic.OnHide();
						} catch (Exception ex) {
							Debug.LogException(ex, mUI.ui);
						}
					}
				}
				if (mUI.Inited) {
					if (mState == eUIState.Opened) {
						try {
							Logic.OnClose();
						} catch (Exception ex) {
							Debug.LogException(ex, mUI.ui);
						}
					}
					GameObject ui = mUI.ui;
					mUI.Clear();
					s_uiloader.UnloadUIObject(ui);
					if (mState == eUIState.Opened && mEventHandler != null) {
						try { mEventHandler.OnClosed(); } catch (Exception ex) { Debug.LogException(ex); }
					}
				}
				mState = eUIState.Closed;
				try {
					Logic.OnTerminated();
				} catch (Exception ex) {
					Debug.LogException(ex);
				}
				if (mEventHandler != null) {
					try { mEventHandler.OnTerminated(); } catch (Exception ex) { Debug.LogException(ex); }
				}
				return true;
			}

			public string MutexGroup {
				get {
					if (!mMutexGroupInited) {
						mMutexGroupInited = true;
						mMutexGroup = Logic.MutexGroup;
					}
					return mMutexGroup;
				}
			}

			protected abstract Transform UIParent { get; }

			private enum ePrepareResult { None, Timeout, Success, Fail }

			private enum eUIState { None, Preparing, Opened, Closed }

			private string mUID;
			private string mPrefabPath;

			private eUIState mState;
			private bool mShowing = true;
			private IUIEventHandler mEventHandler;
			private Action<T> mOnShown;
			private IUILoadingOverlay mLoadingOverlay;
			private bool mLoadingOverlayStarted;
			private bool mClearedForShutdown;

			private int mLoadResult;
			private UIPanelInstance mUI = new UIPanelInstance();
			private ePrepareResult mPrepareResult;

			private bool mMutexGroupInited = false;
			private string mMutexGroup;

			private int mAsyncDoings = 0;

			private async void DoPrepareTimeoutCheck(float dur) {
				mAsyncDoings++;
				await UniTask.Delay(TimeSpan.FromSeconds(dur), true);
				mAsyncDoings--;
				if (mClearedForShutdown) { return; }
				PrepareDone(ePrepareResult.Timeout);
			}

			private async void DoPrepare() {
				mAsyncDoings++;
				bool success = await Logic.OnPrepareExecute();
				mAsyncDoings--;
				if (mClearedForShutdown) { return; }
				PrepareDone(success ? ePrepareResult.Success : ePrepareResult.Fail);
			}

			private void PrepareDone(ePrepareResult result) {
				if (mClearedForShutdown) { return; }
				if (mLoadResult != 0 && mPrepareResult != ePrepareResult.None) { return; }
				mPrepareResult = result;
				TryOpenUI();
			}

			private int TryOpenUI() {
				if (mClearedForShutdown) { return -1; }
				if (mPrepareResult == ePrepareResult.None) { return 0; }
				if (mLoadResult == 0) { return 0; }
				EndLoading();
				if (mPrepareResult != ePrepareResult.Success || mLoadResult < 0) {
					UIManager.CloseGroup(Logic);
					return -1;
				}
				mState = eUIState.Opened;
				int baseSortingOrder = mUI.GetBaseSortingOrder();
				try {
					Logic.OnOpen(mUI.ui, baseSortingOrder);
				} catch (Exception ex) {
					Debug.LogException(ex, mUI.ui);
					UIManager.CloseGroup(Logic);
					return -1;
				}
				if (mEventHandler != null) {
					try { mEventHandler.OnOpened(); } catch (Exception ex) { Debug.LogException(ex); }
				}
				if (mShowing) {
					mUI.DoShow(true);
					try {
						Logic.OnShow();
					} catch (Exception ex) {
						Debug.LogException(ex, mUI.ui);
					}
					if (mEventHandler != null) {
						try { mEventHandler.OnShown(); } catch (Exception ex) { Debug.LogException(ex); }
					}
				}
				mOnShown.Invoke(this as T);
				return 1;
			}

			/// <summary>
			/// 记录本次打开实际使用的遮罩，确保替换遮罩后仍可将开始与结束配对。
			/// </summary>
			private void BeginLoading() {
				mLoadingOverlay = s_loading_overlay;
				if (mLoadingOverlay == null) { return; }
				mLoadingOverlayStarted = true;
				mLoadingOverlay.BeginLoading(mUID);
			}

			/// <summary>
			/// 多条异步退出路径汇合到一次性结束操作，避免加载遮罩滞留。
			/// </summary>
			private void EndLoading() {
				if (!mLoadingOverlayStarted) { return; }
				IUILoadingOverlay overlay = mLoadingOverlay;
				mLoadingOverlayStarted = false;
				mLoadingOverlay = null;
				overlay?.EndLoading(mUID);
			}

			#region instance cacheing

			private static LinkedList<T> s_caches = new LinkedList<T>();
			protected static Func<T> s_instance_ctor;

			protected static T InternalGet(string id, string prefabPath, U logic) {
				T ret = null;
				if (s_caches.Count > 0) {
					var node = s_caches.First;
					while (node != null && node.Value.mAsyncDoings > 0) {
						node = node.Next;
					}
					if (node != null) {
						ret = node.Value;
						s_caches.Remove(node);
					}
				}
				if (ret == null) { ret = s_instance_ctor(); }
				ret.Id = id;
				ret.mUID = id + "_" + ret.GetHashCode();
				ret.mPrefabPath = prefabPath;
				ret.Logic = logic;
				ret.mState = eUIState.None;
				ret.mShowing = true;
				ret.mLoadResult = 0;
				ret.mPrepareResult = ePrepareResult.None;
				ret.mMutexGroupInited = false;
				ret.mMutexGroup = null;
				ret.mLoadingOverlay = null;
				ret.mLoadingOverlayStarted = false;
				ret.mClearedForShutdown = false;
				ret.mAsyncDoings = 0;
				return ret;
			}

			public static void Cache(T ins) {
				if (ins == null) { return; }
				s_caches.AddLast(ins);
			}

			public static void ClearCachedInstancesForShutdown() {
				for (LinkedListNode<T> node = s_caches.First; node != null; node = node.Next) {
					node.Value.ClearReferencesForShutdown();
				}
				s_caches.Clear();
			}

			private void ClearReferencesForShutdown() {
				mClearedForShutdown = true;
				EndLoading();
				Id = null;
				Logic = default;
				mUID = null;
				mPrefabPath = null;
				mState = eUIState.Closed;
				mShowing = false;
				mEventHandler = null;
				mOnShown = null;
				mLoadingOverlay = null;
				mLoadingOverlayStarted = false;
				mLoadResult = 0;
				mPrepareResult = ePrepareResult.None;
				mMutexGroupInited = false;
				mMutexGroup = null;
				mUI.Clear();
				ClearSubclassReferences();
			}

			protected virtual void ClearSubclassReferences() { }

			#endregion

		}

		private class UIInstanceStack : UIInstanceBase<UIInstanceStack, IUILogicStack> {

			public int Index { get; private set; }

			public object Group { get; set; }

			private UIInstanceStack() { }

			private int mIsFullScreen = 0;
			public bool IsFullScreen {
				get {
					if (mIsFullScreen == 0) {
						mIsFullScreen = Logic.IsFullScreen ? 1 : -1;
					}
					return mIsFullScreen > 0;
				}
			}

			private int mAllowMultiple = 0;
			public bool AllowMultiple {
				get {
					if (mAllowMultiple == 0) {
						mAllowMultiple = Logic.AllowMultiple ? 1 : -1;
					}
					return mAllowMultiple > 0;
				}
			}

			protected override Transform UIParent {
				get {
					return Root.ParentForUI;
				}
			}

			static UIInstanceStack() {
				s_instance_ctor = () => { return new UIInstanceStack(); };
			}

			public static UIInstanceStack Get(int index, string id, string prefabPath, IUILogicStack logic) {
				UIInstanceStack ret = InternalGet(id, prefabPath, logic);
				ret.Index = index;
				ret.mAllowMultiple = 0;
				ret.mIsFullScreen = 0;
				return ret;
			}

			public static void ClearCacheForShutdown() {
				UIInstanceBase<UIInstanceStack, IUILogicStack>.ClearCachedInstancesForShutdown();
			}

			protected override void ClearSubclassReferences() {
				Index = 0;
				Group = null;
				mAllowMultiple = 0;
				mIsFullScreen = 0;
			}

		}

		private class UIInstanceFixed : UIInstanceBase<UIInstanceFixed, IUILogicFixed> {

			private UIInstanceFixed() { }

			protected override Transform UIParent {
				get {
					return Root.ParentForUI;
				}
			}

			static UIInstanceFixed() {
				s_instance_ctor = () => { return new UIInstanceFixed(); };
			}

			public static UIInstanceFixed Get(string id, string prefabPath, IUILogicFixed logic) {
				UIInstanceFixed ret = InternalGet(id, prefabPath, logic);
				return ret;
			}

			public static void ClearCacheForShutdown() {
				UIInstanceBase<UIInstanceFixed, IUILogicFixed>.ClearCachedInstancesForShutdown();
			}

		}


	}
}

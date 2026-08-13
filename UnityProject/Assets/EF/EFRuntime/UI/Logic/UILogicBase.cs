using Cysharp.Threading.Tasks;
using EF.UI.WFramework;
using EF.UI.WFramework.Utils;
using System;
using UnityEngine;

namespace EF.UI.WFramework
{

public abstract class UILogicBase : IUILogicBase {

	#region properties

	protected virtual string MutexGroup { get { return null; } }

	protected virtual eUIVisibleOperateType VisibleOperateType { get { return eUIVisibleOperateType.LayerMask; } }

	protected virtual string OpenAnim { get { return "open"; } }

	protected virtual string CloseAnim { get { return "close"; } }

	#endregion

	#region lifecircle

	private int mShowing = 0;

	protected bool Showing { get { return mShowing > 0; } }

	protected virtual bool OnCreate(object para) { return true; }

	protected virtual bool OnPrepareCheck(ref float timeout, ref bool closeWhenTimeout) { return false; }

	protected virtual UniTask<bool> OnPrepareExecute() { return new UniTask<bool>(true); }

	protected virtual void OnOpen(GameObject go, int baseSortingOrder) { }

	protected virtual void OnShow(bool first) { }

	protected virtual void OnHide() { }

	protected virtual void OnClose() { }

	protected virtual void OnTerminated() { }

	#endregion

	#region auto disposes

	private AutoDispose mDisposesOnHide = new AutoDispose();
	private AutoDispose mDisposesOnClose = new AutoDispose();

	private bool mOnShowInvoking = false;

	protected void AddAutoDispose(IDisposable disposable) {
		if (disposable == null) { return; }
		if (mOnShowInvoking) {
			mDisposesOnHide.Add(disposable);
		} else {
			mDisposesOnClose.Add(disposable);
		}
	}

	#endregion

	#region interface implementation

	string IUILogicBase.MutexGroup { get { return MutexGroup; } }

	eUIVisibleOperateType IUILogicBase.VisibleOperateType { get { return VisibleOperateType; } }

	bool IUILogicBase.OnCreate(object para) { mShowing = 0; return OnCreate(para); }

	bool IUILogicBase.OnPrepareCheck(ref float timeout, ref bool closeWhenTimeout) {
		return OnPrepareCheck(ref timeout, ref closeWhenTimeout);
	}

	UniTask<bool> IUILogicBase.OnPrepareExecute() {
		return OnPrepareExecute();
	}

	void IUILogicBase.OnOpen(GameObject go, int baseSortingOrder) {
		mRootAnimation = go.GetComponent<Animation>();
		mRootAnimator = go.GetComponent<Animator>();
		OnOpen(go, baseSortingOrder);
		ShowOpenAnim(go);
	}

	void IUILogicBase.OnShow() {
		bool first = mShowing == 0;
		mShowing = 1;
		mOnShowInvoking = true;
		OnShow(first);
		mOnShowInvoking = false;
	}

	void IUILogicBase.OnHide() {
		mShowing = -1;
		OnHide();
		mDisposesOnHide.Dispose();
	}

	void IUILogicBase.OnClose() { OnClose(); mDisposesOnClose.Dispose(); }

	void IUILogicBase.OnTerminated() { OnTerminated(); }

	#endregion

	private Animation mRootAnimation;
	private Animator mRootAnimator;

	private void ShowOpenAnim(GameObject go) {
		string anim = OpenAnim;
		if (string.IsNullOrEmpty(anim)) { return; }
		if (mRootAnimation != null) {
			mRootAnimation.PlayAnim(anim);
		}
		if (mRootAnimator != null) {
			mRootAnimator.PlayAnim(anim);
		}
	}

	protected void CloseSelf() {
		if (PlayCloseAnim(() => { UIManager.CloseSingle(this); })) { return; }
		UIManager.CloseSingle(this);
	}

	protected void CloseGroup() {
		if (PlayCloseAnim(() => { UIManager.CloseGroup(this); })) { return; }
		UIManager.CloseGroup(this);
	}

	private bool PlayCloseAnim(Action callback) {
		string anim = CloseAnim;
		if (string.IsNullOrEmpty(anim)) { return false; }
		if (mRootAnimation != null) {
			string key = GetType().Name + GetHashCode();
			bool ret = mRootAnimation.PlayAnim(anim, () => {
				UIManager.EndLoading(key);
				callback.Invoke();
			});
			if (ret) {
				UIManager.BeginLoading(key);
			}
			return ret;
		}
		if (mRootAnimator != null) {
			string key = GetType().Name + GetHashCode();
			bool ret = mRootAnimator.PlayAnim(anim, () => {
				UIManager.EndLoading(key);
				callback.Invoke();
			});
			if (ret) {
				UIManager.BeginLoading(key);
			}
			return ret;
		}
		return false;
	}

}
}

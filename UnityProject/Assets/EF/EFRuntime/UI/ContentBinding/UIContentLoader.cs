using EF.UI.WFramework;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace EF.UI.WFramework
{

[RequireComponent(typeof(RectTransform))]
public class UIContentLoader : MonoBehaviour {

	[SerializeField]
	private uint m_MaxChildCount = 1u;

	public IDisposable BindChild(string prefabPath) {
		return BindChildImpl(prefabPath, 0, null);
	}

	public IDisposable BindChild(string prefabPath, int sortingOrderBias) {
		return BindChildImpl(prefabPath, sortingOrderBias, null);
	}

	public IDisposable BindChild(string prefabPath, Action<GameObject> callback) {
		return BindChildImpl(prefabPath, 0, callback);
	}

	public IDisposable BindChild(string prefabPath, int sortingOrderBias, Action<GameObject> callback) {
		return BindChildImpl(prefabPath, sortingOrderBias, callback);
	}

	public void Clear() {
		if (mChildren != null) {
			for (int i = mChildren.Count - 1; i >= 0; i--) {
				mChildren[i].Dispose();
			}
			mChildren.Clear();
			s_cached_children.Enqueue(mChildren);
		}
		mChildren = null;
	}

	private IDisposable BindChildImpl(string prefabPath, int sortingOrderBias, Action<GameObject> callback) {
		if (mTrans == null) { mTrans = GetComponent<RectTransform>(); }
		if (mChildren == null) {
			mChildren = s_cached_children.Count > 0 ? s_cached_children.Dequeue() : new List<ChildItem>();
		}
		if (mCanvas == null) {
			Canvas canvas = GetComponentInParent<Canvas>();
			while (canvas != null && !canvas.overrideSorting) {
				canvas = canvas.transform.parent.GetComponentInParent<Canvas>();
			}
			mCanvas = canvas;
		}
		ChildItem item = new ChildItem(this, prefabPath, sortingOrderBias, callback);
		mChildren.Add(item);
		if (m_MaxChildCount > 0) {
			int n = mChildren.Count;
			if (n > m_MaxChildCount) {
				mChildren[n - 1].Dispose();
				mChildren.RemoveAt(n - 1);
			}
		}
		return item;
	}

	private RectTransform mTrans;

	private Canvas mCanvas;

	private List<ChildItem> mChildren;

	private static Queue<List<ChildItem>> s_cached_children = new Queue<List<ChildItem>>();

	private class ChildItem : IDisposable {

		public ChildItem(UIContentLoader loader, string prefabPath, int sortingOrderBias, Action<GameObject> callback) {
			mLoader = loader;
			mLoadDisposable = loader.mTrans.BindChild(prefabPath, (GameObject go) => {
				int sortingOrder = mLoader.mCanvas.sortingOrder + sortingOrderBias;
				SortingOrderModifier modifier = SortingOrderModifier.Get(go);
				modifier.SetBaseSortingOrder(sortingOrder);
				SortingOrderModifier.Cache(modifier);
				if (callback != null) { callback.Invoke(go); }
			});
		}

		void IDisposable.Dispose() {
			if (mLoadDisposable != null) {
				mLoadDisposable.Dispose();
				mLoadDisposable = null;
			}
			if (mLoader != null && !mLoader.Equals(null)) {
				mLoader.mChildren.Remove(this);
			}
			mLoader = null;
		}

		public void Dispose() {
			if (mLoadDisposable != null) {
				mLoadDisposable.Dispose();
				mLoadDisposable = null;
			}
			mLoader = null;
		}

		private UIContentLoader mLoader;
		private IDisposable mLoadDisposable;

	}

}
}

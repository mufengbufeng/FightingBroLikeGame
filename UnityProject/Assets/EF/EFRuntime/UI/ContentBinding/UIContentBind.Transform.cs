using System;
using UnityEngine;

namespace EF.UI.WFramework
{

public static partial class UIContentBind {

	public static IDisposable BindChild(this Transform parent, string prefabPath) {
		IUIContentBindLoader loader = s_loader;
		if (loader == null || parent == null || parent.Equals(null)) { return s_fake; }
		return new ChildBind(loader, parent, prefabPath, null);
	}

	public static IDisposable BindChild(this Transform parent, string prefabPath, Action<GameObject> onBinded) {
		IUIContentBindLoader loader = s_loader;
		if (loader == null || parent == null || parent.Equals(null)) { return s_fake; }
		return new ChildBind(loader, parent, prefabPath, onBinded);
	}

	private class ChildBind : IDisposable {

		private readonly IUIContentBindLoader mLoader;
		private Transform mParent;
		private GameObject mLoaded;

		public ChildBind(IUIContentBindLoader loader, Transform parent, string prefabPath, Action<GameObject> onBinded) {
			mLoader = loader;
			mParent = parent;
			Load(prefabPath, onBinded);
		}

		void IDisposable.Dispose() {
			mParent = null;
			if (mLoaded != null) {
				mLoader.UnloadGameObject(mLoaded);
				mLoaded = null;
			}
		}

		private async void Load(string prefabPath, Action<GameObject> callback) {
			GameObject go = await mLoader.LoadGameObject(prefabPath);
			if (mParent == null || mParent.Equals(null)) {
				if (go != null) { mLoader.UnloadGameObject(go); }
				return;
			}
			if (mLoaded != null) { mLoader.UnloadGameObject(mLoaded); }
			mLoaded = go;
			if (go != null) {
				Transform trans = go.transform;
				trans.SetParent(mParent);
				trans.localRotation = Quaternion.identity;
				trans.localScale = Vector3.one;
				if (trans is RectTransform rt) {
					rt.anchorMin = Vector2.zero;
					rt.anchorMax = Vector2.one;
					rt.anchoredPosition3D = Vector3.zero;
					rt.sizeDelta = Vector3.one;
				} else {
					trans.localPosition = Vector3.zero;
				}
			}
			if (callback != null) {
				try { callback(go); } catch (Exception e) { Debug.LogException(e); }
			}
		}
	}

}
}

using System;
using UnityEngine;
using UnityEngine.UI;

namespace EF.UI.WFramework
{

public static partial class UIContentBind {

	public static IDisposable BindTexture(this RawImage rawImage, string texturePath) {
		IUIContentBindLoader loader = s_loader;
		if (loader == null || rawImage == null || rawImage.Equals(null)) { return s_fake; }
		ClearBinded(rawImage);
		return AddBinded(rawImage, new TextureBind(loader, rawImage, texturePath, null));
	}

	public static IDisposable BindTexture(this RawImage rawImage, string texturePath, Action<bool> onBinded) {
		IUIContentBindLoader loader = s_loader;
		if (loader == null || rawImage == null || rawImage.Equals(null)) { return s_fake; }
		ClearBinded(rawImage);
		return AddBinded(rawImage, new TextureBind(loader, rawImage, texturePath, onBinded));
	}

	private class TextureBind : IDisposable {

		private readonly IUIContentBindLoader mLoader;
		private RawImage mRawImage;
		private Texture mSavedTex;
		private Texture mLoaded;

		public TextureBind(IUIContentBindLoader loader, RawImage rawImage, string texturePath, Action<bool> onBinded) {
			mLoader = loader;
			mRawImage = rawImage;
			mSavedTex = rawImage.texture;
			rawImage.texture = GetEmptyTexture();
			Load(texturePath, onBinded);
		}

		void IDisposable.Dispose() {
			if (mRawImage != null && !mRawImage.Equals(null)) {
				mRawImage.texture = mSavedTex;
			}
			mRawImage = null;
			mSavedTex = null;
			if (mLoaded != null) {
				mLoader.UnloadTexture(mLoaded);
				mLoaded = null;
			}
		}

		private async void Load(string texturePath, Action<bool> callback) {
			Texture tex = await mLoader.LoadTexture(texturePath);
			if (mRawImage == null || mRawImage.Equals(null)) {
				if (tex != null) { mLoader.UnloadTexture(tex); }
				return;
			}
			if (mLoaded != null) { mLoader.UnloadTexture(mLoaded); }
			mLoaded = tex;
			if (tex != null) { mRawImage.texture = tex; }
			if (callback != null) {
				try { callback(tex != null); } catch (Exception e) { Debug.LogException(e); }
			}
		}

	}

}
}

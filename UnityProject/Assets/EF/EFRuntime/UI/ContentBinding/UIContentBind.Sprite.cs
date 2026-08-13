using System;
using UnityEngine;
using UnityEngine.UI;

namespace EF.UI.WFramework
{

public static partial class UIContentBind {

	public static IDisposable BindSprite(this Image image, string atlasPath, string spritename) {
		IUIContentBindLoader loader = s_loader;
		if (loader == null || image == null || image.Equals(null)) { return s_fake; }
		ClearBinded(image);
		return AddBinded(image, new SpriteBind(loader, image, atlasPath, spritename, null));
	}

	public static IDisposable BindSprite(this Image image, string atlasPath, string spritename, Action<bool> onBinded) {
		IUIContentBindLoader loader = s_loader;
		if (loader == null || image == null || image.Equals(null)) { return s_fake; }
		ClearBinded(image);
		return AddBinded(image, new SpriteBind(loader, image, atlasPath, spritename, onBinded));
	}

	public static IDisposable BindSprite(this Image image, string spritePath) {
		IUIContentBindLoader loader = s_loader;
		if (loader == null || image == null || image.Equals(null)) { return s_fake; }
		ClearBinded(image);
		return AddBinded(image, new SpriteBind(loader, image, spritePath, null));
	}

	public static IDisposable BindSprite(this Image image, string spritePath, Action<bool> onBinded) {
		IUIContentBindLoader loader = s_loader;
		if (loader == null || image == null || image.Equals(null)) { return s_fake; }
		ClearBinded(image);
		return AddBinded(image, new SpriteBind(loader, image, spritePath, onBinded));
	}

	private class SpriteBind : IDisposable {

		private readonly IUIContentBindLoader mLoader;
		private Image mImage;
		private Sprite mSavedSprite;
		private Sprite mLoaded;

		public SpriteBind(IUIContentBindLoader loader, Image image, string atlasPath, string spritename, Action<bool> onBinded) {
			mLoader = loader;
			mImage = image;
			mSavedSprite = image.sprite;
			image.sprite = GetEmptySprite();
			Load(atlasPath, spritename, onBinded);
		}

		public SpriteBind(IUIContentBindLoader loader, Image image, string spritePath, Action<bool> onBinded) {
			mLoader = loader;
			mImage = image;
			mSavedSprite = image.sprite;
			image.sprite = GetEmptySprite();
			Load(spritePath, onBinded);
		}

		void IDisposable.Dispose() {
			if (mImage != null && !mImage.Equals(null)) {
				mImage.sprite = mSavedSprite;
			}
			mImage = null;
			mSavedSprite = null;
			if (mLoaded != null) {
				mLoader.UnloadSprite(mLoaded);
				mLoaded = null;
			}
		}

		private async void Load(string atlasPath, string spritename, Action<bool> callback) {
			Sprite sprite = await mLoader.LoadSprite(atlasPath, spritename);
			if (mImage == null || mImage.Equals(null)) {
				if (sprite != null) { mLoader.UnloadSprite(sprite); }
				return;
			}
			if (mLoaded != null) { mLoader.UnloadSprite(mLoaded); }
			mLoaded = sprite;
			if (sprite != null) { mImage.sprite = sprite; }
			if (callback != null) {
				try { callback(sprite != null); } catch (Exception e) { Debug.LogException(e); }
			}
		}

		private async void Load(string spritePath, Action<bool> callback) {
			Sprite sprite = await mLoader.LoadSprite(spritePath);
			if (mImage == null || mImage.Equals(null)) {
				if (sprite != null) { mLoader.UnloadSprite(sprite); }
				return;
			}
			if (mLoaded != null) { mLoader.UnloadSprite(mLoaded); }
			mLoaded = sprite;
			if (sprite != null) { mImage.sprite = sprite; }
			if (callback != null) {
				try { callback(sprite != null); } catch (Exception e) { Debug.LogException(e); }
			}
		}

	}

}
}

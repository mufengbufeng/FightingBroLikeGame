using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace EF.UI.WFramework
{

public class ToggleContentBind : IDisposable {

	private List<Toggle> mToggles;
	private List<GameObject> mContents;
	private UnityAction<bool>[] mCallbacks;

	public ToggleContentBind(IEnumerable<Toggle> toggles, IEnumerable<GameObject> contents) {
		if (toggles == null) { throw new ArgumentNullException(nameof(toggles)); }
		if (contents == null) { throw new ArgumentNullException(nameof(contents)); }
		mToggles = new List<Toggle>(toggles);
		mContents = new List<GameObject>(contents);
		int nt = mToggles.Count;
		mCallbacks = new UnityAction<bool>[nt];
		for (int i = mContents.Count - 1; i >= nt; i--) {
			GameObject go = mContents[i];
			if (go != null && !go.Equals(null)) { go.SetActive(false); }
		}
		for (int i = 0; i < nt; i++) {
			Toggle toggle = mToggles[i];
			GameObject go = mContents[i];
			bool on = false;
			if (toggle == null || toggle.Equals(null)) { continue; }
			on = toggle.isOn;
			UnityAction<bool> callback = (bool ison) => {
				if (go != null && !go.Equals(null)) { go.SetActive(ison); }
			};
			toggle.onValueChanged.AddListener(callback);
			mCallbacks[i] = callback;
			if (go != null && !go.Equals(null)) { go.SetActive(toggle.isOn); }
		}
	}

	public void Dispose() {
		if (mCallbacks != null) {
			for (int i = mToggles.Count - 1; i >= 0; i--) {
				Toggle toggle = mToggles[i];
				if (toggle == null || toggle.Equals(null)) { continue; }
				UnityAction<bool> callback = mCallbacks[i];
				if (callback == null) { continue; }
				toggle.onValueChanged.RemoveListener(callback);
			}
			mCallbacks = null;
		}
	}

}
}

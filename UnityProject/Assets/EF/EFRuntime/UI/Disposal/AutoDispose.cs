using System;
using System.Collections.Generic;
using UnityEngine;

namespace EF.UI.WFramework
{

public class AutoDispose : IDisposable {

	public void Add(IDisposable disposable) {
		if (disposable == null) { return; }
		mDisposes.Add(disposable);
	}

	public void Dispose() {
		for (int i = mDisposes.Count - 1; i >= 0; i--) {
			try {
				mDisposes[i].Dispose();
			} catch (Exception ex) {
				Debug.LogException(ex);
			}
		}
		mDisposes.Clear();
	}

	private List<IDisposable> mDisposes = new List<IDisposable>(16);

}
}

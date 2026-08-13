using System;
using UnityEngine;
using UnityEngine.Events;

namespace EF.UI.WFramework
{

public static class DisposableExtend {

	public static void AddTo(this IDisposable disposable, UnityEvent evt) {
		if (disposable == null) { return; }
		evt.AddListener(() => {
			try {
				disposable.Dispose();
			} catch (Exception ex) {
				Debug.LogException(ex);
			}
		});
	}

}
}

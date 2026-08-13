using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

namespace EF.UI.WFramework {

	[Flags]
	public enum eUIVisibleOperateType { SetActive = 0, LayerMask = 1, OutOfScreen = 2 }

	public interface IUILogicBase {
		string MutexGroup { get; }
		eUIVisibleOperateType VisibleOperateType { get; }
		bool OnCreate(object para);
		bool OnPrepareCheck(ref float timeout, ref bool closeWhenTimeout);
		UniTask<bool> OnPrepareExecute();
		void OnOpen(GameObject go, int baseSortingOrder);
		void OnShow();
		void OnHide();
		void OnClose();
		void OnTerminated();
	}

	public interface IUIFocusable {
		void OnGetFocus();
		void OnLoseFocus();
		bool OnESC();
	}

	public interface IUILogicStack : IUILogicBase, IUIFocusable {
		bool AllowMultiple { get; }
		bool IsFullScreen { get; }
		bool NewGroup { get; }
	}

	public interface IUILogicFixed : IUILogicBase {
		int SortingOrderBias { get; }
	}

}

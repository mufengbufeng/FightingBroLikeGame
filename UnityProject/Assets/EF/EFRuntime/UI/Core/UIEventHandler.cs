using System;

namespace EF.UI.WFramework {

	public interface IUIEventHandler {
		void OnOpened();
		void OnShown();
		void OnHided();
		void OnClosed();
		void OnTerminated();
	}

	public class UIEventHandlerHelper : IUIEventHandler {
		void IUIEventHandler.OnOpened() {
			if (mOnOpened != null) { mOnOpened.Invoke(); }
		}
		void IUIEventHandler.OnShown() {
			if (mOnShown != null) { mOnShown.Invoke(); }
		}
		void IUIEventHandler.OnHided() {
			if (mOnHided != null) { mOnHided.Invoke(); }
		}
		void IUIEventHandler.OnClosed() {
			if (mOnClosed != null) { mOnClosed.Invoke(); }
		}
		void IUIEventHandler.OnTerminated() {
			if (mOnTerminated != null) { mOnTerminated.Invoke(); }
		}
		public UIEventHandlerHelper SetOnOpened(Action onOpened) { mOnOpened  = onOpened; return this; }
		public UIEventHandlerHelper SetOnShown(Action onShown) { mOnShown = onShown; return this; }
		public UIEventHandlerHelper SetOnHided(Action onHided) {  mOnHided = onHided; return this; }
		public UIEventHandlerHelper SetOnClosed(Action onClosed) { mOnClosed = onClosed; return this; }
		public UIEventHandlerHelper SetOnTerminated(Action onTerminated) { mOnTerminated = onTerminated; return this; }
		private Action mOnOpened;
		private Action mOnShown;
		private Action mOnHided;
		private Action mOnClosed;
		private Action mOnTerminated;
		public static UIEventHandlerHelper OnOpened(Action onOpened) {
			return new UIEventHandlerHelper().SetOnOpened(onOpened);
		}
		public static UIEventHandlerHelper OnShown(Action onShown) {
			return new UIEventHandlerHelper().SetOnShown(onShown);
		}
		public static UIEventHandlerHelper OnHided(Action onHided) {
			return new UIEventHandlerHelper().SetOnHided(onHided);
		}
		public static UIEventHandlerHelper OnClosed(Action onClosed) {
			return new UIEventHandlerHelper().SetOnClosed(onClosed);
		}
		public static UIEventHandlerHelper OnTerminated(Action onTerminated) {
			return new UIEventHandlerHelper().SetOnTerminated(onTerminated);
		}
	}

}

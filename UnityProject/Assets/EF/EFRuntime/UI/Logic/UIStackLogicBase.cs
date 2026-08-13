using EF.UI.WFramework;

namespace EF.UI.WFramework
{

public abstract class UIStackLogicBase : UILogicBase, IUILogicStack {

	protected virtual bool AllowMultiple { get { return false; } }
	protected abstract bool IsFullScreen { get; }
	protected abstract bool NewGroup { get; }
	protected virtual void OnGetFocus() { }

	protected virtual bool OnESC() { return true; }
	protected virtual void OnLoseFocus() { }

	bool IUILogicStack.AllowMultiple { get { return AllowMultiple; } }
	bool IUILogicStack.IsFullScreen { get { return IsFullScreen; } }
	bool IUILogicStack.NewGroup { get { return NewGroup; } }

	void IUIFocusable.OnGetFocus() { OnGetFocus(); }
	void IUIFocusable.OnLoseFocus() { OnLoseFocus(); }
	bool IUIFocusable.OnESC() { return OnESC(); }

}
}

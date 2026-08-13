namespace EF.UI.WFramework {

	public interface IUIDynamicFocusable : IUIFocusable {

		void SetDynamicFocusAgent(IUILogicDynamicFocusAgent agent);

	}

	public interface IUILogicDynamicFocusAgent {
		bool RequireFocus();
		bool ReleaseFocus();
	}

}
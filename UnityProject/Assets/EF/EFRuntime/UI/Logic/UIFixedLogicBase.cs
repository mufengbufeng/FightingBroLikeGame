using EF.UI.WFramework;

namespace EF.UI.WFramework
{

public abstract class UIFixedLogicBase : UILogicBase, IUILogicFixed {

	protected abstract int SortingOrderBias { get; }
	int IUILogicFixed.SortingOrderBias { get { return SortingOrderBias; } }

}
}

using System;
using System.Collections.Generic;

namespace EF.DataDriven {

	[Flags]
	public enum eRamDataStructChangedType { None = 0, Self = 1, Children = 2 }

	public abstract class RamDataNodeStructBase : RamDataNodeBase {

		public delegate T RamDataItemCtor<T>(IRamDataStructCtrl parent, out IRamDataCtrl ctrl) where T : RamDataNodeBase;

		protected IRamDataStructCtrl mCtrl;

		public RamDataNodeStructBase(IRamDataStructCtrl parent, out IRamDataCtrl ctrl) : base(parent, out ctrl) {
			mCtrl = new StructCtrl(this);
		}

		protected virtual void HandleEventBubbling() {
			if (mParent != null) { mParent.HandleEventBubbling(); }
		}

		protected eRamDataNodeChangedType CombineNodeChangedType(eRamDataNodeChangedType a, eRamDataNodeChangedType b) {
			if (a == eRamDataNodeChangedType.Changed || b == eRamDataNodeChangedType.Changed) { return eRamDataNodeChangedType.Changed; }
			if (a == eRamDataNodeChangedType.Init || b == eRamDataNodeChangedType.Init) { return eRamDataNodeChangedType.Init; }
			return eRamDataNodeChangedType.None;
		}

		private class StructCtrl : IRamDataStructCtrl {
			private RamDataNodeStructBase mTarget;
			public StructCtrl(RamDataNodeStructBase target) { mTarget = target; }
			public void HandleEventBubbling() { mTarget.HandleEventBubbling(); }
		}

		protected static List<IRamDataCtrl> s_temp_ctrls = new List<IRamDataCtrl>();

	}

}
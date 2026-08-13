namespace EF.DataDriven {

	public abstract class RamDataCustomBase<T> : RamDataNodeStructBase where T : RamDataCustomBase<T> {

		public RamDataCustomBase(IRamDataStructCtrl parent, out IRamDataCtrl ctrl) : base(parent, out ctrl) { }

		public GreatEvent<T> onChanged {
			get {
				if (mOnChanged == null) { mOnChanged = new GreatEvent<T>(out mOnChangedCtrl); }
				return mOnChanged;
			}
		}

		protected override eRamDataNodeChangedType CollectAndNotifyChanged() {
			return eRamDataNodeChangedType.None;
		}

		protected override void HandleEventBubbling() {
			if (mOnChanged != null) { mOnChanged.Invoke(this as T); }
			base.HandleEventBubbling();
		}

		protected override void Reset() {
			if (mOnChangedCtrl != null) { mOnChangedCtrl.RemoveAll(); }
		}

		protected override void Dispose() {
			if (mOnChangedCtrl != null) { mOnChangedCtrl.Dispose(); }
		}

		private GreatEvent<T> mOnChanged;
		private IGreatEventCtrl mOnChangedCtrl;

	}

}

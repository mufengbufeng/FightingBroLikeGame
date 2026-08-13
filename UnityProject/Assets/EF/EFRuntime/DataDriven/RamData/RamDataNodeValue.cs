namespace EF.DataDriven {

	public abstract class RamDataNodeValue<T, TV> : RamDataNodeBase where T : RamDataNodeValue<T, TV> {

		private TV mPrevValue;
		private TV mValue;
		private int mDirty = 0;
		private GreatEvent<T, TV> mOnChanged;
		private IGreatEventCtrl mOnChangedCtrl;

		public RamDataNodeValue(IRamDataStructCtrl parent, out IRamDataCtrl ctrl) : base(parent, out ctrl) { }

		public TV Value {
			get {
				CollectUsage();
				return mValue;
			}
			set {
				if (CheckEqual(value, mValue)) { return; }
				if (mDirty < 0) {
					mDirty = 1;
					mPrevValue = mValue;
				}
				mValue = value;
			}
		}

		public GreatEvent<T, TV> onChanged {
			get {
				if (mOnChanged == null) {
					mOnChanged = new GreatEvent<T, TV>(out mOnChangedCtrl);
				}
				return mOnChanged;
			}
		}

		public static implicit operator TV(RamDataNodeValue<T, TV> node) { node.CollectUsage(); return node.mValue; }

		public override string ToString() {
			return typeof(TV).IsClass && mValue == null ? "null" : mValue.ToString();
		}

		protected override eRamDataNodeChangedType CollectAndNotifyChanged() {
			if (mDirty < 0) { return eRamDataNodeChangedType.None; }
			eRamDataNodeChangedType ret = mDirty > 0 ? eRamDataNodeChangedType.Changed : eRamDataNodeChangedType.Init;
			mDirty = -1;
			if (mOnChanged != null) { mOnChanged.Invoke(this as T, mPrevValue); }
			TryDispatchInternalChanged();
			return ret;
		}

		protected override void Reset() {
			if (mOnChangedCtrl != null) { mOnChangedCtrl.RemoveAll(); }
		}

		protected override void Dispose() {
			if (mOnChangedCtrl != null) { mOnChangedCtrl.Dispose(); }
			mOnChanged = null;
			mOnChangedCtrl = null;
		}

		protected abstract bool CheckEqual(TV a, TV b);

	}

	public sealed class RamDataByte : RamDataNodeValue<RamDataByte, byte> {
		public RamDataByte(IRamDataStructCtrl parent, out IRamDataCtrl ctrl) : base(parent, out ctrl) { }
		protected override bool CheckEqual(byte a, byte b) { return a == b; }
	}

	public sealed class RamDataSByte : RamDataNodeValue<RamDataSByte, sbyte> {
		public RamDataSByte(IRamDataStructCtrl parent, out IRamDataCtrl ctrl) : base(parent, out ctrl) { }
		protected override bool CheckEqual(sbyte a, sbyte b) { return a == b; }
	}

	public sealed class RamDataShort : RamDataNodeValue<RamDataShort, short> {
		public RamDataShort(IRamDataStructCtrl parent, out IRamDataCtrl ctrl) : base(parent, out ctrl) { }
		protected override bool CheckEqual(short a, short b) { return a == b; }
	}

	public sealed class RamDataUShort : RamDataNodeValue<RamDataUShort, ushort> {
		public RamDataUShort(IRamDataStructCtrl parent, out IRamDataCtrl ctrl) : base(parent, out ctrl) { }
		protected override bool CheckEqual(ushort a, ushort b) { return a == b; }
	}

	public sealed class RamDataInt : RamDataNodeValue<RamDataInt, int> {
		public RamDataInt(IRamDataStructCtrl parent, out IRamDataCtrl ctrl) : base(parent, out ctrl) { }
		protected override bool CheckEqual(int a, int b) { return a == b; }
	}

	public sealed class RamDataUInt : RamDataNodeValue<RamDataUInt, uint> {
		public RamDataUInt(IRamDataStructCtrl parent, out IRamDataCtrl ctrl) : base(parent, out ctrl) { }
		protected override bool CheckEqual(uint a, uint b) { return a == b; }
	}

	public sealed class RamDataLong : RamDataNodeValue<RamDataLong, long> {
		public RamDataLong(IRamDataStructCtrl parent, out IRamDataCtrl ctrl) : base(parent, out ctrl) { }
		protected override bool CheckEqual(long a, long b) { return a == b; }
	}

	public sealed class RamDataULong : RamDataNodeValue<RamDataULong, ulong> {
		public RamDataULong(IRamDataStructCtrl parent, out IRamDataCtrl ctrl) : base(parent, out ctrl) { }
		protected override bool CheckEqual(ulong a, ulong b) { return a == b; }
	}

	public sealed class RamDataFloat : RamDataNodeValue<RamDataFloat, float> {
		public RamDataFloat(IRamDataStructCtrl parent, out IRamDataCtrl ctrl) : base(parent, out ctrl) { }
		protected override bool CheckEqual(float a, float b) { return a == b; }
	}

	public sealed class RamDataDouble : RamDataNodeValue<RamDataDouble, double> {
		public RamDataDouble(IRamDataStructCtrl parent, out IRamDataCtrl ctrl) : base(parent, out ctrl) { }
		protected override bool CheckEqual(double a, double b) { return a == b; }
	}

	public sealed class RamDataBoolean : RamDataNodeValue<RamDataBoolean, bool> {
		public RamDataBoolean(IRamDataStructCtrl parent, out IRamDataCtrl ctrl) : base(parent, out ctrl) { }
		protected override bool CheckEqual(bool a, bool b) { return a == b; }
		public override string ToString() {
			return Value ? "true" : "false";
		}
	}

	public sealed class RamDataString : RamDataNodeValue<RamDataString, string> {
		public RamDataString(IRamDataStructCtrl parent, out IRamDataCtrl ctrl) : base(parent, out ctrl) { }
		protected override bool CheckEqual(string a, string b) { return a == b; }
		public override string ToString() {
			return Value == null ? "null" : $"\"{Value}\"";
		}
	}

}
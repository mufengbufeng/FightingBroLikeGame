using System;
using System.Collections;
using System.Collections.Generic;

namespace EF.DataDriven {

	public class RamDataList<T> : RamDataNodeStructBase, IEnumerable<T>, IReadOnlyList<T> where T : RamDataNodeBase {

		public RamDataList(IRamDataStructCtrl parent, RamDataItemCtor<T> itemCtor, out IRamDataCtrl ctrl) : base(parent, out ctrl) {
			mItemCtor = itemCtor;
		}

		public GreatEvent<RamDataList<T>, eRamDataStructChangedType> onChanged {
			get {
				if (mOnChanged == null) {
					mOnChanged = new GreatEvent<RamDataList<T>, eRamDataStructChangedType>(out mOnChangedCtrl);
				}
				return mOnChanged;
			}
		}

		public int Count { get { CollectUsage(); return mList1.Count; } }

		public T Add() {
			IRamDataCtrl ctrl;
			T item = mItemCtor(mCtrl, out ctrl);
			mList1.Add(item);
			mList2.Add(ctrl);
			mDirty = 1;
			return item;
		}

		public T Insert(int index) {
			if (index < 0 || index > mList1.Count) { throw new IndexOutOfRangeException(); }
			IRamDataCtrl ctrl;
			T item = mItemCtor(mCtrl, out ctrl);
			mList1.Insert(index, item);
			mList2.Insert(index, ctrl);
			mDirty = 1;
			return item;
		}

		public int IndexOf(T item) {
			CollectUsage();
			return mList1.IndexOf(item);
		}

		public bool Contains(T item) {
			CollectUsage();
			return mList1.Contains(item);
		}

		public bool Remove(T item) {
			return RemoveAt(mList1.IndexOf(item));
		}

		public bool RemoveAt(int index) {
			if (index < 0) { return false; }
			IRamDataCtrl ctrl = mList2[index];
			mList1.RemoveAt(index);
			mList2.RemoveAt(index);
			ctrl.Reset();
			ctrl.Dispose();
			mDirty = 1;
			return true;
		}

		public T this[int index] { get { CollectUsage(); return mList1[index]; } }

		public void Clear() {
			mList1.Clear();
			s_temp_ctrls.Clear();
			s_temp_ctrls.AddRange(mList2);
			mList2.Clear();
			foreach (var ctrl in s_temp_ctrls) {
				ctrl.Reset();
				ctrl.Dispose();
			}
			s_temp_ctrls.Clear();
			mDirty = 1;
		}

		public override string ToString() {
			int n = mList1.Count;
			string[] strs = new string[n];
			for (int i = 0; i < n; i++) {
				strs[i] = mList1[i].ToString();
			}
			return $"[{string.Join(",", strs)}]";
		}

		private RamDataItemCtor<T> mItemCtor;

		private GreatEvent<RamDataList<T>, eRamDataStructChangedType> mOnChanged;
		private IGreatEventCtrl mOnChangedCtrl;

		private List<T> mList1 = new List<T>();
		private List<IRamDataCtrl> mList2 = new List<IRamDataCtrl>();

		private int mDirty = 0;

		protected override eRamDataNodeChangedType CollectAndNotifyChanged() {
			eRamDataNodeChangedType ret = eRamDataNodeChangedType.None;
			eRamDataStructChangedType flags = eRamDataStructChangedType.None;
			int n = mList2.Count;
			for (int i = 0; i < n; i++) {
				eRamDataNodeChangedType t = mList2[i].CollectAndNotifyChanged();
				switch (t) {
					case eRamDataNodeChangedType.Init:
					case eRamDataNodeChangedType.Changed:
						flags |= eRamDataStructChangedType.Children;
						break;
				}
				ret = CombineNodeChangedType(ret, t);
			}
			if (mDirty < 0 && flags == eRamDataStructChangedType.None) { return eRamDataNodeChangedType.None; }
			bool selfChanged = mDirty >= 0;
			if (selfChanged) {
				ret = CombineNodeChangedType(ret, mDirty > 0 ? eRamDataNodeChangedType.Changed : eRamDataNodeChangedType.Init);
				flags |= eRamDataStructChangedType.Self;
				mDirty = -1;
			}
			if (mOnChanged != null) { mOnChanged.Invoke(this, flags); }
			if (selfChanged) { TryDispatchInternalChanged(); }
			return ret;
		}

		protected override void Reset() {
			Clear();
			if (mOnChangedCtrl != null) { mOnChangedCtrl.RemoveAll(); }
			mDirty = 0;
		}

		protected override void Dispose() {
			Clear();
			if (mOnChangedCtrl != null) { mOnChangedCtrl.Dispose(); }
			mOnChanged = null;
			mOnChangedCtrl = null;
		}

		protected override void HandleEventBubbling() {
			if (mOnChanged != null) { mOnChanged.Invoke(this, eRamDataStructChangedType.Children); }
			base.HandleEventBubbling();
		}

		IEnumerator<T> IEnumerable<T>.GetEnumerator() {
			CollectUsage();
			return mList1.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator() {
			CollectUsage();
			return mList1.GetEnumerator();
		}
	}

}

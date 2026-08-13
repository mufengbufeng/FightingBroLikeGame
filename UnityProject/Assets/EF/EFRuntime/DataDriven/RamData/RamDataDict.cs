using System.Collections;
using System.Collections.Generic;

namespace EF.DataDriven {

	public class RamDataDict<TKey, TVal> : RamDataNodeStructBase, IEnumerable<KeyValuePair<TKey, TVal>>, IReadOnlyDictionary<TKey, TVal> where TVal : RamDataNodeBase {

		public RamDataDict(IRamDataStructCtrl parent, RamDataItemCtor<TVal> itemCtor, out IRamDataCtrl ctrl) : base(parent, out ctrl) {
			mItemCtrl = itemCtor;
		}

		public GreatEvent<RamDataDict<TKey, TVal>, eRamDataStructChangedType> onChanged {
			get {
				if (mOnChanged == null) {
					mOnChanged = new GreatEvent<RamDataDict<TKey, TVal>, eRamDataStructChangedType>(out mOnChangedCtrl);
				}
				return mOnChanged;
			}
		}

		public TVal Add(TKey key) {
			IRamDataCtrl ctrl;
			var item = mItemCtrl(mCtrl, out ctrl);
			mDict1.Add(key, item);
			mDict2.Add(key, ctrl);
			mDirty = 1;
			return item;
		}

		public bool TryGetValue(TKey key, out TVal value) {
			CollectUsage();
			return mDict1.TryGetValue(key, out value);
		}

		public bool ContainsKey(TKey key) {
			CollectUsage();
			return mDict1.ContainsKey(key);
		}

		public bool ContainsValue(TVal value) {
			CollectUsage();
			return mDict1.ContainsValue(value);
		}

		public bool Remove(TKey key) {
			bool ret = mDict1.Remove(key);
			if (!ret) { return false; }
			IRamDataCtrl ctrl;
			if (!mDict2.TryGetValue(key, out ctrl)) { return false; }
			mDict2.Remove(key);
			ctrl.Reset();
			ctrl.Dispose();
			mDirty = 1;
			return true;
		}

		public IEnumerable<TKey> Keys { get { CollectUsage(); return mDict1.Keys; } }

		public IEnumerable<TVal> Values { get { CollectUsage(); return mDict1.Values; } }

		public int Count { get { CollectUsage(); return mDict1.Count; } }

		public TVal this[TKey key] { get { CollectUsage(); return mDict1[key]; } }

		public void Clear() {
			mDict1.Clear();
			s_temp_ctrls.Clear();
			s_temp_ctrls.AddRange(mDict2.Values);
			mDict2.Clear();
			foreach (var ctrl in s_temp_ctrls) {
				ctrl.Reset();
				ctrl.Dispose();
			}
			s_temp_ctrls.Clear();
			mDirty = 1;
		}

		public override string ToString() {
			int n = mDict1.Count;
			string[] strs = new string[n];
			int i = 0;
			foreach (KeyValuePair<TKey, TVal> kv in mDict1) {
				string key = kv.Key is string ? $"\"{kv.Key}\"" : kv.Key.ToString();
				strs[i++] = $"{key}:{kv.Value}";
			}
			return $"{{{string.Join(",", strs)}}}";
		}

		private RamDataItemCtor<TVal> mItemCtrl;

		private Dictionary<TKey, TVal> mDict1 = new Dictionary<TKey, TVal>();
		private Dictionary<TKey, IRamDataCtrl> mDict2 = new Dictionary<TKey, IRamDataCtrl>();

		private int mDirty = 0;

		private GreatEvent<RamDataDict<TKey, TVal>, eRamDataStructChangedType> mOnChanged;
		private IGreatEventCtrl mOnChangedCtrl;

		protected override eRamDataNodeChangedType CollectAndNotifyChanged() {
			eRamDataNodeChangedType ret = eRamDataNodeChangedType.None;
			eRamDataStructChangedType flags = eRamDataStructChangedType.None;
			foreach (var kv in mDict2) {
				eRamDataNodeChangedType t = kv.Value.CollectAndNotifyChanged();
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

		IEnumerator<KeyValuePair<TKey, TVal>> IEnumerable<KeyValuePair<TKey, TVal>>.GetEnumerator() {
			CollectUsage();
			return mDict1.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator() {
			CollectUsage();
			return mDict1.GetEnumerator();
		}

	}

}

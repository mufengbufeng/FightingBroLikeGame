using System;
using UnityEngine;

namespace EF.DataDriven {

	public static class DataDrivenBind {

		public static IDisposable Bind<T, TV>(this RamDataNodeValue<T, TV> node, Action<TV, TV> callback) where T : RamDataNodeValue<T, TV> {
			if (node == null || callback == null) { return s_fake; }
			return new V<T, TV>(node, callback);
		}

		public static IDisposable Bind<T>(this RamDataCustomBase<T> node, Action<T> callback) where T : RamDataCustomBase<T> {
			if (node == null || callback == null) { return s_fake; }
			return new N<T>(node, callback);
		}

		public static IDisposable Bind<T>(this RamDataList<T> list, Action<RamDataList<T>, eRamDataStructChangedType> callback) where T : RamDataNodeBase {
			if (list == null || callback == null) { return s_fake; }
			return new L<T>(list, callback);
		}

		public static IDisposable Bind<TKey, TVal>(this RamDataDict<TKey, TVal> dict, Action<RamDataDict<TKey, TVal>, eRamDataStructChangedType> callback) where TVal : RamDataNodeBase {
			if (dict == null || callback == null) { return s_fake; }
			return new D<TKey, TVal>(dict, callback);
		}

		private class V<T, TV> : IDisposable where T : RamDataNodeValue<T, TV> {

			RamDataNodeValue<T, TV> mNode;
			private Action<T, TV> mOnChanged;
			private Action<TV, TV> mCallback;

			public V(RamDataNodeValue<T, TV> node, Action<TV, TV> callback) {
				mNode = node;
				mCallback = callback;
				mOnChanged = OnChanged;
				node.onChanged.Add(mOnChanged);
				try { callback(node.Value, node.Value); } catch (Exception e) { Debug.LogException(e); }
			}

			void IDisposable.Dispose() {
				if (mNode != null && mOnChanged != null) {
					mNode.onChanged.Remove(mOnChanged);
				}
				mNode = null;
				mOnChanged = null;
				mCallback = null;
			}

			private void OnChanged(T node, TV prev) {
				try { mCallback(node.Value, prev); } catch (Exception e) { Debug.LogException(e); }
			}

		}

		private class N<T> : IDisposable where T : RamDataCustomBase<T> {

			private RamDataCustomBase<T> mNode;
			private Action<T> mCallback;

			public N(RamDataCustomBase<T> node, Action<T> callback) {
				mNode = node;
				mCallback = callback;
				node.onChanged.Add(mCallback);
				try { callback(node as T); } catch (Exception e) { Debug.LogException(e); }
			}

			void IDisposable.Dispose() {
				if (mNode != null && mCallback != null) {
					mNode.onChanged.Remove(mCallback);
				}
				mNode = null;
				mCallback = null;
			}

		}

		private class L<T> : IDisposable where T : RamDataNodeBase {

			private RamDataList<T> mList;
			private Action<RamDataList<T>, eRamDataStructChangedType> mCallback;

			public L(RamDataList<T> list, Action<RamDataList<T>, eRamDataStructChangedType> callback) {
				mList = list;
				mCallback = callback;
				list.onChanged.Add(mCallback);
				try { callback(list, eRamDataStructChangedType.None); } catch (Exception e) { Debug.LogException(e); }
			}

			void IDisposable.Dispose() {
				if (mList != null && mCallback != null) {
					mList.onChanged.Remove(mCallback);
				}
				mList = null;
				mCallback = null;
			}

		}

		private class D<TKey, TVal> : IDisposable where TVal : RamDataNodeBase {

			private RamDataDict<TKey, TVal> mDict;
			private Action<RamDataDict<TKey, TVal>, eRamDataStructChangedType> mCallback;

			public D(RamDataDict<TKey, TVal> dict, Action<RamDataDict<TKey, TVal>, eRamDataStructChangedType> callback) {
				mDict = dict;
				mCallback = callback;
				dict.onChanged.Add(mCallback);
				try { callback(dict, eRamDataStructChangedType.None); } catch (Exception e) { Debug.LogException(e); }
			}

			void IDisposable.Dispose() {
				if (mDict != null && mCallback != null) {
					mDict.onChanged.Remove(mCallback);
				}
				mDict = null;
				mCallback = null;
			}

		}

		private class Fake : IDisposable { void IDisposable.Dispose() { } }

		private static IDisposable s_fake = new Fake();

	}

}
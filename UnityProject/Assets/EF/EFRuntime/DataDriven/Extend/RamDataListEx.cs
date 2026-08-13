using System;
using System.Collections.Generic;

namespace EF.DataDriven {

	public static class RamDataListEx {

		public static void SyncFrom<T, F>(this RamDataList<T> target, IEnumerable<F> from, Action<F, T> convert) where T : RamDataNodeBase {
			int n = 0;
			foreach (F f in from) {
				T to = target.Count <= n ? target.Add() : target[n];
				convert(f, to);
				n++;
			}
			for (int i = target.Count - 1; i >= n; i--) {
				target.RemoveAt(i);
			}
		}

		public static void SyncFrom<T, F>(this RamDataList<T> target, IEnumerable<F> from, Func<F, T, bool> convert) where T : RamDataNodeBase {
			int n = 0;
			foreach (F f in from) {
				T to = target.Count <= n ? target.Add() : target[n];
				if (convert(f, to)) { n++; }
			}
			for (int i = target.Count - 1; i >= n; i--) {
				target.RemoveAt(i);
			}
		}

		public static void SyncFrom<T, F>(this RamDataList<T> target, IEnumerable<F> from, Func<F, bool> filter, Action<F, T> convert) where T : RamDataNodeBase {
			int n = 0;
			foreach (F f in from) {
				if (filter != null && !filter(f)) { continue; }
				T to = target.Count <= n ? target.Add() : target[n];
				convert(f, to);
				n++;
			}
			for (int i = target.Count - 1; i >= n; i--) {
				target.RemoveAt(i);
			}
		}

	}

}
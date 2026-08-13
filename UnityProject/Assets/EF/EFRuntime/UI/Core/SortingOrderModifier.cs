using System.Collections.Generic;
using UnityEngine;

namespace EF.UI.WFramework {

	public class SortingOrderModifier {

		private GameObject mRoot;

		private SortingOrderModifier() { }

		public void SetBaseSortingOrder(int baseSortingOrder) {
			mBaseSortingOrder = baseSortingOrder;
			for (int i = mSortingOrderDatas.Count - 1; i >= 0; i--) {
				SortingOrderData data = mSortingOrderDatas[i];
				if (data.canvas != null) {
					data.canvas.sortingOrder = baseSortingOrder + data.offset;
				}
				if (data.renderer != null) {
					data.renderer.sortingOrder = baseSortingOrder + data.offset;
				}
			}
		}

		public int GetBaseSortingOrder() { return mBaseSortingOrder; }

		public void SetLayer(int layer) {
			for (int i = mSortingOrderDatas.Count - 1; i >= 0; i--) {
				mSortingOrderDatas[i].go.layer = layer;
			}
		}

		private void Init() {
			mSortingOrderDatas.Clear();
			s_temp_canvases.Clear();
			s_temp_renderers.Clear();
			int min = int.MaxValue;
			mRoot.GetComponentsInChildren<Canvas>(true, s_temp_canvases);
			mRoot.GetComponentsInChildren<Renderer>(true, s_temp_renderers);
			for (int i = 0; i < s_temp_canvases.Count; i++) {
				Canvas canvas = s_temp_canvases[i];
				if (!canvas.overrideSorting) { continue; }
				min = Mathf.Min(min, canvas.sortingOrder);
				mSortingOrderDatas.Add(new SortingOrderData() {
					go = canvas.gameObject,
					canvas = canvas,
					offset = canvas.sortingOrder
				});
			}
			for (int i = 0; i < s_temp_renderers.Count; i++) {
				Renderer renderer = s_temp_renderers[i];
				min = Mathf.Min(min, renderer.sortingOrder);
				mSortingOrderDatas.Add(new SortingOrderData() {
					go = renderer.gameObject,
					renderer = renderer,
					offset = renderer.sortingOrder
				});
			}
			s_temp_canvases.Clear();
			s_temp_renderers.Clear();
			int layerForHide = UIManager.Root.LayerForHide;
			for (int i = mSortingOrderDatas.Count - 1; i >= 0; i--) {
				SortingOrderData data = mSortingOrderDatas[i];
				data.offset -= min;
				mSortingOrderDatas[i] = data;
				data.go.layer = layerForHide;
			}
		}
		private struct SortingOrderData {
			public GameObject go;
			public Canvas canvas;
			public Renderer renderer;
			public int offset;
		}

		private int mBaseSortingOrder;
		private List<SortingOrderData> mSortingOrderDatas = new List<SortingOrderData>();

		private static List<Canvas> s_temp_canvases = new List<Canvas>();
		private static List<Renderer> s_temp_renderers = new List<Renderer>();

		private static Queue<SortingOrderModifier> s_caches = new Queue<SortingOrderModifier>();

		public static SortingOrderModifier Get(GameObject root) {
			SortingOrderModifier modifier = s_caches.Count > 0 ? s_caches.Dequeue() : new SortingOrderModifier();
			modifier.mRoot = root;
			modifier.mSortingOrderDatas.Clear();
			modifier.Init();
			return modifier;
		}

		public static bool Cache(SortingOrderModifier modifier) {
			if (modifier == null) { return false; }
			if (s_caches.Contains(modifier)) { return false; }
			modifier.mRoot = null;
			modifier.mSortingOrderDatas.Clear();
			s_caches.Enqueue(modifier);
			return true;
		}

		public static void ClearCache() {
			s_temp_canvases.Clear();
			s_temp_renderers.Clear();
			s_caches.Clear();
		}

	}

}

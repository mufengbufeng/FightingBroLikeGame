using System;
using System.Collections.Generic;
using UnityEngine;

namespace EF.UI.WFramework {

	public partial class UIManager {

		private class Processor {

			private static readonly List<UIInstanceStack> temp_uistack_ins = new List<UIInstanceStack>(32);
			private readonly List<UIInstanceStack> mStack = new List<UIInstanceStack>();
			private readonly List<UIInstanceFixed> mFixed = new List<UIInstanceFixed>();

			private readonly FocusMgr mFocusMgr = new FocusMgr();

			private Action<UIInstanceStack> mProcessStackConflicts;
			private Action<UIInstanceFixed> mProcessFixedConflicts;

			public Processor() {
				mProcessStackConflicts = ProcessStackConflicts;
				mProcessFixedConflicts = ProcessFixedConflicts;
			}

			public bool Open(ParametersForUI cfg, object parameter, IUIEventHandler handler) {
				IUILogicBase logic = GetLogicInstance(cfg.logic_type);
				if (logic == null) { return false; }
				bool toopen = false;
				try {
					toopen = logic.OnCreate(parameter);
				} catch (Exception e) {
					Debug.LogException(e);
				}
				if (!toopen) {
					CacheLogicInstance(logic);
					return false;
				}
				mFocusMgr.HoldDispatchFocusChange();
				int baseSortingOrder;
				float posZ;
				if (logic is IUILogicStack stackLogic) {
					UIInstanceStack top = GetStackTop();
					UIInstanceStack uiStack = UIInstanceStack.Get(top != null ? top.Index + 1 : 0, cfg.id, cfg.prefab_path, stackLogic);
					if (uiStack.Logic.NewGroup) {
						uiStack.Group = uiStack;
					} else {
						object group = null;
						int n = mStack.Count;
						for (int i = n - 1; i >= 0; i--) {
							UIInstanceStack tmp = mStack[i];
							if (tmp == null) { continue; }
							group = tmp.Group;
							break;
						}
						uiStack.Group = group;
					}
					mStack.Add(uiStack);
					GetStackSortingOrderAndZ(uiStack.Index, out baseSortingOrder, out posZ);
					uiStack.Start(baseSortingOrder, posZ, handler, mProcessStackConflicts);
				} else if (logic is IUILogicFixed fixedLogic) {
					UIInstanceFixed uiFixed = UIInstanceFixed.Get(cfg.id, cfg.prefab_path, fixedLogic);
					GetFixedSortingORderAndZ(uiFixed.Logic.SortingOrderBias, out baseSortingOrder, out posZ);
					uiFixed.Start(baseSortingOrder, posZ, handler, mProcessFixedConflicts);
					mFixed.Add(uiFixed);
				} else {
					Debug.LogError("[UIManager] Invalid UI Logic Type !");
					return false;
				}
				mFocusMgr.AddFocusable(logic as IUIFocusable, baseSortingOrder);
				mFocusMgr.TryDispatchFocusChange();
				return true;
			}

			public void ResetPositionZ() {
				for (int i = mStack.Count - 1; i >= 0; i--) {
					UIInstanceStack tmp = mStack[i];
					if (tmp == null) { continue; }
					GetStackSortingOrderAndZ(tmp.Index, out _, out float z);
					tmp.ResetPositionZ(z);
				}
				for (int i = mFixed.Count - 1; i >= 0; i--) {
					UIInstanceFixed tmp = mFixed[i];
					GetFixedSortingORderAndZ(tmp.Logic.SortingOrderBias, out _, out float z);
					tmp.ResetPositionZ(z);
				}
			}

			public bool ProcessEscape() {
				IUIFocusable target = mFocusMgr.Current;
				if (target == null) { return false; }
				try { return target.OnESC(); } catch (Exception e) { Debug.LogException(e); }
				return false;
			}

			public bool CloseSingle(IUILogicBase logic) {
				if (logic is IUILogicStack stackLogic) {
					return CloseStack(stackLogic, false) > 0;
				}
				if (logic is IUILogicFixed fixedLogic) {
					return CloseFixed(fixedLogic);
				}
				return false;
			}

			public bool CloseGroup(IUILogicBase logic) {
				if (logic is IUILogicStack stackLogic) {
					return CloseStack(stackLogic, true) > 0;
				}
				if (logic is IUILogicFixed fixedLogic) {
					return CloseFixed(fixedLogic);
				}
				return false;
			}

			public bool CloseGroup(string id) {
				mFocusMgr.HoldDispatchFocusChange();
				bool ret = CloseByIdInternal(id, 1, true) > 0;
				mFocusMgr.TryDispatchFocusChange();
				return ret;
			}

			public bool CloseSingle(string id) {
				mFocusMgr.HoldDispatchFocusChange();
				bool ret = CloseByIdInternal(id, 1, false) > 0;
				mFocusMgr.TryDispatchFocusChange();
				return ret;
			}

			public int CloseAllStack() {
				mFocusMgr.HoldDispatchFocusChange();
				temp_uistack_ins.Clear();
				temp_uistack_ins.AddRange(mStack);
				mStack.Clear();
				int ret = 0;
				for (int i = temp_uistack_ins.Count - 1; i >= 0; i--) {
					UIInstanceStack tmp = temp_uistack_ins[i];
					if (tmp != null && CloseUIInstance<UIInstanceStack, IUILogicStack>(tmp)) { ret++; }
				}
				temp_uistack_ins.Clear();
				mFocusMgr.TryDispatchFocusChange();
				return ret;
			}

			public int CloseAll() {
				mFocusMgr.HoldDispatchFocusChange();
				int ret = CloseAllStackInternal();
				for (int i = mFixed.Count - 1; i >= 0; i--) {
					UIInstanceFixed item = mFixed[i];
					mFixed.RemoveAt(i);
					if (item != null && CloseUIInstance<UIInstanceFixed, IUILogicFixed>(item)) { ret++; }
				}
				mFocusMgr.TryDispatchFocusChange();
				return ret;
			}

			public void Shutdown() {
				mFocusMgr.HoldDispatchFocusChange();
				for (int i = mStack.Count - 1; i >= 0; i--) {
					UIInstanceStack item = mStack[i];
					if (item != null) { TerminateUIInstance<UIInstanceStack, IUILogicStack>(item); }
				}
				mStack.Clear();

				for (int i = mFixed.Count - 1; i >= 0; i--) {
					UIInstanceFixed item = mFixed[i];
					if (item != null) { TerminateUIInstance<UIInstanceFixed, IUILogicFixed>(item); }
				}
				mFixed.Clear();
				temp_uistack_ins.Clear();
				mFocusMgr.TryDispatchFocusChange();
			}

			public static void ClearTemporaryInstances() {
				temp_uistack_ins.Clear();
			}

			private int CloseAllStackInternal() {
				temp_uistack_ins.Clear();
				temp_uistack_ins.AddRange(mStack);
				mStack.Clear();
				int ret = 0;
				for (int i = temp_uistack_ins.Count - 1; i >= 0; i--) {
					UIInstanceStack item = temp_uistack_ins[i];
					if (item != null && CloseUIInstance<UIInstanceStack, IUILogicStack>(item)) { ret++; }
				}
				temp_uistack_ins.Clear();
				return ret;
			}

			private int CloseStack(IUILogicStack logic, bool group) {
				if (logic == null) { return -1; }
				for (int i = mStack.Count - 1; i >= 0; i--) {
					UIInstanceStack tmp = mStack[i];
					if (tmp != null && tmp.Logic == logic) {
						mFocusMgr.HoldDispatchFocusChange();
						int ret = 0;
						if (group) {
							ret = CloseGroupFromStackIndex(i, -1);
						} else {
							mStack[i] = null;
							if (CloseUIInstance<UIInstanceStack, IUILogicStack>(tmp)) {
								ret = 1;
							}
						}
						if (ret > 0) { TrimStack(true); }
						mFocusMgr.TryDispatchFocusChange();
						return ret;
					}
				}
				return 0;
			}

			private bool CloseFixed(IUILogicFixed logic) {
				for (int i = mFixed.Count - 1; i >= 0; i--) {
					UIInstanceFixed tmp = mFixed[i];
					if (tmp.Logic == logic) {
						mFixed.RemoveAt(i);
						return CloseUIInstance<UIInstanceFixed, IUILogicFixed>(tmp);
					}
				}
				return false;
			}

			private int CloseByIdInternal(string id, int max, bool group) {
				int ret = 0;
				bool needTrimStack = false;
				for (int i = mStack.Count - 1; i >= 0 && max > 0; i--) {
					UIInstanceStack tmp = mStack[i];
					if (tmp != null && tmp.Id == id) {
						if (group) {
							ret += CloseGroupFromStackIndex(i, -1);
						} else {
							mStack[i] = null;
							if (CloseUIInstance<UIInstanceStack, IUILogicStack>(tmp)) { ret++; }
						}
						max--;
						needTrimStack = true;
					}
				}
				if (needTrimStack) { TrimStack(true); }
				for (int i = mFixed.Count - 1; i >= 0 && max > 0; i--) {
					UIInstanceFixed tmp = mFixed[i];
					if (tmp.Id == id) {
						mFixed.RemoveAt(i);
						if (CloseUIInstance<UIInstanceFixed, IUILogicFixed>(tmp)) { ret++; }
						max--;
					}
				}
				return ret;
			}

			private int CloseGroupFromStackIndex(int index, int until) {
				int n = mStack.Count;
				if (until >= index) { n = until + 1; }
				if (index < 0 || index >= n) { return 0; }
				UIInstanceStack ui = mStack[index];
				if (ui == null) { return 0; }
				object group = ui.Group;
				int to = index;
				for (int i = index + 1; i < n; i++) {
					UIInstanceStack tmp = mStack[i];
					if (tmp == null) { continue; }
					if (tmp.Group != group) { break; }
					to = i;
				}
				int ret = 0;
				for (int i = to; i >= index; i--) {
					UIInstanceStack tmp = mStack[i];
					if (tmp == null) { continue; }
					mStack[i] = null;
					if (CloseUIInstance<UIInstanceStack, IUILogicStack>(tmp)) { ret++; }
				}
				return ret;
			}

			private bool CloseUIInstance<T, U>(T ui) where T : UIInstanceBase<T, U> where U : IUILogicBase {
				mFocusMgr.RemoveFocusable(ui.Logic as IUIFocusable);
				bool ret = ui.Close();
				CacheLogicInstance(ui.Logic);
				UIInstanceBase<T, U>.Cache(ui);
				return ret;
			}

			private void TerminateUIInstance<T, U>(T ui) where T : UIInstanceBase<T, U> where U : IUILogicBase {
				mFocusMgr.RemoveFocusable(ui.Logic as IUIFocusable);
				ui.Close();
			}

			private void ProcessStackConflicts(UIInstanceStack ui) {
				int closed = 0;
				string mg = ui.MutexGroup;
				int foundInStack = -1;
				for (int i = mStack.Count - 1; i >= 0; i--) {
					UIInstanceStack tmp = mStack[i];
					if (tmp == null) { continue; }
					if (foundInStack < 0) {
						if (tmp == ui) { foundInStack = i; }
						continue;
					}
					if (tmp.Id == ui.Id) {
						if (!ui.AllowMultiple) {
							CloseGroupFromStackIndex(i, foundInStack - 1);
						}
					} else if (!string.IsNullOrEmpty(mg) && tmp.MutexGroup == mg) {
						closed += CloseGroupFromStackIndex(i, foundInStack - 1);
						break;
					}
				}
				if (ui.Showing) {
					if (ui.IsFullScreen) {
						int hideFrom = foundInStack;
						for (int i = foundInStack - 1; i >= 0; i--) {
							UIInstanceStack tmp = mStack[i];
							if (tmp == null) { continue; }
							if (!tmp.Showing) { break; }
							hideFrom = i;
						}
						for (int i = hideFrom; i < foundInStack; i++) {
							UIInstanceStack tmp = mStack[i];
							if (tmp != null) { tmp.Hide(); }
						}
					}
				}
				if (closed > 0) { TrimStack(false); }
			}

			private void ProcessFixedConflicts(UIInstanceFixed ui) {
				string mg = ui.MutexGroup;
				if (!string.IsNullOrEmpty(mg)) {
					for (int i = mFixed.Count - 1; i >= 0; i--) {
						UIInstanceFixed tmp = mFixed[i];
						if (tmp == ui) { continue; }
						if (tmp.MutexGroup == mg || tmp.Id == ui.Id) {
							mFixed.RemoveAt(i);
							CloseUIInstance<UIInstanceFixed, IUILogicFixed>(tmp);
							break;
						}
					}
				}
			}

			private void TrimStack(bool checkTop) {
				int n = mStack.Count;
				while (n > 0) {
					n--;
					if (mStack[n] != null) { break; }
					mStack.RemoveAt(n);
				}
				if (!checkTop || mStack.Count <= 0) { return; }
				int from = n + 1;
				for (int i = n; i >= 0; i--) {
					UIInstanceStack tmp = mStack[i];
					if (tmp == null) { continue; }
					if (tmp.Showing) { break; }
					from = i;
					if (tmp.IsFullScreen) { break; }
				}
				for (int i = from; i <= n; i++) {
					UIInstanceStack tmp = mStack[i];
					if (tmp == null) { continue; }
					tmp.Resume();
				}
			}

			private UIInstanceStack GetStackTop() {
				for (int i = mStack.Count - 1; i >= 0; i--) {
					UIInstanceStack tmp = mStack[i];
					if (tmp == null) { continue; }
					return tmp;
				}
				return null;
			}

		}

		#region sorting order and z

		private static Canvas s_canvas;
		private static Camera s_ui_cam;

		private static int s_sorting_order_min;
		private static int s_sorting_order_max;
		private static int s_sorting_order_range_per_ui;
		private static float s_position_z_interval;

		private static float s_pos_z_from;
		private static float s_pos_z_to;

		private static float s_camera_near_clip;
		private static float s_camera_far_clip;
		private static float s_camera_size;
		private static int s_screen_width;
		private static int s_screen_height;

		private static bool s_to_recalculate_ui_pos = false;

		/// <summary>
		/// 清理跨模块生命周期保留的缓存与场景对象引用。
		/// </summary>
		private static void ClearRuntimeCachesAndReferences() {
			Processor.ClearTemporaryInstances();
			UIInstanceStack.ClearCacheForShutdown();
			UIInstanceFixed.ClearCacheForShutdown();
			SortingOrderModifier.ClearCache();

			s_canvas = null;
			s_ui_cam = null;
			s_sorting_order_min = 0;
			s_sorting_order_max = 0;
			s_sorting_order_range_per_ui = 0;
			s_position_z_interval = 0f;
			s_pos_z_from = 0f;
			s_pos_z_to = 0f;
			s_camera_near_clip = 0f;
			s_camera_far_clip = 0f;
			s_camera_size = 0f;
			s_screen_width = 0;
			s_screen_height = 0;
			s_to_recalculate_ui_pos = false;
		}

		private static void InitCameraRange() {
			s_sorting_order_min = Root.SortingOrderMin;
			s_sorting_order_max = Root.SortingOrderMax;
			s_sorting_order_range_per_ui = Root.SortingOrderRangePerUI;
			s_position_z_interval = Root.PositionZInterval;
			s_canvas = Root.RootCanvas;
			if (s_canvas == null) {
				throw new InvalidOperationException("[WFrameworkUI] UIRoot 未配置 RootCanvas。");
			}
			CalculateUIPos();
		}

		private static void CheckScreenOrCameraChanged() {
			if (s_processor == null) { return; }
			if (s_to_recalculate_ui_pos) {
				s_to_recalculate_ui_pos = false;
				CalculateUIPos();
				s_processor.ResetPositionZ();
			}
			if (s_ui_cam == null) {
				if (s_canvas != null && s_canvas.worldCamera != null) {
					s_to_recalculate_ui_pos = true;
				}
				return;
			}
			if (s_ui_cam.nearClipPlane != s_camera_near_clip ||
				s_ui_cam.farClipPlane != s_camera_far_clip ||
				s_ui_cam.orthographicSize != s_camera_size ||
				Screen.width != s_screen_width || Screen.height != s_screen_height) {
				s_to_recalculate_ui_pos = true;
			}
		}

		private static void CalculateUIPos() {
			s_ui_cam = s_canvas.worldCamera;
			s_screen_width = Screen.width;
			s_screen_height = Screen.height;
			if (s_ui_cam == null) {
				s_camera_near_clip = 0f;
				s_camera_far_clip = 0f;
				s_camera_size = 0f;
				s_pos_z_from = 0f;
				s_pos_z_to = 0f;
				return;
			}
			s_camera_near_clip = s_ui_cam.nearClipPlane;
			s_camera_far_clip = s_ui_cam.farClipPlane;
			s_camera_size = s_ui_cam.orthographicSize;
			Transform ct = s_ui_cam.transform;
			Vector3 pos = ct.position;
			Vector3 forward = ct.forward;
			Vector3 ne = pos + forward * s_camera_near_clip;
			Vector3 fe = pos + forward * s_camera_far_clip;
			Transform ut = s_canvas.transform;
			float lne = ut.InverseTransformPoint(ne).z;
			float lfe = ut.InverseTransformPoint(fe).z;
			s_pos_z_from = lfe;
			s_pos_z_to = lne;
		}

		private static void GetStackSortingOrderAndZ(int index, out int sortingOrder, out float z) {
			sortingOrder = s_sorting_order_min + s_sorting_order_range_per_ui * (index + 2);
			z = s_pos_z_from - s_position_z_interval * (index + 2);
		}

		private static void GetFixedSortingORderAndZ(int bias, out int sortingOrder, out float z) {
			if (bias < 0) {
				sortingOrder = s_sorting_order_max - s_sorting_order_range_per_ui + bias;
				z = s_pos_z_to - bias * s_position_z_interval / s_sorting_order_range_per_ui;
				return;
			}
			sortingOrder = s_sorting_order_min + bias;
			z = s_pos_z_from - s_position_z_interval * bias / s_sorting_order_range_per_ui;
		}

		#endregion

		#region Logic Instance Cache

		private static Dictionary<Type, Queue<IUILogicBase>> s_logic_cache = null;
		private static Dictionary<object, Type> s_logic_to_type = null;

		private static Type s_type_logic_base = typeof(IUILogicBase);

		private static IUILogicBase GetLogicInstance(Type type) {
			if (type == null || !type.IsClass || !s_type_logic_base.IsAssignableFrom(type)) { return null; }
			if (s_logic_cache != null && s_logic_cache.TryGetValue(type, out Queue<IUILogicBase> cache)) {
				if (cache.Count > 0) {
					object o = cache.Dequeue();
					s_logic_to_type.Add(o, type);
					return o as IUILogicBase;
				}
			}
			object obj = Activator.CreateInstance(type);
			if (s_logic_to_type != null) { s_logic_to_type.Add(obj, type); }
			return obj as IUILogicBase;
		}

		private static void CacheLogicInstance(IUILogicBase logic) {
			if (logic == null || s_logic_to_type == null) { return; }
			if (!s_logic_to_type.TryGetValue(logic, out Type type)) { return; }
			s_logic_to_type.Remove(logic);
			if (!s_logic_cache.TryGetValue(type, out Queue<IUILogicBase> cache)) {
				cache = new Queue<IUILogicBase>();
				s_logic_cache.Add(type, cache);
			}
			cache.Enqueue(logic);
		}

		#endregion

	}

}

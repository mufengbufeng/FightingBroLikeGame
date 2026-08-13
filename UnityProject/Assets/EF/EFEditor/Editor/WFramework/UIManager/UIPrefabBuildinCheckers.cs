using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.U2D;
using UnityEngine.UI;

using Object = UnityEngine.Object;

namespace EF.UI.WFramework {

	public static class UIPrefabBuildinCheckers {

		public static void CheckMissingReference(GameObject root, IPrefabCheckerError err) {
			string lang = CultureInfo.CurrentCulture.ThreeLetterWindowsLanguageName.ToLower();
			Stack<Transform> stack = new Stack<Transform>();
			stack.Push(root.transform);
			List<Component> components = new List<Component>();
			int count = 0;
			while (stack.Count > 0) {
				Transform t = stack.Pop();
				components.Clear();
				t.GetComponents<Component>(components);
				for (int i = components.Count - 1; i >= 0; i--) {
					Component comp = components[i];
					if (comp != null && !comp.Equals(null)) { continue; }
					switch (lang) {
						case "chs":
							err.LogError($"节点'{t.name}'引用了丢失的脚本。", t);
							break;
						default:
							err.LogError($"Missing MonoScript on '{t.name}'.", t);
							break;
					}
					count++;
					break;
				}
				for (int i = t.childCount - 1; i >= 0; i--) {
					stack.Push(t.GetChild(i));
				}
			}
			if (count > 0) {
				switch (lang) {
					case "chs":
						err.ShowDialog($"当前prefab中有{count}个节点引用了丢失了脚本。\n请在Console窗口中查看详情。");
						break;
					default:
						err.ShowDialog($"Missing MonoScript found in this prefab on {count} node(s).\nSee Console Window for more detail.");
						break;
				}
			}
		}

		public static void CheckReferencingTextureInAtlas(GameObject root, IPrefabCheckerError err) {
			HashSet<string> sprites = new HashSet<string>();
			foreach (string guid in AssetDatabase.FindAssets("t:SpriteAtlas")) {
				string path = AssetDatabase.GUIDToAssetPath(guid);
				SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
				foreach (Object obj in atlas.GetPackables()) {
					string p = AssetDatabase.GetAssetPath(obj);
					if (obj is Sprite sprite) {
						sprites.Add(p);
					} else if (AssetDatabase.IsValidFolder(p)) {
						foreach (string sguid in AssetDatabase.FindAssets("t:Sprite", new string[] { p })) {
							sprites.Add(AssetDatabase.GUIDToAssetPath(sguid));
						}
					} else {
						foreach (var sub in AssetDatabase.LoadAllAssetRepresentationsAtPath(path)) {
							if (sub is Sprite sp) {
								sprites.Add(AssetDatabase.GetAssetPath(sp));
							}
						}
					}
				}
			}
			string lang = CultureInfo.CurrentCulture.ThreeLetterWindowsLanguageName.ToLower();
			Stack<Transform> stack = new Stack<Transform>();
			stack.Push(root.transform);
			List<Component> components = new List<Component>();
			int count = 0;
			while (stack.Count > 0) {
				Transform t = stack.Pop();
				components.Clear();
				t.GetComponents<Component>(components);
				for (int i = components.Count - 1; i >= 0; i--) {
					Component comp = components[i];
					if (comp == null || comp.Equals(null)) { continue; }
					SerializedObject so = new SerializedObject(comp);
					SerializedProperty sp = so.GetIterator();
					while (sp.Next(true)) {
						if (sp.propertyType != SerializedPropertyType.ObjectReference) { continue; }
						Object obj = sp.objectReferenceValue;
						if (obj == null || obj.Equals(null)) { continue; }
						if (obj is Texture texture) {
							string tp = AssetDatabase.GetAssetPath(texture);
							if (!string.IsNullOrEmpty(tp) && sprites.Contains(tp)) {
								switch (lang) {
									case "chs":
										err.LogError($"节点'{t.name}'上'{comp.name}'组件中引用了图集中的贴图'{tp}'。这可能会导致在发布后找不到此贴图资源。", comp);
										break;
									default:
										err.LogError($"Component '{comp.name}' on node '{t.name}' references a texture '{tp}' in SpriteAtlas. Missing references may occur when using assetbundle.", comp);
										break;
								}
								count++;
							}
						} else if (obj is Material material) {
							SerializedObject tso = new SerializedObject(material);
							SerializedProperty tsp = tso.GetIterator();
							while (tsp.Next(true)) {
								if (tsp.propertyType != SerializedPropertyType.ObjectReference) { continue; }
								Object tobj = tsp.objectReferenceValue;
								if (tobj == null || tobj.Equals(null)) { continue; }
								Texture tex = tobj as Texture;
								if (tex == null) { continue; }
								string tp = AssetDatabase.GetAssetPath(tex);
								if (!string.IsNullOrEmpty(tp) && sprites.Contains(tp)) {
									switch (lang) {
										case "chs":
											err.LogError($"节点'{t.name}'上'{comp.name}'组件中使用的材质'{material.name}'引用了图集中的贴图'{tp}'。这可能会导致在发布后找不到此贴图资源。", comp);
											break;
										default:
											err.LogError($"Material '{material.name}' used by component '{comp.name}' on node '{t.name}' references a texture '{tp}' in SpriteAtlas. Missing references may occur when using assetbundle.", comp);
											break;
									}
									count++;
								}
							}
							tso.Dispose();
						}
					}
					so.Dispose();
				}
				for (int i = t.childCount - 1; i >= 0; i--) {
					stack.Push(t.GetChild(i));
				}
			}
			if (count > 0) {
				switch (lang) {
					case "chs":
						err.ShowDialog($"当前prefab中引用了位于图集中的贴图，这可能会导致在发布后找不到此贴图资源。\n请在Console窗口中查看详情。");
						break;
					default:
						err.ShowDialog($"Texture(s) in SpriteAtlas is referenced by this prefab. Missing references may occur when using assetbundle.\nSee Console Window for more detail.");
						break;
				}
			}
		}

		private struct TransInMask {
			public Transform trans;
			public bool inMask;
		}

		public static void CheckContentInMask(GameObject root, IPrefabCheckerError err,
			bool ignoreDisabledCanvas, bool ignoreDisabledRenderer) {
			string lang = CultureInfo.CurrentCulture.ThreeLetterWindowsLanguageName.ToLower();
			Stack<TransInMask> stack = new Stack<TransInMask>();
			stack.Push(new TransInMask() { trans = root.transform, inMask = false });
			int count = 0;
			while (stack.Count > 0) {
				TransInMask t = stack.Pop();
				bool inMask = t.inMask;
				if (inMask) {
					Canvas canvas = t.trans.GetComponent<Canvas>();
					if (canvas != null && canvas.overrideSorting && (!ignoreDisabledCanvas || canvas.enabled)) {
						switch (lang) {
							case "chs":
								err.LogError($"Canvas({t.trans.name})位于Mask之中，但勾选了OverrideSorting，这会导致其不受Mask控制。", t.trans);
								break;
							default:
								err.LogError($"Canvas({t.trans.name}) should be masked but 'OverrideSorting' is checked. Mask will not control the contents in this canvas.", t.trans);
								break;
						}
						count++;
					}
					Renderer renderer = t.trans.GetComponent<Renderer>();
					if (renderer != null && (!ignoreDisabledRenderer || renderer.enabled)) {
						switch (lang) {
							case "chs":
								err.LogError($"{renderer.GetType().Name}({t.trans.name})位于Mask之中，但它不受Mask控制。", t.trans);
								break;
							default:
								err.LogError($"{renderer.GetType().Name}({t.trans.name}) under Mask node will not be controlled by the mask.", t.trans);
								break;
						}
						count++;
					}
				} else {
					inMask = t.trans.GetComponent<Mask>() != null || t.trans.GetComponent<RectMask2D>() != null;
				}
				for (int i = t.trans.childCount - 1; i >= 0; i--) {
					stack.Push(new TransInMask() { trans = t.trans.GetChild(i), inMask = inMask });
				}
			}
			if (count > 0) {
				switch (lang) {
					case "chs":
						err.ShowDialog($"当前prefab中有位于Mask节点下但不受Mask控制的内容。\n请在Console窗口中查看详情。");
						break;
					default:
						err.ShowDialog($"Some contents under mask node will not be masked in this prefab.\nSee Console Window for more detail.");
						break;
				}
			}
		}

		public static void CheckCanvasSortingOrderRange(GameObject root, IPrefabCheckerError err,
			int range, bool ignoreDisabledCanvas, bool ignoreDisabledRenderer) {
			string lang = CultureInfo.CurrentCulture.ThreeLetterWindowsLanguageName.ToLower();
			Stack<Transform> stack = new Stack<Transform>();
			stack.Push(root.transform);
			int min = int.MaxValue;
			Transform tMin = null;
			int max = int.MinValue;
			Transform tMax = null;
			while (stack.Count > 0) {
				Transform t = stack.Pop();
				Canvas canvas = t.GetComponent<Canvas>();
				Renderer renderer = t.GetComponent<Renderer>();
				if (canvas != null && canvas.overrideSorting && (!ignoreDisabledCanvas || canvas.enabled)) {
					if (canvas.sortingOrder < min) {
						min = canvas.sortingOrder;
						tMin = t;
					}
					if (canvas.sortingOrder > max) {
						max = canvas.sortingOrder;
						tMax = t;
					}
				}
				if (renderer != null && (!ignoreDisabledRenderer || renderer.enabled)) {
					if (renderer.sortingOrder < min) {
						min = renderer.sortingOrder;
						tMin = t;
					}
					if (renderer.sortingOrder > max) {
						max = renderer.sortingOrder;
						tMax = t;
					}
				}
				for (int i = t.childCount - 1; i >= 0; i--) {
					stack.Push(t.GetChild(i));
				}
			}
			if (min < max && (max - min) > range) {
				switch (lang) {
					case "chs":
						err.LogError($"Prefab中使用的SortingOrder范围越界({range})。", root);
						err.LogWarning($"SortingOrder最小值：{min}({tMin.name})。", tMin);
						err.LogWarning($"SortingOrder最大值：{max}({tMax.name})。", tMax);
						err.ShowDialog($"Prefab中使用的SortingOrder范围越界({range})。\n请在Console窗口中查看详情。");
						break;
					default:
						err.LogError($"The min-max value of SortingOrder in this prefab is beyond range({range}).", root);
						err.LogWarning($"SortingOrder min value: {min}({tMin.name}).", tMin);
						err.LogWarning($"SortingOrder max value: {max}({tMax.name}).", tMax);
						err.ShowDialog($"The min-max value of SortingOrder in this prefab is beyond range({range}).\nSee Console Window for more detail.");
						break;
				}
			}
		}

		public static void CheckUIRootCanvas(GameObject root, IPrefabCheckerError err) {
			string lang = CultureInfo.CurrentCulture.ThreeLetterWindowsLanguageName.ToLower();
			Canvas canvas = root.GetComponent<Canvas>();
			if (canvas == null) {
				switch (lang) {
					case "chs":
						err.ShowDialog("UI界面根节点应添加Canvas组件。");
						break;
					default:
						err.ShowDialog("'Canvas' component is required on ui panel root.");
						break;
				}
				return;
			}
			if (!canvas.overrideSorting) {
				switch (lang) {
					case "chs":
						err.ShowDialog("UI界面根节点Canvas组件应勾选OverrideSorting。");
						break;
					default:
						err.ShowDialog("'OverrideSorting' on 'Canvas' component of ui panel root should be checked.");
						break;
				}
				return;
			}
		}

		private static HashSet<Type> s_pointer_interfaces = new HashSet<Type>() {
		typeof(IPointerEnterHandler), typeof(IPointerExitHandler),
		typeof(IPointerDownHandler), typeof(IPointerUpHandler), typeof(IPointerClickHandler),
		typeof(IBeginDragHandler), typeof(IEndDragHandler), typeof(IInitializePotentialDragHandler), typeof(IDragHandler),
		typeof(IDropHandler), typeof(IScrollHandler)
	};

		private class CanvasContainer {
			public Canvas canvas;
			public bool hasRaycaster;
			public bool requireRaycaster;
		}

		private struct TransInCanvas {
			public Transform trans;
			public CanvasContainer container;
		}

		public static void CheckGraphicRaycasterOnCanvas(GameObject root, IPrefabCheckerError err) {
			string lang = CultureInfo.CurrentCulture.ThreeLetterWindowsLanguageName.ToLower();
			List<CanvasContainer> containers = new List<CanvasContainer>();
			Stack<TransInCanvas> stack = new Stack<TransInCanvas>();
			stack.Push(new TransInCanvas() { trans = root.transform, container = null });
			List<Component> components = new List<Component>();
			while (stack.Count > 0) {
				TransInCanvas tc = stack.Pop();
				CanvasContainer container = tc.container;
				Canvas canvas = tc.trans.GetComponent<Canvas>();
				if (canvas != null) {
					container = new CanvasContainer() {
						canvas = canvas,
						hasRaycaster = canvas.GetComponent<GraphicRaycaster>() != null,
						requireRaycaster = false
					};
					containers.Add(container);
				}
				if (container != null && !container.requireRaycaster) {
					components.Clear();
					tc.trans.GetComponents<Component>(components);
					for (int i = components.Count - 1; i >= 0 && !container.requireRaycaster; i--) {
						Component comp = components[i];
						if (comp == null || comp.Equals(null)) { continue; }
						foreach (Type ti in comp.GetType().GetInterfaces()) {
							if (!s_pointer_interfaces.Contains(ti)) { continue; }
							container.requireRaycaster = true;
							break;
						}
					}
				}
				for (int i = tc.trans.childCount - 1; i >= 0; i--) {
					stack.Push(new TransInCanvas() {
						trans = tc.trans.GetChild(i),
						container = container
					});
				}
			}
			int count = 0;
			for (int i = 0; i < containers.Count; i++) {
				CanvasContainer container = containers[i];
				if (container.hasRaycaster || !container.requireRaycaster) { continue; }
				switch (lang) {
					case "chs":
						err.LogError($"Canvas({container.canvas.name})节点下有需要鼠标或触摸交互的组件，但此Canvas节点上未挂有GraphicRaycaster。", container.canvas.transform);
						break;
					default:
						err.LogError($"'GraphicRaycaster' is required on Canvas({container.canvas.name}) because some components under it requires mouse or touch interactives.", container.canvas.transform);
						break;
				}
				count++;
			}
			if (count > 0) {
				switch (lang) {
					case "chs":
						err.ShowDialog($"当前prefab中有{count}个节Canvas点需要挂但未挂有GraphicRaycaster组件。\n请在Console窗口中查看详情。");
						break;
					default:
						err.ShowDialog($"{count} Canvas(es) in this prefab require 'GraphicRaycaster' but not found.\nSee Console Window for more detail.");
						break;
				}
			}
		}

		public struct ProtectedComponent {
			public Transform node;
			public Type component;
			public Func<string, bool> check_field_legal;
		}

		public static void CheckIllegallyAnimated(GameObject root, IEnumerable<ProtectedComponent> invalids, IPrefabCheckerError err) {
			string lang = CultureInfo.CurrentCulture.ThreeLetterWindowsLanguageName.ToLower();
			List<EditorCurveBinding> illegals = new List<EditorCurveBinding>();
			List<ProtectedClipTarget> protecteds = new List<ProtectedClipTarget>();
			Stack<Transform> stack = new Stack<Transform>();
			stack.Push(root.transform);
			int count = 0;
			while (stack.Count > 0) {
				Transform t = stack.Pop();
				Animation animation = t.GetComponent<Animation>();
				Animator animator = t.GetComponent<Animator>();
				AnimatorController controller = null;
				bool protectedsInited = false;
				if (animator != null) {
					controller = animator.runtimeAnimatorController as AnimatorController;
				}
				if (animation != null) {
					foreach (object st in animation) {
						AnimatorState state = st as AnimatorState;
						if (state == null) { continue; }
						AnimationClip clip = state.motion as AnimationClip;
						if (clip == null) { continue; }
						if (!protectedsInited) {
							protectedsInited = true;
							CollectProtectedTargets(t, invalids, protecteds);
						}
						if (protecteds.Count <= 0) { continue; }
						illegals.Clear();
						CheckPropertyIllegallyAnimated(clip, protecteds, illegals);
						foreach (EditorCurveBinding illegal in illegals) {
							string p = illegal.path;
							switch (lang) {
								case "chs":
									if (string.IsNullOrEmpty(p)) { p = "自身"; } else { p = $"'{p}'"; }
									err.LogError($"节点{t.name}上{clip.name}动画非法控制了{p}节点的{illegal.type.Name}。", t);
									break;
								default:
									string animnode = t.name;
									if (string.IsNullOrEmpty(p)) { p = animnode; animnode = "the node itself"; } else { animnode = $"'{animnode}'"; }
									err.LogError($"'{illegal.type.Name}' on node '{p}' is illegally controlled by AnimationClip({clip.name}) on {animnode}", t);
									break;
							}
							count++;
						}
					}
				}
				if (controller != null) {
					foreach (AnimatorControllerLayer layer in controller.layers) {
						AnimatorStateMachine sm = layer.stateMachine;
						if (sm == null) { continue; }
						foreach (ChildAnimatorState state in sm.states) {
							AnimationClip clip = state.state.motion as AnimationClip;
							if (clip == null) { continue; }
							if (!protectedsInited) {
								protectedsInited = true;
								CollectProtectedTargets(t, invalids, protecteds);
							}
							if (protecteds.Count <= 0) { continue; }
							illegals.Clear();
							CheckPropertyIllegallyAnimated(clip, protecteds, illegals);
							foreach (EditorCurveBinding illegal in illegals) {
								string p = illegal.path;
								switch (lang) {
									case "chs":
										if (string.IsNullOrEmpty(p)) { p = "自身"; } else { p = $"'{p}'"; }
										err.LogError($"节点{t.name}上{state.state.name}状态({clip.name}动画)非法控制了{p}节点的{illegal.type.Name}。", t);
										break;
									default:
										string animnode = t.name;
										if (string.IsNullOrEmpty(p)) { p = animnode; animnode = "the node itself"; } else { animnode = $"'{animnode}'"; }
										err.LogError($"'{illegal.type.Name}' on node '{p}' is illegally controlled by state '{state.state.name}(clip:{clip.name})' on {animnode}", t);
										break;
								}
								count++;
							}
						}
					}
				}
				protecteds.Clear();
				for (int i = t.childCount - 1; i >= 0; i--) {
					stack.Push(t.GetChild(i));
				}
			}
			if (count > 0) {
				switch (lang) {
					case "chs":
						err.ShowDialog("当前prefab中有受AnimationClip非法控制的属性。\n请在Console窗口中查看详情。");
						break;
					default:
						err.ShowDialog("Some properties of components are illegally controlled by AnimationClip in this prefab.\nSee Console Window for more detail.");
						break;
				}
			}
		}

		private struct ProtectedClipTarget {
			public string path;
			public ProtectedComponent comp;
		}

		private static Type s_type_gameobject = typeof(GameObject);

		private static void CheckPropertyIllegallyAnimated(AnimationClip clip, List<ProtectedClipTarget> invalids, List<EditorCurveBinding> illegals) {
			string pp = null;
			Type pt = null;
			foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip)) {
				string path = binding.path;
				Type type = binding.type;
				if (pp == path && pt == type) { continue; }
				for (int i = invalids.Count - 1; i >= 0; i--) {
					ProtectedClipTarget invalid = invalids[i];
					if (invalid.path != path) { continue; }
					Type t = invalid.comp.component ?? s_type_gameobject;
					if (t != type && !type.IsSubclassOf(t)) { continue; }
					Func<string, bool> checker = invalid.comp.check_field_legal;
					bool valid = checker != null && checker(binding.propertyName);
					if (valid) { continue; }
					illegals.Add(binding);
					pp = path;
					pt = type;
					break;
				}
			}
		}

		private static List<string> s_temp_node_paths = new List<string>(8);
		private static string CalculateRelativePath(Transform parent, Transform child) {
			if (child == null) { return null; }
			bool flag = parent == null;
			s_temp_node_paths.Clear();
			Transform t = child;
			while (t != null) {
				if (t == parent) { flag = true; break; }
				s_temp_node_paths.Add(t.name);
				t = t.parent;
			}
			if (!flag) { return null; }
			s_temp_node_paths.Reverse();
			return string.Join("/", s_temp_node_paths);
		}

		private static void CollectProtectedTargets(Transform root, IEnumerable<ProtectedComponent> invalids, List<ProtectedClipTarget> results) {
			foreach (ProtectedComponent invalid in invalids) {
				string path = CalculateRelativePath(root, invalid.node);
				if (path == null) { continue; }
				results.Add(new ProtectedClipTarget() { path = path, comp = invalid });
			}
		}
	}
}

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
#if UNITY_2021_2_OR_NEWER
using UnityEditor.SceneManagement;
#else
using UnityEditor.Experimental.SceneManagement;
#endif
using UnityEngine;

using Object = UnityEngine.Object;

namespace EF.UI.WFramework {

	public static class UIPrefabCheckerStarter {

		[InitializeOnLoadMethod]
		static void OnEditorLoaded() {
			PrefabStage.prefabStageOpened += OnPrefabStageOpened;
			PrefabStage.prefabSaved += OnPrefabSaved;
			PrefabUtility.prefabInstanceUpdated += OnPrefabInstanceUpdated;
		}

		private static void OnPrefabStageOpened(PrefabStage stage) {
			ReadyCheckPrefab(stage.prefabContentsRoot,
#if UNITY_2021_1_OR_NEWER
				stage.assetPath
#else
				stage.prefabAssetPath
#endif
				);
		}

		private static void OnPrefabSaved(GameObject go) {
			PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
			if (stage.prefabContentsRoot == go) {
				ReadyCheckPrefab(go,
#if UNITY_2021_1_OR_NEWER
				stage.assetPath
#else
				stage.prefabAssetPath
#endif
				);
			}
		}

		private static void OnPrefabInstanceUpdated(GameObject go) {
			ReadyCheckPrefab(go, PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go));
		}

		private static void ReadyCheckPrefab(GameObject go, string path) {
			InitCheckers();
			int n = s_checkers.Count;
			if (n <= 0) { return; }
			Error err = new Error();
			for (int i = 0; i < n; i++) {
				PrefabChecker checker = s_checkers[i];
				bool match;
				if (checker.IsRegex) {
					match = Regex.IsMatch(path, checker.Pattern);
				} else {
					match = path.StartsWith(checker.Pattern);
				}
				if (!match) { continue; }
				checker.Checker.Invoke(go, path, err);
			}
		}

		private class Error : IPrefabCheckerError {
			void IPrefabCheckerError.LogError(string msg, Object context) {
				Debug.LogError(msg, context);
			}
			void IPrefabCheckerError.LogWarning(string msg, Object context) {
				Debug.LogWarning(msg, context);
			}
			void IPrefabCheckerError.ShowDialog(string msg) {
				EditorUtility.DisplayDialog("Prefab Checker", msg, "OK");
			}
		}

		private enum eCheckMethodParaType {
			Error, GameObject, String
		}

		private class CheckerMethod {
			private MethodInfo mMethod;
			private eCheckMethodParaType[] mParaTypes;
			private CheckerMethod() { }
			public void Invoke(GameObject go, string path, IPrefabCheckerError err) {
				object[] args = null;
				int pn = mParaTypes.Length;
				switch (pn) {
					case 2: args = s_args_2; break;
					case 3: args = s_args_3; break;
				}
				if (args == null) { return; }
				for (int i = 0; i < pn; i++) {
					switch (mParaTypes[i]) {
						case eCheckMethodParaType.GameObject:
							args[i] = go;
							break;
						case eCheckMethodParaType.String:
							args[i] = path;
							break;
						case eCheckMethodParaType.Error:
							args[i] = err;
							break;
					}
				}
				try {
					mMethod.Invoke(null, args);
				} catch (Exception e) {
					Debug.LogException(e);
				}
			}
			public static CheckerMethod Get(MethodInfo method) {
				if (method.ContainsGenericParameters) {
					// 不可以有泛型参数
					return null;
				}
				ParameterInfo[] parameters = method.GetParameters();
				int lenp = parameters.Length;
				if (lenp > 3 || lenp <= 1) {
					// 参数不可以为空，也不可以超过两项
					return null;
				}
				s_temp_para_types.Clear();
				bool hasError = false;
				bool hasGameObject = false;
				for (int i = 0; i < lenp; i++) {
					ParameterInfo para = parameters[i];
					Type t = para.ParameterType;
					eCheckMethodParaType pt = eCheckMethodParaType.GameObject;
					if (t == s_type_gameobject) {
						pt = eCheckMethodParaType.GameObject;
						hasGameObject = true;
					} else if (t == s_type_string) {
						pt = eCheckMethodParaType.String;
					} else if (t == s_type_error) {
						pt = eCheckMethodParaType.Error;
						hasError = true;
					} else {
						// 参数仅可以是IPrefabCheckerError、GameObject或String
						return null;
					}
					if (s_temp_para_types.Contains(pt)) {
						// 参数类型重复
						return null;
					}
					s_temp_para_types.Add(pt);
				}
				if (!hasError) {
					// 必要参数
					return null;
				}
				if (!hasGameObject) {
					// 必要参数
					return null;
				}
				return new CheckerMethod() { mMethod = method, mParaTypes = s_temp_para_types.ToArray() };
			}
			private static readonly Type s_type_gameobject = typeof(GameObject);
			private static readonly Type s_type_string = typeof(string);
			private static readonly Type s_type_error = typeof(IPrefabCheckerError);
			private static readonly List<eCheckMethodParaType> s_temp_para_types = new List<eCheckMethodParaType>(2);
			private static readonly object[] s_args_2 = new object[2];
			private static readonly object[] s_args_3 = new object[3];
		}

		private struct PrefabChecker {
			public bool IsRegex;
			public string Pattern;
			public CheckerMethod Checker;
		}

		private static List<PrefabChecker> s_checkers = null;

		private static void InitCheckers() {
			if (s_checkers != null) { return; }
			s_checkers = new List<PrefabChecker>();
			BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
				foreach (Type type in assembly.GetTypes()) {
					foreach (MethodInfo method in type.GetMethods(flags)) {
						CheckerMethod cm = null;
						foreach (PrefabCheckerAttribute attr in method.GetCustomAttributes<PrefabCheckerAttribute>()) {
							if (string.IsNullOrEmpty(attr.Pattern)) { continue; }
							if (cm == null) {
								cm = CheckerMethod.Get(method);
								if (cm == null) { break; }
							}
							s_checkers.Add(new PrefabChecker() {
								IsRegex = attr.IsRegex,
								Pattern = attr.Pattern,
								Checker = cm
							});
						}
					}
				}
			}
		}

	}

	public class PrefabCheckerAttribute : Attribute {
		public readonly bool IsRegex;
		public readonly string Pattern;
		public PrefabCheckerAttribute(bool isRegex, string pattern) {
			IsRegex = isRegex;
			Pattern = pattern;
		}
	}

	public interface IPrefabCheckerError {
		void LogError(string msg, Object context);
		void LogWarning(string msg, Object context);
		void ShowDialog(string msg);
	}

}

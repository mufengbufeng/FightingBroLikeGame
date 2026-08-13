using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

namespace EF.UI.WFramework {

	public static class UILogicGenerator {

		[MenuItem("Assets/Create/EF W-Framework/UI Logic/Fixed Template Define", true)]
		static bool CanCreateFixedTemplateDefine() {
			Init();
			return s_type_fixed_tpl == null;
		}

		[MenuItem("Assets/Create/EF W-Framework/UI Logic/Fixed Template Define", false)]
		static void CreateFixedTemplateDefine() {
			string dir = AssetDatabase.GetAssetPath(Selection.activeObject);
			if (!AssetDatabase.IsValidFolder(dir)) {
				dir = Path.GetDirectoryName(dir);
			}
			if (!dir.EndsWith("/")) { dir += "/"; }
			string path = dir + "UILogicTemplateFixed.cs";
			ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
				0,
				ScriptableObject.CreateInstance<TplCreatedAction>(),
				path,
				EditorGUIUtility.IconContent("cs Script Icon").image as Texture2D,
				"IUIFixedLogicTemplateDefine"
			);
		}

		[MenuItem("Assets/Create/EF W-Framework/UI Logic/Stack Template Define", true)]
		static bool CanCreateStackTemplateDefine() {
			Init();
			return s_type_stack_tpl == null;
		}

		[MenuItem("Assets/Create/EF W-Framework/UI Logic/Stack Template Define", false)]
		static void CreateStackTemplateDefine() {
			string dir = AssetDatabase.GetAssetPath(Selection.activeObject);
			if (!AssetDatabase.IsValidFolder(dir)) {
				dir = Path.GetDirectoryName(dir);
			}
			if (!dir.EndsWith("/")) { dir += "/"; }
			string path = dir + "UILogicTemplateStack.cs";
			ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
				0,
				ScriptableObject.CreateInstance<TplCreatedAction>(),
				path,
				EditorGUIUtility.IconContent("cs Script Icon").image as Texture2D,
				"IUIStackLogicTemplateDefine"
			);
		}

		[MenuItem("Assets/Create/EF W-Framework/UI Logic/Fixed UI Logic", true)]
		static bool CanCreateFixedLogic() {
			Init();
			if (s_type_fixed_tpl == null) { return false; }
			if (GetSelectedMonoScript() == null) { return false; }
			return true;
		}

		[MenuItem("Assets/Create/EF W-Framework/UI Logic/Fixed UI Logic", false)]
		static void CreateFixedLogic() {
			MonoScript script = GetSelectedMonoScript();
			IUIFixedLogicTemplateDefine tpl = Activator.CreateInstance(s_type_fixed_tpl, new object[] { script }) as IUIFixedLogicTemplateDefine;
			CreateLogic(new FixedTplDefine(script, tpl));
		}

		[MenuItem("Assets/Create/EF W-Framework/UI Logic/Stack UI Logic", true)]
		static bool CanCreateStackLogic() {
			Init();
			if (s_type_stack_tpl == null) { return false; }
			if (GetSelectedMonoScript() == null) { return false; }
			return true;
		}

		[MenuItem("Assets/Create/EF W-Framework/UI Logic/Stack UI Logic", false)]
		static void CreateStackLogic() {
			MonoScript script = GetSelectedMonoScript();
			IUIStackLogicTemplateDefine tpl = Activator.CreateInstance(s_type_stack_tpl, new object[] { script }) as IUIStackLogicTemplateDefine;
			CreateLogic(new StackTplDefine(script, tpl));
		}

		private static void CreateLogic(ITplDefine tpl) {
			MonoScript script = tpl.Script;
			string ns = tpl.GetNameSpace();
			string path = tpl.GetCodePath();
			if (string.IsNullOrEmpty(path)) {
				EditorUtility.DisplayDialog("Generate Logic", "Invalid code path !", "OK");
				return;
			}
			string cls = Path.GetFileNameWithoutExtension(path);
			Type bt = tpl.GetBaseType();
			if (bt == null) {
				EditorUtility.DisplayDialog("Generate Logic", "Invalid base type !", "OK");
				return;
			}
			Type mono = script.GetClass();
			List<string> usings = new List<string>();
			usings.Add("UnityEngine");
			usings.Add(bt.Namespace);
			usings.Add(mono.Namespace);
			if (!string.IsNullOrEmpty(ns)) { while (usings.Remove(ns)) { } }
			usings.Sort();
			StringBuilder code = new StringBuilder();
			foreach (string s in usings.Distinct()) {
				if (string.IsNullOrEmpty(s)) { continue; }
				code.AppendLine($"using {s};");
			}
			code.AppendLine();
			string indent = "";
			if (!string.IsNullOrEmpty(ns)) {
				indent = "\t";
				code.AppendLine($"namespace {ns} {{");
				code.AppendLine();
			}
			code.AppendLine($"{indent}public class {cls} : {bt.Name} {{");
			code.AppendLine();
			code.AppendLine($"{indent}\tprivate {mono.Name} mUI;");
			code.AppendLine();
			List<string> props = new List<string>();
			tpl.GetPropertiesDefine(props);
			foreach (string prop in props) {
				code.AppendLine($"{indent}\t{prop}");
			}
			if (props.Count > 0) { code.AppendLine(); }
			tpl.GetOnOpenDefine(out string onopen, out string govar);
			code.AppendLine($"{indent}\t{onopen} {{");
			code.AppendLine($"{indent}\t\tmUI = {govar}.GetComponent<{mono.Name}>();");
			code.AppendLine($"{indent}\t\t// TODO");
			code.AppendLine($"{indent}\t\tmUI.Open();");
			code.AppendLine($"{indent}\t}}");
			code.AppendLine();
			code.AppendLine($"{indent}\t{tpl.GetOnCloseDefine()} {{");
			code.AppendLine($"{indent}\t\tmUI.Clear();");
			code.AppendLine($"{indent}\t\tmUI = null;");
			code.AppendLine($"{indent}\t}}");
			code.AppendLine();
			code.AppendLine($"{indent}}}");
			code.AppendLine();
			if (!string.IsNullOrEmpty(ns)) {
				code.AppendLine("}");
				code.AppendLine();
			}
			string dir = Path.GetDirectoryName(path);
			if (!Directory.Exists(dir)) { Directory.CreateDirectory(dir); }
			File.WriteAllText(path, code.ToString(), Encoding.UTF8);
			AssetDatabase.Refresh();
		}

		private interface ITplDefine {
			MonoScript Script { get; }
			string GetNameSpace();
			string GetCodePath();
			Type GetBaseType();
			void GetPropertiesDefine(List<string> properties);
			void GetOnOpenDefine(out string def, out string gameObjectVar);
			string GetOnCloseDefine();
		}

		private class FixedTplDefine : ITplDefine {
			private MonoScript mScript;
			private IUIFixedLogicTemplateDefine mDef;
			public FixedTplDefine(MonoScript script, IUIFixedLogicTemplateDefine def) { mScript = script; mDef = def; }
			MonoScript ITplDefine.Script { get { return mScript; } }
			Type ITplDefine.GetBaseType() { return mDef.GetBaseType(); }
			string ITplDefine.GetCodePath() { return mDef.GetCodePath(); }
			string ITplDefine.GetNameSpace() { return mDef.GetNameSpace(); }
			string ITplDefine.GetOnCloseDefine() { return mDef.GetOnCloseDefine(); }
			void ITplDefine.GetOnOpenDefine(out string def, out string gameObjectVar) { mDef.GetOnOpenDefine(out def, out gameObjectVar); }
			void ITplDefine.GetPropertiesDefine(List<string> properties) { mDef.GetPropertiesDefine(properties); }
		}

		private class StackTplDefine : ITplDefine {
			private MonoScript mScript;
			private IUIStackLogicTemplateDefine mDef;
			public StackTplDefine(MonoScript script, IUIStackLogicTemplateDefine def) { mScript = script; mDef = def; }
			MonoScript ITplDefine.Script { get { return mScript; } }
			Type ITplDefine.GetBaseType() { return mDef.GetBaseType(); }
			string ITplDefine.GetCodePath() { return mDef.GetCodePath(); }
			string ITplDefine.GetNameSpace() { return mDef.GetNameSpace(); }
			string ITplDefine.GetOnCloseDefine() { return mDef.GetOnCloseDefine(); }
			void ITplDefine.GetOnOpenDefine(out string def, out string gameObjectVar) { mDef.GetOnOpenDefine(out def, out gameObjectVar); }
			void ITplDefine.GetPropertiesDefine(List<string> properties) { mDef.GetPropertiesDefine(properties); }
		}

		private class TplCreatedAction : EndNameEditAction {
			public override void Action(int instanceId, string pathName, string resourceFile) {
				string fn = Path.GetFileNameWithoutExtension(pathName);
				string impl = resourceFile;
				StringBuilder code = new StringBuilder();
				code.AppendLine("using EF.UI.WFramework;");
				code.AppendLine("using System;");
				code.AppendLine("using System.Collections.Generic;");
				code.AppendLine("using System.IO;");
				code.AppendLine("using UnityEditor;");
				code.AppendLine();
				code.AppendLine($"public class {fn} : {impl} {{");
				code.AppendLine();
				code.AppendLine("\tprivate MonoScript mScript;");
				code.AppendLine();
				code.AppendLine($"\tpublic {fn}(MonoScript script) {{");
				code.AppendLine("\t\tmScript = script;");
				code.AppendLine("\t}");
				code.AppendLine();
				code.AppendLine($"\tstring {impl}.GetNameSpace() {{");
				code.AppendLine("\t\t// TODO");
				code.AppendLine("\t\treturn null;");
				code.AppendLine("\t}");
				code.AppendLine();
				code.AppendLine($"\tstring {impl}.GetCodePath() {{");
				code.AppendLine("\t\t// TODO");
				code.AppendLine("\t\tstring path = AssetDatabase.GetAssetPath(mScript);");
				code.AppendLine("\t\tstring dir = Path.GetDirectoryName(path);");
				code.AppendLine("\t\tstring fn = Path.GetFileNameWithoutExtension(path);");
				code.AppendLine("\t\treturn dir + \"/\" + fn + \"Logic.cs\";");
				code.AppendLine("\t}");
				code.AppendLine();
				code.AppendLine($"\tType {impl}.GetBaseType() {{");
				code.AppendLine("\t\t// TODO");
				code.AppendLine("\t\treturn mScript.GetClass().Namespace;");
				code.AppendLine("\t}");
				code.AppendLine();
				code.AppendLine($"\tvoid {impl}.GetPropertiesDefine(List<string> properties) {{");
				code.AppendLine("\t\t// TODO");
				code.AppendLine("\t}");
				code.AppendLine();
				code.AppendLine($"\tvoid {impl}.GetOnOpenDefine(out string def, out string gameObjectVar) {{");
				code.AppendLine("\t\t// TODO");
				code.AppendLine("\t\tdef = \"protected override void OnOpen(GameObject go, int baseSortingOrder)\";");
				code.AppendLine("\t\tgameObjectVar = \"go\";");
				code.AppendLine("\t}");
				code.AppendLine();
				code.AppendLine($"\tstring {impl}.GetOnCloseDefine() {{");
				code.AppendLine("\t\t// TODO");
				code.AppendLine("\t\treturn \"protected override void OnClose()\";");
				code.AppendLine("\t}");
				code.AppendLine();
				code.AppendLine("}");
				File.WriteAllText(pathName, code.ToString(), Encoding.UTF8);
				AssetDatabase.Refresh();
			}
		}

		private static bool s_inited = false;

		private static Type s_type_fixed_tpl;
		private static Type s_type_stack_tpl;

		private static void Init() {
			if (s_inited) { return; }
			s_inited = true;
			Type typeFixedTpl = typeof(IUIFixedLogicTemplateDefine);
			Type typeStackTpl = typeof(IUIStackLogicTemplateDefine);
			foreach (Assembly assmebly in AppDomain.CurrentDomain.GetAssemblies()) {
				foreach (Type type in assmebly.GetTypes()) {
					if (!type.IsClass) { continue; }
					if (typeFixedTpl.IsAssignableFrom(type)) {
						if (s_type_fixed_tpl != null) {
							Debug.LogWarning("More than one class implements 'IUIFixedLogicTemplateDefine' !");
						} else {
							s_type_fixed_tpl = type;
						}
					}
					if (typeStackTpl.IsAssignableFrom(type)) {
						if (s_type_stack_tpl != null) {
							Debug.LogWarning("More than one class implements 'IUIStackLogicTemplateDefine' !");
						} else {
							s_type_stack_tpl = type;
						}
					}
				}
			}
		}

		private static MonoScript GetSelectedMonoScript() {
			if (!(Selection.activeObject is MonoScript script)) { return null; }
			if (!script.GetClass().IsSubclassOf(typeof(MonoBehaviour))) { return null; }
			return script;
		}

	}

}

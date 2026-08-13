using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace EF.DataDriven {

	public class RamDataCodeGenerator {

		[MenuItem("EF/Data Driven/Regenerate Code &#%d")]
		static void RegenerateCode() {
			List<Type> types = CollectDataDrivenTypes();
			HashSet<string> typenames = new HashSet<string>();
			foreach (Type type in types) { typenames.Add(type.Name); }
			List<string> codepaths = new List<string>();
			foreach (string guid in AssetDatabase.FindAssets("t:script")) {
				string path = AssetDatabase.GUIDToAssetPath(guid);
				string fn = Path.GetFileNameWithoutExtension(path);
				if (typenames.Contains(fn)) { codepaths.Add(path); }
			}
			Dictionary<string, TypeCount> dict = new Dictionary<string, TypeCount>();
			foreach (Type type in types) {
				dict.Add(type.FullName, new TypeCount() { type = type, count = 0 });
			}
			string basetype = Regex.Match(s_type_custom_base.Name, @"\w+").Value;
			List<FileData> srcs = new List<FileData>();
			foreach (string path in codepaths) {
				string code = File.ReadAllText(path, Encoding.UTF8);
				MatchCollection matchNS = Regex.Matches(code, @"namespace\s+((\w+\s*\.\s*)*\w+)\s*");
				if (matchNS.Count > 1) { continue; }
				FileData src = null;
				string name_space = matchNS.Count == 1 ? Regex.Replace(matchNS[0].Groups[1].Value, @"\s+", "") : null;
				List<KeyValuePair<int, string>> fields = new List<KeyValuePair<int, string>>(32);
				foreach (Match match in Regex.Matches(code, @"public\s+(\w+\s*\.\s*)*\w+\s*(<\s*(\w+\s*,\s*)?\w+\s*>)?\s+(\w+)\s*\W")) {
					fields.Add(new KeyValuePair<int, string>(match.Index, match.Groups[4].Value));
				}
				MatchCollection match_cls = Regex.Matches(code, @"public\s+((\w+)\s+)*class\s+(\w+)\s*:\s*(((\w+)\s*\.\s*)*(\w+))\s*<\s*(\w+)\s*>");
				for (int i = match_cls.Count - 1; i >= 0; i--) {
					Match match = match_cls[i];
					List<string> fields_sort = new List<string>();
					while (true) {
						int n = fields.Count;
						if (n <= 0) { break; }
						KeyValuePair<int, string> field = fields[n - 1];
						if (field.Key < match.Index) { break; }
						if (field.Key >= match.Index) {
							fields.RemoveAt(n - 1);
						}
						fields_sort.Add(field.Value);
					}
					fields_sort.Reverse();
					string classname = match.Groups[3].Value;
					if (classname != match.Groups[8].Value) {
						// Invalid type define
						continue;
					}
					if (match.Groups[7].Value != basetype) {
						// ignored class : classname
						Debug.LogError($"Invalid class '{classname}' and it will be ignored !");
						continue;
					}
					TypeCount tc;
					if (!dict.TryGetValue(string.IsNullOrEmpty(name_space) ? classname : $"{name_space}.{classname}", out tc)) { continue; }
					tc.count++;
					if (src == null) {
						src = new FileData() { path = path, name_space = name_space, classes = new List<ClassData>() };
						srcs.Add(src);
					}
					List<string> class_modifiers = new List<string>();
					foreach (Capture c in match.Groups[2].Captures) {
						class_modifiers.Add(c.Value);
					}
					List<KeyValuePair<int, MemberData>> members = new List<KeyValuePair<int, MemberData>>();
					foreach (MemberInfo mi in tc.type.GetMembers(BindingFlags.Public | BindingFlags.Instance)) {
						if (mi.DeclaringType != tc.type) { continue; }
						PropertyInfo pi = mi as PropertyInfo;
						FieldInfo fi = mi as FieldInfo;
						if (pi == null && fi == null) { continue; }
						Type ct = ConvertType(pi == null ? fi.FieldType : pi.PropertyType);
						int si = fields_sort.IndexOf(mi.Name);
						if (si < 0) { si = fields_sort.Count + members.Count; }
						members.Add(new KeyValuePair<int, MemberData>(si, new MemberData() { type = ct, name = mi.Name }));
					}
					members.Sort((KeyValuePair<int, MemberData> a, KeyValuePair<int, MemberData> b) => {
						return Comparer<int>.Default.Compare(a.Key, b.Key);
					});
					List<MemberData> sorted = new List<MemberData>();
					foreach (KeyValuePair<int, MemberData> m in members) { sorted.Add(m.Value); }
					src.classes.Add(new ClassData() { type = tc.type, class_modifers = class_modifiers, members = sorted });
				}
				if (src != null) { src.classes.Reverse(); }
			}
			foreach (var kv in dict) {
				if (kv.Value.count <= 0) {
					Debug.LogError($"Type '{kv.Key}' define not found !");
				} else if (kv.Value.count > 1) {
					Debug.LogError($"Type '{kv.Key}' defined more than once !");
				}
			}
			foreach (FileData src in srcs) { GenerateCode(src); }
			AssetDatabase.Refresh();
		}

		private static List<Type> CollectDataDrivenTypes() {
			List<Type> types = new List<Type>();
			Type tsb = typeof(RamDataNodeStructBase);
			foreach (Assembly assemble in AppDomain.CurrentDomain.GetAssemblies()) {
				foreach (Type t in assemble.GetTypes()) {
					if (!t.IsSubclassOf(tsb)) { continue; }
					Type tb = t.BaseType;
					if (!tb.IsGenericType || tb.GetGenericTypeDefinition() != s_type_custom_base) { continue; }
					types.Add(t);
				}
			}
			return types;
		}

		private static void GenerateCode(FileData src) {
			HashSet<string> usings = new HashSet<string>();
			if (!string.IsNullOrEmpty(s_type_custom_base.Namespace)) { usings.Add(s_type_custom_base.Namespace); }
			if (!string.IsNullOrEmpty(s_type_struct_ctrl.Namespace)) { usings.Add(s_type_struct_ctrl.Namespace); }
			if (!string.IsNullOrEmpty(s_type_data_ctrl.Namespace)) { usings.Add(s_type_data_ctrl.Namespace); }
			foreach (ClassData cls in src.classes) {
				foreach (MemberData md in cls.members) {
					GetTypeName(md.type, usings);
				}
			}
			string basetype = Regex.Match(s_type_custom_base.Name, @"\w+").Value;
			StringBuilder code = new StringBuilder();
			if (!string.IsNullOrEmpty(src.name_space)) { usings.Remove(src.name_space); }
			List<string> using_list = new List<string>(usings);
			using_list.Sort();
			foreach (string u in using_list) {
				code.AppendLine($"using {u};");
			}
			if (using_list.Count > 0) { code.AppendLine(); }
			string indent = "";
			if (!string.IsNullOrEmpty(src.name_space)) {
				code.AppendLine($"namespace {src.name_space} {{");
				indent = "\t";
				code.AppendLine();
			}
			foreach (ClassData cls in src.classes) {
				string modifiers = "";
				if (cls.class_modifers.Count > 0) {
					modifiers = string.Join(" ", cls.class_modifers) + " ";
				}
				string cn = cls.type.Name;
				code.AppendLine($"{indent}public {modifiers}class {cn} : {basetype}<{cn}> {{");
				code.AppendLine();
				code.AppendLine($"{indent}\tpublic {cn}(IRamDataStructCtrl parent, out IRamDataCtrl ctrl) : base(parent, out ctrl) {{");
				foreach (MemberData md in cls.members) {
					string tn = GetTypeName(md.type, null);
					if (md.type.IsGenericType) {
						Type[] ats = md.type.GenericTypeArguments;
						string et = GetTypeName(ats[ats.Length - 1], null);
						code.AppendLine($"{indent}\t\t{md.name} = new {tn}(mCtrl, (IRamDataStructCtrl p, out IRamDataCtrl c) => {{ return new {et}(p, out c); }}, out m_{md.name}Ctrl);");
					} else {
						code.AppendLine($"{indent}\t\t{md.name} = new {tn}(mCtrl, out m_{md.name}Ctrl);");
					}
				}
				code.AppendLine($"{indent}\t}}");
				code.AppendLine($"{indent}\tpublic {cn}() : this(null, out _) {{ }}");
				code.AppendLine();
				foreach (MemberData md in cls.members) {
					string tn = GetTypeName(md.type, null);
					code.AppendLine($"{indent}\tpublic {tn} {md.name} {{ get; private set; }}");
					code.AppendLine();
				}
				code.AppendLine($"{indent}\tpublic override string ToString() {{");
				code.Append($"{indent}\t\treturn $\"[{cls.type.Name}]{{{{");
				bool first = true;
				foreach (MemberData md in cls.members) {
					if (first) { first = false; } else { code.Append(","); }
					code.Append($"\\\"{md.name}\\\":{{{md.name}}}");
				}
				code.AppendLine("}}\";");
				code.AppendLine($"{indent}\t}}");
				code.AppendLine();
				code.AppendLine($"{indent}\t#region internals");
				code.AppendLine();
				code.AppendLine($"{indent}\tprotected override eRamDataNodeChangedType CollectAndNotifyChanged() {{");
				code.AppendLine($"{indent}\t\teRamDataNodeChangedType ret = eRamDataNodeChangedType.None;");
				foreach (MemberData md in cls.members) {
					code.AppendLine($"{indent}\t\tret = CombineNodeChangedType(ret, m_{md.name}Ctrl.CollectAndNotifyChanged());");
				}
				code.AppendLine($"{indent}\t\treturn ret;");
				code.AppendLine($"{indent}\t}}");
				code.AppendLine();
				foreach (MemberData md in cls.members) {
					code.AppendLine($"{indent}\tprivate IRamDataCtrl m_{md.name}Ctrl;");
				}
				code.AppendLine();
				code.AppendLine($"{indent}\tprotected override void Reset() {{");
				code.AppendLine($"{indent}\t\tbase.Reset();");
				foreach (MemberData md in cls.members) {
					code.AppendLine($"{indent}\t\tm_{md.name}Ctrl.Reset();");
				}
				code.AppendLine($"{indent}\t}}");
				code.AppendLine();
				code.AppendLine($"{indent}\tprotected override void Dispose() {{");
				code.AppendLine($"{indent}\t\tbase.Dispose();");
				foreach (MemberData md in cls.members) {
					code.AppendLine($"{indent}\t\tm_{md.name}Ctrl.Dispose();");
				}
				code.AppendLine($"{indent}\t}}");
				code.AppendLine();
				code.AppendLine($"{indent}\t#endregion internals");
				code.AppendLine();
				code.AppendLine($"{indent}}}");
			}
			if (!string.IsNullOrEmpty(src.name_space)) {
				code.AppendLine("}");
			}
			File.WriteAllText(src.path, code.ToString(), Encoding.UTF8);
		}

		private static Type s_type_struct_ctrl = typeof(IRamDataStructCtrl);
		private static Type s_type_data_ctrl = typeof(IRamDataCtrl);
		private static Type s_type_list = typeof(List<>);
		private static Type s_type_ramdata_list = typeof(RamDataList<>);
		private static Type s_type_dict = typeof(Dictionary<,>);
		private static Type s_type_ramdata_dict = typeof(RamDataDict<,>);
		private static Type s_type_custom_base = typeof(RamDataCustomBase<>);
		private static Type s_type_ramdata_base = typeof(RamDataNodeBase);

		private static Dictionary<Type, Type> s_type_map = new Dictionary<Type, Type>() {
			{ typeof(byte), typeof(RamDataByte) },
			{ typeof(sbyte), typeof(RamDataSByte) },
			{ typeof(short), typeof(RamDataShort) },
			{ typeof(ushort), typeof(RamDataUShort) },
			{ typeof(int), typeof(RamDataInt) },
			{ typeof(uint), typeof(RamDataUInt) },
			{ typeof(long), typeof(RamDataLong) },
			{ typeof(ulong), typeof(RamDataULong) },
			{ typeof(float), typeof(RamDataFloat) },
			{ typeof(double), typeof(RamDataDouble) },
			{ typeof(bool), typeof(RamDataBoolean) },
			{ typeof(string), typeof(RamDataString) }
		};

		private static Dictionary<Type, string> s_type_names = new Dictionary<Type, string>() {
			{ typeof(byte), "byte" },
			{ typeof(sbyte), "sbyte" },
			{ typeof(short), "short" },
			{ typeof(ushort), "ushort" },
			{ typeof(int), "int" },
			{ typeof(uint), "uint" },
			{ typeof(long), "long" },
			{ typeof(ulong), "ulong" },
			{ typeof(float), "float" },
			{ typeof(double), "double" },
			{ typeof(bool), "bool" },
			{ typeof(string), "string" }
		};

		private static Type ConvertType(Type type) {
			if (type.IsSubclassOf(s_type_ramdata_base)) { return type; }
			if (type.IsArray) {
				Type et = ConvertType(type.GetElementType());
				if (et == null) { return null; }
				return s_type_ramdata_list.MakeGenericType(new Type[] { et });
			}
			if (type.IsGenericType) {
				Type td = type.GetGenericTypeDefinition();
				if (td == s_type_list) {
					Type et = ConvertType(type.GenericTypeArguments[0]);
					if (et == null) { return null; }
					return s_type_ramdata_list.MakeGenericType(new Type[] { et });
				}
				if (td == s_type_dict) {
					Type[] gts = type.GenericTypeArguments;
					Type et = ConvertType(gts[1]);
					if (et == null) { return null; }
					return s_type_ramdata_dict.MakeGenericType(new Type[] { gts[0], et });
				}
				return null;
			}
			Type t;
			return s_type_map.TryGetValue(type, out t) ? t : null;
		}

		private static string GetTypeName(Type type, HashSet<string> usings) {
			string ret;
			if (s_type_names.TryGetValue(type, out ret)) { return ret; }
			if (usings != null && !string.IsNullOrEmpty(type.Namespace)) { usings.Add(type.Namespace); }
			if (type.IsGenericType) {
				Type[] args = type.GenericTypeArguments;
				int n = args.Length;
				string[] names = new string[n];
				for (int i = 0; i < n; i++) {
					Type t = args[i];
					names[i] = GetTypeName(t, usings);
				}
				return $"{Regex.Match(type.Name, @"\w+").Value}<{string.Join(", ", names)}>";
			}
			return type.Name;
		}

		private class FileData {
			public string path;
			public string name_space;
			public List<ClassData> classes;
		}

		private class ClassData {
			public Type type;
			public List<string> class_modifers;
			public List<MemberData> members;
		}

		private class MemberData {
			public Type type;
			public string name;
		}

		private class TypeCount {
			public Type type;
			public int count;
		}

	}

}
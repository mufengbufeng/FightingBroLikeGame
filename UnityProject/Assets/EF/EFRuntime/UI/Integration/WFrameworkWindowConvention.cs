using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace EF.UI.WFramework
{
    /// <summary>
    /// 默认窗口约定：窗口 id 映射到同名资源地址和可推导的 Logic 类型。
    /// </summary>
    internal static class WFrameworkWindowConvention
    {
        private const string WindowSuffix = "Window";
        private const string LogicSuffix = "Logic";

        private static readonly Dictionary<string, ParametersForUI> s_parameters = new(StringComparer.Ordinal);
        private static Assembly s_logicAssembly;

        /// <summary>
        /// 指定当前热更新程序集。上游示例中的 Loader 与 Logic 同程序集，
        /// EF 运行时 Loader 则需要由热更新入口显式提供该程序集。
        /// </summary>
        internal static void SetLogicAssembly(Assembly logicAssembly)
        {
            if (ReferenceEquals(s_logicAssembly, logicAssembly))
            {
                return;
            }

            s_parameters.Clear();
            s_logicAssembly = logicAssembly;
        }

        /// <summary>
        /// 根据 id 生成窗口参数。PascalCase 窗口使用 FooWindow -> FooLogic，
        /// 下划线 id 兼容上游的 ui_demo_entry -> UIDemoEntry 约定。
        /// </summary>
        internal static ParametersForUI GetParameterForUI(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return default;
            }

            if (s_parameters.TryGetValue(id, out ParametersForUI parameters))
            {
                return parameters;
            }

            Type logicType = FindLogicType(GetLogicTypeName(id));
            if (logicType == null)
            {
                return default;
            }

            parameters = new ParametersForUI
            {
                id = id,
                prefab_path = id,
                logic_type = logicType
            };
            s_parameters.Add(id, parameters);
            return parameters;
        }

        /// <summary>
        /// 在模块关闭时释放对热更新类型的静态引用。
        /// </summary>
        internal static void Clear()
        {
            s_parameters.Clear();
            s_logicAssembly = null;
        }

        private static string GetLogicTypeName(string id)
        {
            if (id.IndexOf('_') >= 0)
            {
                string[] segments = id.Split('_');
                var builder = new System.Text.StringBuilder(id.Length);
                for (int index = 0; index < segments.Length; index++)
                {
                    string segment = segments[index];
                    if (string.IsNullOrEmpty(segment))
                    {
                        continue;
                    }

                    if (string.Equals(segment, "ui", StringComparison.OrdinalIgnoreCase))
                    {
                        builder.Append("UI");
                    }
                    else
                    {
                        builder.Append(char.ToUpperInvariant(segment[0]));
                        if (segment.Length > 1)
                        {
                            builder.Append(segment, 1, segment.Length - 1);
                        }
                    }
                }

                return builder.ToString();
            }

            if (id.EndsWith(WindowSuffix, StringComparison.Ordinal) && id.Length > WindowSuffix.Length)
            {
                return id.Substring(0, id.Length - WindowSuffix.Length) + LogicSuffix;
            }

            return id + LogicSuffix;
        }

        private static Type FindLogicType(string logicTypeName)
        {
            if (s_logicAssembly != null)
            {
                return FindLogicTypeInAssembly(s_logicAssembly, logicTypeName);
            }

            Type result = null;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
            {
                Type type = FindLogicTypeInAssembly(assemblies[assemblyIndex], logicTypeName);
                if (type == null)
                {
                    continue;
                }

                if (result != null && result != type)
                {
                    Debug.LogError($"[WFrameworkUI] 窗口约定找到多个 Logic 类型：{logicTypeName}。");
                    return null;
                }

                result = type;
            }

            return result;
        }

        private static Type FindLogicTypeInAssembly(Assembly assembly, string logicTypeName)
        {
            if (assembly == null)
            {
                return null;
            }

            Type result = null;
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                types = exception.Types;
            }
            catch
            {
                return null;
            }

            for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
            {
                Type type = types[typeIndex];
                if (type == null
                    || !type.IsClass
                    || type.IsAbstract
                    || !string.Equals(type.Name, logicTypeName, StringComparison.Ordinal)
                    || !typeof(IUILogicBase).IsAssignableFrom(type))
                {
                    continue;
                }

                if (result != null && result != type)
                {
                    Debug.LogError($"[WFrameworkUI] 程序集 {assembly.FullName} 中存在多个 Logic 类型：{logicTypeName}。");
                    return null;
                }

                result = type;
            }

            return result;
        }
    }
}

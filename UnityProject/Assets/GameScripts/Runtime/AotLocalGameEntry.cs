using System;
using System.Reflection;
using EF.Debugger;

namespace EF.HotFix
{
    /// <summary>
    /// 在不加载热更新 DLL 的 Player 中启动已编译进包体的本地游戏程序集。
    /// </summary>
    public static class AotLocalGameEntry
    {
        private const string GameAssemblyName = "GameLogic";
        private const string EntryTypeName = "GameLogic.GameLogicEntry";
        private const string EntryMethodName = "Init";

        /// <summary>
        /// 查找已加载的 AOT 游戏程序集并执行其启动入口。
        /// </summary>
        public static void Init()
        {
            Assembly gameAssembly = FindGameAssembly();
            Type entryType = gameAssembly.GetType(EntryTypeName);
            if (entryType == null)
            {
                throw new InvalidOperationException($"AOT 游戏程序集未包含入口类型：{EntryTypeName}");
            }

            MethodInfo initMethod = entryType.GetMethod(EntryMethodName, BindingFlags.Public | BindingFlags.Static);
            if (initMethod == null)
            {
                throw new InvalidOperationException($"AOT 游戏入口未包含静态方法：{EntryTypeName}.{EntryMethodName}");
            }

            initMethod.Invoke(null, null);
            Log.Info("[AotLocalGameEntry] AOT 本地游戏入口初始化完成。");
        }

        /// <summary>
        /// 在当前 AppDomain 中查找被 Player 作为 AOT 程序集编译的游戏逻辑。
        /// </summary>
        private static Assembly FindGameAssembly()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (string.Equals(assembly.GetName().Name, GameAssemblyName, StringComparison.Ordinal))
                {
                    return assembly;
                }
            }

            throw new InvalidOperationException(
                "未找到 AOT 本地游戏程序集 GameLogic。请在构建前关闭“启用热更新”，并同步 HybridCLR 构建模式。");
        }
    }
}

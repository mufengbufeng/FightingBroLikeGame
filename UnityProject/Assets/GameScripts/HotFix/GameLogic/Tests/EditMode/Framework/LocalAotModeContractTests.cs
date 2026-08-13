using System.IO;
using NUnit.Framework;

namespace GameLogic.Tests
{
    /// <summary>
    /// 非热更新模式的本地 AOT 启动源码契约测试。
    /// </summary>
    [TestFixture]
    public sealed class LocalAotModeContractTests
    {
        [Test]
        public void GameEntry_源码契约_关闭热更新时启动Aot本地入口()
        {
            string source = ReadSource("Assets", "GameScripts", "Runtime", "GameEntry.cs");

            StringAssert.Contains("AotLocalGameEntry.Init();", source);
            StringAssert.DoesNotContain("已跳过热更新，本地逻辑未配置", source);
        }

        [Test]
        public void AotLocalGameEntry_源码契约_调用已加载GameLogic入口()
        {
            string source = ReadSource("Assets", "GameScripts", "Runtime", "AotLocalGameEntry.cs");

            StringAssert.Contains("AppDomain.CurrentDomain.GetAssemblies()", source);
            StringAssert.Contains("GameLogic.GameLogicEntry", source);
            StringAssert.Contains("initMethod.Invoke(null, null);", source);
        }

        [Test]
        public void HotFixConfig_源码契约_关闭热更新时不再要求Dll资源()
        {
            string source = ReadSource("Assets", "GameScripts", "Runtime", "HotFixConfig.cs");

            StringAssert.Contains("if (!EnableHotFix)", source);
        }

        [Test]
        public void OfflinePackage_源码契约_关闭热更新时跳过HybridClrDll生成()
        {
            string source = ReadSource("Assets", "EF", "EFEditor", "Editor", "OfflinePackageBuildCommand.cs");

            StringAssert.Contains("if (!hotFixConfig.EnableHotFix)", source);
            StringAssert.Contains("跳过热更新 DLL 生成", source);
        }

        [Test]
        public void Aot模式同步器_源码契约_调整HybridClr热更新程序集列表()
        {
            string source = ReadSource("Assets", "EF", "EFEditor", "Editor", "HotFixBuildModeSynchronizer.cs");

            StringAssert.Contains("HybridCLRSettings.Instance", source);
            StringAssert.Contains("GameLogic.asmdef", source);
            StringAssert.Contains("GameProto.asmdef", source);
        }

        [Test]
        public void Aot模式_源码契约_保留反射入口()
        {
            StringAssert.Contains(
                "[Preserve]",
                ReadSource("Assets", "GameScripts", "HotFix", "GameLogic", "GameLogicEntry.cs"));
        }

        [Test]
        public void GameLogicEntry_源码契约_Aot反射入口保留Init方法()
        {
            string source = ReadSource("Assets", "GameScripts", "HotFix", "GameLogic", "GameLogicEntry.cs");

            StringAssert.Contains("[Preserve]\n        public static void Init()", source);
        }

        /// <summary>
        /// 读取项目内指定源码，并在目标缺失时给出明确的测试失败信息。
        /// </summary>
        private static string ReadSource(params string[] parts)
        {
            var pathParts = new string[parts.Length + 3];
            pathParts[0] = TestContext.CurrentContext.TestDirectory;
            pathParts[1] = "..";
            pathParts[2] = "..";
            for (int index = 0; index < parts.Length; index++)
            {
                pathParts[index + 3] = parts[index];
            }

            string path = Path.Combine(pathParts);
            string fullPath = Path.GetFullPath(path);
            Assert.That(File.Exists(fullPath), Is.True, $"缺少本地 AOT 模式源码：{fullPath}");
            return File.ReadAllText(fullPath).Replace("\r\n", "\n");
        }
    }
}

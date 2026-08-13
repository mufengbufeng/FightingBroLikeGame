using System.IO;
using NUnit.Framework;

namespace GameLogic.Tests
{
    /// <summary>
    /// Runtime 启动入口的源码契约测试，约束热更入口读取前注册 W-Framework UI 管理器。
    /// </summary>
    [TestFixture]
    public sealed class GameEntryUiRegistrationContractTests
    {
        [Test]
        public void Awake_源码契约_仅注册WFrameworkUI管理器()
        {
            string source = ReadGameEntrySource();

            StringAssert.Contains(
                "ModuleSystem.Register<WFrameworkUI.IWFrameworkUIManager>(new WFrameworkUI.WFrameworkUIManager(_resourceManager))",
                source);
            StringAssert.DoesNotContain("ModuleSystem.Register<IUIManager>", source);
            StringAssert.DoesNotContain("new UIManager(", source);
        }

        [Test]
        public void Init_源码契约_异步加载热更Dll()
        {
            string source = ReadGameEntrySource();

            StringAssert.Contains("await Resource.Load<TextAsset>", source);
            StringAssert.DoesNotContain("LoadAssetAsync<TextAsset>", source);
            StringAssert.DoesNotContain("LoadAssetSync<TextAsset>", source);
        }

        [Test]
        public void Init_源码契约_仅Player加载Aot元数据()
        {
            string source = ReadGameEntrySource();

            StringAssert.Contains("#if !UNITY_EDITOR\n        await LoadAotMetadataAssembliesAsync();\n#endif", source);
        }

        [Test]
        public void HotFixConfig_源码契约_提供默认开启的热更新开关()
        {
            string source = ReadHotFixConfigSource();

            StringAssert.Contains("private bool _enableHotFix = true;", source);
            StringAssert.Contains("public bool EnableHotFix => _enableHotFix;", source);
        }

        [Test]
        public void Init_源码契约_关闭热更新时跳过程序集加载和热更入口()
        {
            string source = ReadGameEntrySource();

            int guardIndex = source.IndexOf("if (!_hotFixConfig.EnableHotFix)");
            Assert.That(guardIndex, Is.GreaterThanOrEqualTo(0));

            int returnIndex = source.IndexOf("return;", guardIndex);
            int aotLoadIndex = source.IndexOf("await LoadAotMetadataAssembliesAsync();");
            int hotFixLoadIndex = source.IndexOf("await LoadHotUpdateAssembliesAsync();");
            int entryIndex = source.IndexOf("InvokeHotfixEntry();");

            Assert.That(returnIndex, Is.GreaterThan(guardIndex));
            Assert.That(aotLoadIndex, Is.GreaterThan(returnIndex));
            Assert.That(hotFixLoadIndex, Is.GreaterThan(returnIndex));
            Assert.That(entryIndex, Is.GreaterThan(returnIndex));
        }

        [Test]
        public void OnDestroy_源码契约_关闭所有模块()
        {
            string source = ReadGameEntrySource();

            StringAssert.Contains("private void OnDestroy()", source);
            StringAssert.Contains("ModuleSystem.ShutdownAll();", source);
        }

        private static string GetGameEntryPath()
        {
            string path = Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "..",
                "..",
                "Assets",
                "GameScripts",
                "Runtime",
                "GameEntry.cs");
            return Path.GetFullPath(path);
        }

        /// <summary>
        /// 获取热更新配置源码的绝对路径。
        /// </summary>
        private static string GetHotFixConfigPath()
        {
            string path = Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "..",
                "..",
                "Assets",
                "GameScripts",
                "Runtime",
                "HotFixConfig.cs");
            return Path.GetFullPath(path);
        }

        /// <summary>
        /// 统一源码换行符，避免 Git 自动换行策略影响字符串契约断言。
        /// </summary>
        private static string ReadGameEntrySource()
        {
            return File.ReadAllText(GetGameEntryPath()).Replace("\r\n", "\n");
        }

        /// <summary>
        /// 读取热更新配置源码并统一换行符，保证跨平台契约断言稳定。
        /// </summary>
        private static string ReadHotFixConfigSource()
        {
            return File.ReadAllText(GetHotFixConfigPath()).Replace("\r\n", "\n");
        }
    }
}

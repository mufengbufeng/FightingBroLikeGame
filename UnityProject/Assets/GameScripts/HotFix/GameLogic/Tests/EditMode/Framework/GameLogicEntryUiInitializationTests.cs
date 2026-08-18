using System.IO;
using NUnit.Framework;

namespace GameLogic.Tests
{
    /// <summary>
    /// 验证热更入口只初始化 W-Framework UI 根节点。
    /// </summary>
    [TestFixture]
    public sealed class GameLogicEntryUiInitializationTests
    {
        [Test]
        public void InitializeUI_源码契约_只初始化WFramework根节点()
        {
            string source = File.ReadAllText(GetGameLogicEntryPath());

            StringAssert.Contains("WFramework.IWFrameworkUIManager", source);
            StringAssert.Contains("WFramework.UIManager.Root", source);
            StringAssert.Contains("InitializeWFrameworkUI()", source);
            StringAssert.Contains("logicAssembly: typeof(GameLogicEntry).Assembly", source);
            StringAssert.DoesNotContain("IUIManager", source);
            StringAssert.DoesNotContain("UILayer", source);
            StringAssert.DoesNotContain("RegisterLayerRoot", source);
            StringAssert.DoesNotContain("ReferenceCollector", source);
            StringAssert.DoesNotContain("WFrameworkUIRootBindings", source);
            StringAssert.DoesNotContain("FindFirstObjectByType", source);
            StringAssert.DoesNotContain("EnsureCanvasRoot", source);
            StringAssert.DoesNotContain("AddComponent<Canvas>", source);
        }

        /// <summary>
        /// Entry 场景必须在原有 UIRoot 下序列化独立的 W-Framework Canvas 根节点。
        /// </summary>
        [Test]
        public void EntryScene_序列化WFramework独立Canvas根节点()
        {
            string scene = File.ReadAllText(GetEntryScenePath());

            StringAssert.Contains("m_Name: WFrameworkUI", scene);
            StringAssert.Contains("EF.Runtime::EF.UI.WFramework.UIRoot", scene);
            StringAssert.Contains("m_RootCanvas: {fileID:", scene);
            StringAssert.Contains("m_ParentForUI: {fileID:", scene);
            StringAssert.Contains("m_LayerForHide: 2", scene);
            StringAssert.Contains("m_StandaloneUpdate: 0", scene);
            StringAssert.Contains("m_OverrideSorting: 1", scene);
            StringAssert.DoesNotContain("WFrameworkUIRootBindings", scene);
        }

        /// <summary>
        /// MainWindow 必须由独立流程通过 W-Framework UI 管理器打开和关闭。
        /// </summary>
        [Test]
        public void MainWindowProcedure_源码契约_通过WFramework管理器维护窗口()
        {
            string mainWindowProcedure = File.ReadAllText(GetMainWindowProcedurePath());
            string entry = File.ReadAllText(GetGameLogicEntryPath());

            StringAssert.Contains("new MainWindowProcedure()", entry);
            StringAssert.Contains("new GamePlayProcedure()", entry);
            StringAssert.Contains("IWFrameworkUIManager uiManager = GameLogicEntry.WFrameworkUI;", mainWindowProcedure);
            StringAssert.Contains("uiManager.Open(MainWindowId)", mainWindowProcedure);
            StringAssert.Contains("uiManager.CloseGroup(MainWindowId)", mainWindowProcedure);
        }

        private static string GetMainWindowProcedurePath()
        {
            return Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "..",
                "..",
                "Assets",
                "GameScripts",
                "HotFix",
                "GameLogic",
                "Procedure",
                "MainWindowProcedure.cs");
        }

        private static string GetGameLogicEntryPath()
        {
            return Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "..",
                "..",
                "Assets",
                "GameScripts",
                "HotFix",
                "GameLogic",
                "GameLogicEntry.cs");
        }

        private static string GetEntryScenePath()
        {
            return Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "..",
                "..",
                "Assets",
                "Scenes",
                "Entry.unity");
        }
    }
}

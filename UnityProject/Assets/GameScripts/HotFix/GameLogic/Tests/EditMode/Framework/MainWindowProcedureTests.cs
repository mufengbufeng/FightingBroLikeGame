using System.Reflection;
using EF.UI.WFramework;
using NUnit.Framework;

namespace GameLogic.Tests
{
    /// <summary>
    /// 验证初始化流程与 MainWindowProcedure 的窗口生命周期协作。
    /// </summary>
    [TestFixture]
    public sealed class MainWindowProcedureTests
    {
        [TearDown]
        public void TearDown()
        {
            GameLogicEntry.SetWFrameworkUIManagerForTests(null);
        }

        /// <summary>
        /// 初始化流程应切换到 MainWindowProcedure，并由该流程提交 MainWindow 打开请求。
        /// </summary>
        [Test]
        public void InitProcedure_进入后切换MainWindowProcedure并打开窗口()
        {
            var uiManager = new RecordingUiManager { IsInitialized = true };
            GameLogicEntry.SetWFrameworkUIManagerForTests(uiManager);

            var fsmManager = new EF.Fsm.FsmManager();
            var procedureManager = new EF.Procedure.ProcedureManager();
            try
            {
                procedureManager.Initialize(fsmManager, new InitProcedure(), new MainWindowProcedure());
                procedureManager.StartProcedure<InitProcedure>();

                Assert.That(procedureManager.CurrentProcedure, Is.TypeOf<MainWindowProcedure>());
                Assert.That(uiManager.OpenedId, Is.EqualTo("MainWindow"));
                Assert.That(uiManager.OpenCount, Is.EqualTo(1));
            }
            finally
            {
                procedureManager.Shutdown();
                fsmManager.Shutdown();
            }
        }

        /// <summary>
        /// W-Framework 未初始化时，主窗口流程不得提交无效的打开请求。
        /// </summary>
        [Test]
        public void MainWindowProcedure_未初始化时不打开窗口()
        {
            var uiManager = new RecordingUiManager { IsInitialized = false };
            GameLogicEntry.SetWFrameworkUIManagerForTests(uiManager);

            var fsmManager = new EF.Fsm.FsmManager();
            var procedureManager = new EF.Procedure.ProcedureManager();
            try
            {
                procedureManager.Initialize(fsmManager, new MainWindowProcedure());
                procedureManager.StartProcedure<MainWindowProcedure>();

                Assert.That(uiManager.OpenCount, Is.EqualTo(0));
            }
            finally
            {
                procedureManager.Shutdown();
                fsmManager.Shutdown();
            }
        }

        /// <summary>
        /// 主窗口流程正常离开时，应关闭 MainWindow 所属分组。
        /// </summary>
        [Test]
        public void MainWindowProcedure_正常离开时关闭窗口分组()
        {
            var uiManager = new RecordingUiManager { IsInitialized = true };
            GameLogicEntry.SetWFrameworkUIManagerForTests(uiManager);

            var fsmManager = new EF.Fsm.FsmManager();
            var procedureManager = new EF.Procedure.ProcedureManager();
            try
            {
                procedureManager.Initialize(fsmManager, new MainWindowProcedure(), new ExitProcedure());
                procedureManager.StartProcedure<MainWindowProcedure>();
                fsmManager.GetFsm<EF.Procedure.IProcedureManager>("Procedure").ChangeState<ExitProcedure>();

                Assert.That(uiManager.ClosedGroupId, Is.EqualTo("MainWindow"));
                Assert.That(uiManager.CloseGroupCount, Is.EqualTo(1));
            }
            finally
            {
                procedureManager.Shutdown();
                fsmManager.Shutdown();
            }
        }

        /// <summary>
        /// 用于触发主窗口流程正常离开的空流程。
        /// </summary>
        private sealed class ExitProcedure : EF.Procedure.ProcedureBase
        {
        }

        /// <summary>
        /// 记录 W-Framework UI 管理器调用，隔离流程测试与真实资源加载。
        /// </summary>
        private sealed class RecordingUiManager : IWFrameworkUIManager
        {
            public bool IsInitialized { get; set; }

            public string OpenedId { get; private set; }

            public int OpenCount { get; private set; }

            public string ClosedGroupId { get; private set; }

            public int CloseGroupCount { get; private set; }

            public void Initialize(
                bool useLogicCache = true,
                IUILoadingOverlay loadingOverlay = null,
                Assembly logicAssembly = null)
            {
                IsInitialized = true;
            }

            public void SetLoadingOverlay(IUILoadingOverlay loadingOverlay)
            {
            }

            public bool Open(string id, object parameter = null)
            {
                OpenedId = id;
                OpenCount++;
                return IsInitialized;
            }

            public bool Open(string id, object parameter, IUIEventHandler eventHandler)
            {
                return Open(id, parameter);
            }

            public bool CloseSingle(string id)
            {
                return false;
            }

            public bool CloseGroup(string id)
            {
                ClosedGroupId = id;
                CloseGroupCount++;
                return IsInitialized;
            }

            public int CloseAll()
            {
                return 0;
            }

            public bool ProcessEscape()
            {
                return false;
            }

            public void Update(float elapseSeconds, float realElapseSeconds)
            {
            }

            public void Shutdown()
            {
                IsInitialized = false;
            }
        }
    }
}

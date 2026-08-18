using EF.Debugger;
using EF.Procedure;
using EF.UI.WFramework;
using ProcedureOwner = EF.Fsm.IFsm<EF.Procedure.IProcedureManager>;

namespace GameLogic
{
    /// <summary>
    /// 主窗口流程，负责在初始化完成后打开并维护 MainWindow。
    /// </summary>
    public sealed class MainWindowProcedure : ProcedureBase
    {
        private const string MainWindowId = "MainWindow";

        /// <summary>
        /// 初始化主窗口流程。
        /// </summary>
        protected override void OnInit(ProcedureOwner procedureOwner)
        {
            base.OnInit(procedureOwner);
            Log.Info("[MainWindowProcedure] OnInit");
        }

        /// <summary>
        /// 进入主窗口流程并通过 W-Framework 打开 MainWindow。
        /// </summary>
        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);

            IWFrameworkUIManager uiManager = GameLogicEntry.WFrameworkUI;
            if (uiManager == null)
            {
                Log.Error("[MainWindowProcedure] W-Framework UI 管理器为空，无法打开 MainWindow。");
                return;
            }

            if (!uiManager.IsInitialized)
            {
                Log.Error("[MainWindowProcedure] W-Framework UI 尚未初始化，无法打开 MainWindow。");
                return;
            }

            if (!uiManager.Open(MainWindowId))
            {
                Log.Error("[MainWindowProcedure] MainWindow 打开请求失败。");
                return;
            }

            Log.Info("[MainWindowProcedure] MainWindow 打开请求已提交。");
        }

        /// <summary>
        /// 正常离开主窗口流程时关闭 MainWindow 所属分组。
        /// </summary>
        protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
            base.OnLeave(procedureOwner, isShutdown);

            if (!isShutdown)
            {
                IWFrameworkUIManager uiManager = GameLogicEntry.WFrameworkUI;
                if (uiManager != null && uiManager.IsInitialized)
                {
                    uiManager.CloseGroup(MainWindowId);
                }
            }

            Log.Info($"[MainWindowProcedure] OnLeave，isShutdown={isShutdown}。");
        }

        /// <summary>
        /// 销毁主窗口流程。
        /// </summary>
        protected override void OnDestroy(ProcedureOwner procedureOwner)
        {
            base.OnDestroy(procedureOwner);
            Log.Info("[MainWindowProcedure] OnDestroy");
        }
    }
}

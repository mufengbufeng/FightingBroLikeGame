using EF.Debugger;
using EF.UI.WFramework;
using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// 主菜单窗口逻辑。
    /// </summary>
    public sealed class MainLogic : UIStackLogicBase
    {
        private MainWindow mUI;

        /// <summary>
        /// 主菜单占满游戏窗口。
        /// </summary>
        protected override bool IsFullScreen => true;

        /// <summary>
        /// 主菜单作为独立窗口分组打开。
        /// </summary>
        protected override bool NewGroup => true;

        /// <summary>
        /// 主菜单只负责展示结构，不播放进入动画。
        /// </summary>
        protected override string OpenAnim => null;

        /// <summary>
        /// 主菜单只负责展示结构，不播放退出动画。
        /// </summary>
        protected override string CloseAnim => null;

        /// <summary>
        /// 获取窗口绑定并注册主菜单按钮事件。
        /// </summary>
        protected override void OnOpen(GameObject go, int baseSortingOrder)
        {
            base.OnOpen(go, baseSortingOrder);
            mUI = go.GetComponent<MainWindow>();
            if (mUI == null)
            {
                Log.Error("[MainLogic] MainWindow 绑定组件缺失。", go);
                return;
            }

            mUI.Open();
            mUI.StartExpeditionButton?.button?.onClick.AddListener(OnStartExpeditionClicked);
            mUI.ExplorerButton?.button?.onClick.AddListener(OnExplorerClicked);
            mUI.RelicLibraryButton?.button?.onClick.AddListener(OnRelicLibraryClicked);
            mUI.SettingsButton?.button?.onClick.AddListener(OnSettingsClicked);
        }

        /// <summary>
        /// 清理窗口绑定组件及其按钮监听。
        /// </summary>
        protected override void OnClose()
        {
            mUI?.Clear();
            mUI = null;
            base.OnClose();
        }

        /// <summary>
        /// 处理开始远征按钮点击。
        /// </summary>
        private void OnStartExpeditionClicked()
        {
            Log.Info("[MainLogic] 点击：开始远征。");
        }

        /// <summary>
        /// 处理探索者按钮点击。
        /// </summary>
        private void OnExplorerClicked()
        {
            Log.Info("[MainLogic] 点击：探索者。");
        }

        /// <summary>
        /// 处理遗物库按钮点击。
        /// </summary>
        private void OnRelicLibraryClicked()
        {
            Log.Info("[MainLogic] 点击：遗物库。");
        }

        /// <summary>
        /// 处理设置按钮点击。
        /// </summary>
        private void OnSettingsClicked()
        {
            Log.Info("[MainLogic] 点击：设置。");
        }
    }
}

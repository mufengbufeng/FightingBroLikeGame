using System.Reflection;
using EF.Common;

namespace EF.UI.WFramework
{
    /// <summary>
    /// W-Framework UI 在 EF 模块系统中的访问入口。
    /// </summary>
    public interface IWFrameworkUIManager : IEFManager
    {
        /// <summary>
        /// W-Framework UI 是否已绑定到场景 Canvas。
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// 使用场景中已经注册的 <see cref="UIRoot"/> 初始化 W-Framework UI。
        /// 场景根由 <see cref="UIRoot.Awake"/> 注册，适配层不会创建或重配 Canvas。
        /// <paramref name="logicAssembly"/> 指定当前热更新程序集，以便按约定解析窗口 Logic。
        /// </summary>
        void Initialize(
            bool useLogicCache = true,
            IUILoadingOverlay loadingOverlay = null,
            Assembly logicAssembly = null);

        /// <summary>
        /// 设置异步准备和动画关闭期间使用的加载遮罩。
        /// </summary>
        void SetLoadingOverlay(IUILoadingOverlay loadingOverlay);

        /// <summary>
        /// 打开指定窗口。
        /// </summary>
        bool Open(string id, object parameter = null);

        /// <summary>
        /// 打开指定窗口并接收生命周期回调。
        /// </summary>
        bool Open(string id, object parameter, IUIEventHandler eventHandler);

        /// <summary>
        /// 关闭匹配窗口的一个实例。
        /// </summary>
        bool CloseSingle(string id);

        /// <summary>
        /// 关闭匹配窗口所属的堆叠分组。
        /// </summary>
        bool CloseGroup(string id);

        /// <summary>
        /// 关闭全部堆叠和固定窗口。
        /// </summary>
        int CloseAll();

        /// <summary>
        /// 将 Escape 输入分发给当前焦点窗口。
        /// </summary>
        bool ProcessEscape();
    }
}

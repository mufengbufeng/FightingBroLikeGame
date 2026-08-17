using EF.Bootstrap;
using EF.Commercial;
using EF.Common;
using EF.Debugger;
using EF.Entity;
using EF.Fsm;
using EF.Model;
using EF.ObjectPool;
using EF.Procedure;
using EF.Resource;
using EF.Save;
using EF.Sound;
using EF.Timer;
using UnityEngine.Scripting;
using WFramework = EF.UI.WFramework;

namespace GameLogic
{
    /// <summary>
    /// 热更新游戏逻辑入口。
    /// </summary>
    [Preserve]
    public static class GameLogicEntry
    {
        private static IResourceManager _resourceManager;
        private static EventHub _eventHub;
        private static WFramework.IWFrameworkUIManager _wFrameworkUIManager;
        private static ISoundManager _soundManager;
        private static ITimerManager _timerManager;
        private static IObjectPoolManager _objectPoolManager;
        private static IFsmManager _fsmManager;
        private static IProcedureManager _procedureManager;
        private static ISaveManager _saveManager;
        private static ICommercialService _commercialService;
        private static ModelManager _modelManager;
        private static IEntityManager _entityManager;

        /// <summary>
        /// 资源管理器。
        /// </summary>
        public static IResourceManager Resource => _resourceManager;

        /// <summary>
        /// 事件系统枢纽。
        /// </summary>
        public static EventHub Event => _eventHub;

        /// <summary>
        /// 唯一的运行时 UI 管理器。
        /// </summary>
        public static WFramework.IWFrameworkUIManager WFrameworkUI => _wFrameworkUIManager;

        /// <summary>
        /// 音频管理器。
        /// </summary>
        public static ISoundManager Sound => _soundManager;

        /// <summary>
        /// 计时器管理器。
        /// </summary>
        public static ITimerManager Timer => _timerManager;

        /// <summary>
        /// 对象池管理器。
        /// </summary>
        public static IObjectPoolManager ObjectPool => _objectPoolManager;

        /// <summary>
        /// 状态机管理器。
        /// </summary>
        public static IFsmManager Fsm => _fsmManager;

        /// <summary>
        /// 流程管理器。
        /// </summary>
        public static IProcedureManager Procedure => _procedureManager;

        /// <summary>
        /// 本地保存管理器。
        /// </summary>
        public static ISaveManager Save => _saveManager;

        /// <summary>
        /// 平台无关的商业化服务。
        /// </summary>
        public static ICommercialService Commercial => _commercialService;

        /// <summary>
        /// 模型管理器。
        /// </summary>
        public static ModelManager Model => _modelManager;

        /// <summary>
        /// 实体管理器。
        /// </summary>
        public static IEntityManager Entity => _entityManager;

        /// <summary>
        /// 热更新代码入口点。
        /// </summary>
        [Preserve]
        public static void Init()
        {
            Log.Info("[GameLogicEntry] 开始初始化热更新逻辑...");

            _resourceManager = ModuleSystem.Get<IResourceManager>();
            _eventHub = new EventHub();
            ModuleSystem.Register(_eventHub, replace: true);
            _soundManager = ModuleSystem.Get<ISoundManager>();
            _timerManager = ModuleSystem.Get<ITimerManager>();
            _objectPoolManager = ModuleSystem.Get<IObjectPoolManager>();
            _fsmManager = ModuleSystem.Get<IFsmManager>();
            _procedureManager = ModuleSystem.Get<IProcedureManager>();
            _saveManager = ModuleSystem.Get<ISaveManager>();
            _commercialService = ModuleSystem.Get<ICommercialService>();
            _entityManager = ModuleSystem.Get<IEntityManager>();
            _modelManager = ModuleSystem.Get<ModelManager>();
            _wFrameworkUIManager = ModuleSystem.Get<WFramework.IWFrameworkUIManager>();

            InitializeUI();
            InitializeProcedures();
            BootstrapLoadingService.Hide();

            Log.Info("[GameLogicEntry] 框架热更新骨架初始化完成。");
        }

        /// <summary>
        /// 初始化由场景 UIRoot 注册的 W-Framework UI。
        /// </summary>
        internal static void InitializeUI()
        {
            if (_wFrameworkUIManager == null)
            {
                Log.Error("[GameLogicEntry] 未获取到 W-Framework UI 管理器。");
                return;
            }

            if (WFramework.UIManager.Root == null)
            {
                Log.Error("[GameLogicEntry] 场景中未注册 W-Framework UIRoot，无法初始化 W-Framework UI。");
                return;
            }

            try
            {
                InitializeWFrameworkUI();
            }
            catch (System.Exception exception)
            {
                Log.Error($"[GameLogicEntry] W-Framework UI 初始化失败：{exception}");
            }
        }

        /// <summary>
        /// 使用场景已注册的原生 UIRoot 初始化唯一的 W-Framework UI 管理器。
        /// </summary>
        private static void InitializeWFrameworkUI()
        {
            if (_wFrameworkUIManager.IsInitialized)
            {
                return;
            }

            _wFrameworkUIManager.Initialize(logicAssembly: typeof(GameLogicEntry).Assembly);
            Log.Info("[GameLogicEntry] W-Framework UI 管理器初始化完成。");
        }

        /// <summary>
        /// 测试专用：注入 W-Framework UI 管理器。
        /// </summary>
        internal static void SetWFrameworkUIManagerForTests(WFramework.IWFrameworkUIManager uiManager)
        {
            _wFrameworkUIManager = uiManager;
        }

        /// <summary>
        /// 测试专用：注入事件总线。
        /// </summary>
        internal static void SetEventHubForTests(EventHub eventHub)
        {
            _eventHub = eventHub;
        }

        /// <summary>
        /// 测试专用：触发 W-Framework 场景根初始化。
        /// </summary>
        internal static void InitializeUIForTests()
        {
            InitializeUI();
        }

        /// <summary>
        /// 初始化流程管理器。
        /// </summary>
        private static void InitializeProcedures()
        {
            Log.Info("[GameLogicEntry] 初始化流程管理器...");

            try
            {
                _procedureManager.Initialize(
                    _fsmManager,
                    new InitProcedure(),
                    new MainWindowProcedure());
                _procedureManager.StartProcedure<InitProcedure>();
                Log.Info("[GameLogicEntry] 流程管理器启动完成。");
            }
            catch (System.Exception exception)
            {
                Log.Error($"[GameLogicEntry] 流程管理器初始化失败：{exception.Message}");
            }
        }
    }
}

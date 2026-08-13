using System;
using System.Reflection;
using EF.Resource;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EF.UI.WFramework
{
    /// <summary>
    /// 将 W-Framework 静态 UI 栈接入 EF 生命周期、资源系统和输入系统。
    /// </summary>
    public sealed class WFrameworkUIManager : IWFrameworkUIManager
    {
        private readonly IResourceManager _resourceManager;

        private WFrameworkResourceLoader _loader;
        private Assembly _logicAssembly;

        /// <summary>
        /// 创建 W-Framework UI 管理器。
        /// </summary>
        public WFrameworkUIManager(IResourceManager resourceManager)
        {
            _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
        }

        /// <summary>
        /// W-Framework UI 是否已经完成初始化。
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// 使用场景已经注册的原生 <see cref="UIRoot"/> 初始化上游静态 UI 管理器。
        /// </summary>
        public void Initialize(
            bool useLogicCache = true,
            IUILoadingOverlay loadingOverlay = null,
            Assembly logicAssembly = null)
        {
            if (IsInitialized)
            {
                if (!ReferenceEquals(_logicAssembly, logicAssembly))
                {
                    throw new InvalidOperationException("W-Framework UI 已初始化，不能在场景未重载时替换逻辑程序集。");
                }

                return;
            }

            if (UIManager.IsInitialized)
            {
                throw new InvalidOperationException("W-Framework UI 已由其它管理器初始化。");
            }

            UIRoot root = UIManager.Root;
            if (root == null)
            {
                throw new InvalidOperationException("场景中没有已注册的 W-Framework UIRoot。请在 UIRoot/WFrameworkUI 上序列化 UIRoot 组件。");
            }

            root.ValidateSceneConfiguration();
            _logicAssembly = logicAssembly;
            WFrameworkWindowConvention.SetLogicAssembly(_logicAssembly);

            bool startedCoreInitialization = false;
            bool initializedContentBinding = false;

            try
            {
                _loader = new WFrameworkResourceLoader(_resourceManager);
                startedCoreInitialization = true;
                UIManager.Init(_loader, loadingOverlay, useLogicCache);
                UIContentBind.Init(_loader);
                initializedContentBinding = true;
                IsInitialized = UIManager.IsInitialized;

                if (!IsInitialized)
                {
                    throw new InvalidOperationException("W-Framework UI 根节点初始化失败。");
                }
            }
            catch
            {
                if (startedCoreInitialization)
                {
                    UIManager.Shutdown();
                }

                if (initializedContentBinding)
                {
                    UIContentBind.Shutdown();
                }

                _loader?.Dispose();
                _loader = null;
                _logicAssembly = null;
                WFrameworkWindowConvention.Clear();
                throw;
            }
        }

        /// <summary>
        /// 在热更新逻辑准备好后设置或替换加载遮罩。
        /// </summary>
        public void SetLoadingOverlay(IUILoadingOverlay loadingOverlay)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("W-Framework UI 尚未初始化。");
            }

            UIManager.SetLoadingOverlay(loadingOverlay);
        }

        /// <summary>
        /// 通过上游栈式生命周期打开窗口。
        /// </summary>
        public bool Open(string id, object parameter = null)
        {
            return IsInitialized && UIManager.Open(id, parameter);
        }

        /// <summary>
        /// 通过上游栈式生命周期打开窗口并绑定回调。
        /// </summary>
        public bool Open(string id, object parameter, IUIEventHandler eventHandler)
        {
            return IsInitialized && UIManager.OpenWithHandler(eventHandler, id, parameter);
        }

        /// <summary>
        /// 关闭一个匹配窗口实例。
        /// </summary>
        public bool CloseSingle(string id)
        {
            return IsInitialized && UIManager.CloseSingle(id);
        }

        /// <summary>
        /// 关闭匹配窗口所属分组。
        /// </summary>
        public bool CloseGroup(string id)
        {
            return IsInitialized && UIManager.CloseGroup(id);
        }

        /// <summary>
        /// 关闭全部窗口。
        /// </summary>
        public int CloseAll()
        {
            return IsInitialized ? UIManager.CloseAll() : 0;
        }

        /// <summary>
        /// 将 Escape 事件转发给当前焦点窗口。
        /// </summary>
        public bool ProcessEscape()
        {
            return IsInitialized && UIManager.ProcessEscape();
        }

        /// <summary>
        /// 由 EF 模块系统驱动上游 UI 的每帧逻辑。
        /// </summary>
        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            if (!IsInitialized)
            {
                return;
            }

#if ENABLE_INPUT_SYSTEM
            bool escapePressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            UIManager.Update(elapseSeconds, realElapseSeconds, escapePressed);
#else
            UIManager.Update(elapseSeconds, realElapseSeconds);
#endif
        }

        /// <summary>
        /// 释放上游静态状态和动态绑定资源；场景 UI 根节点由场景自身持有。
        /// </summary>
        public void Shutdown()
        {
            if (IsInitialized)
            {
                UIManager.Shutdown();
                UIContentBind.Shutdown();
            }

            _loader?.Dispose();
            _loader = null;
            _logicAssembly = null;
            WFrameworkWindowConvention.Clear();
            IsInitialized = false;
        }
    }
}

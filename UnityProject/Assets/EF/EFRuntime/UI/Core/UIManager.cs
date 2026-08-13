using System;
using System.Collections.Generic;
using UnityEngine;

namespace EF.UI.WFramework {

	public static partial class UIManager {

		public static UIRoot Root { get; private set; }

		/// <summary>
		/// 当前静态 UI 管理器是否已完成初始化。
		/// </summary>
		public static bool IsInitialized { get { return s_processor != null; } }

		public static void Init(IUILoader uiLoader, IUILoadingOverlay loadingOverlay, bool useLogicCache) {
			if (uiLoader == null) { throw new ArgumentNullException(nameof(uiLoader)); }
			if (s_uiloader != null) { throw new InvalidOperationException(); }
			s_uiloader = uiLoader;
			s_loading_overlay = loadingOverlay;
			if (useLogicCache) {
				s_logic_cache = new Dictionary<Type, Queue<IUILogicBase>>();
				s_logic_to_type = new Dictionary<object, Type>();
			}
			TryInit();
		}

		public static UIManagerExtend ex { get; private set; } = new UIManagerExtend();

		/// <summary>
		/// 设置异步准备和动画关闭期间使用的加载遮罩。
		/// </summary>
		public static void SetLoadingOverlay(IUILoadingOverlay loadingOverlay) {
			s_loading_overlay = loadingOverlay;
		}

		/// <summary>
		/// 通知可选的加载遮罩开始展示。
		/// </summary>
		internal static void BeginLoading(string key) {
			if (s_loading_overlay != null) {
				s_loading_overlay.BeginLoading(key);
			}
		}

		/// <summary>
		/// 通知可选的加载遮罩结束展示。
		/// </summary>
		internal static void EndLoading(string key) {
			if (s_loading_overlay != null) {
				s_loading_overlay.EndLoading(key);
			}
		}

		public static bool Open(string id) {
			return OpenInternal(id, null, null);
		}

		public static bool Open(string id, object parameter) {
			return OpenInternal(id, parameter, null);
		}

		public static bool OpenWithHandler(IUIEventHandler handler, string id) {
			return OpenInternal(id, null, handler);
		}

		public static bool OpenWithHandler(IUIEventHandler handler, string id, object parameter) {
			return OpenInternal(id, parameter, handler);
		}

		public static bool Open(ParametersForUI cfg) {
			return OpenInternal(cfg, null, null);
		}

		public static bool Open(ParametersForUI cfg, object parameter) {
			return OpenInternal(cfg, parameter, null);
		}

		public static bool OpenWithHandler(IUIEventHandler handler, ParametersForUI cfg) {
			return OpenInternal(cfg, null, handler);
		}

		public static bool OpenWithHandler(IUIEventHandler handler, ParametersForUI cfg, object parameter) {
			return OpenInternal(cfg, parameter, handler);
		}

		public static bool CloseSingle(string id) {
			if (s_processor == null) { return false; }
			return s_processor.CloseSingle(id);
		}

		public static bool CloseGroup(string id) {
			if (s_processor == null) { return false; }
			return s_processor.CloseGroup(id);
		}

		public static bool CloseSingle(IUILogicBase logic) {
			if (logic == null) { return false; }
			if (s_processor == null) { return false; }
			return s_processor.CloseSingle(logic);
		}

		public static bool CloseGroup(IUILogicBase logic) {
			if (logic == null) { return false; }
			if (s_processor == null) { return false; }
			return s_processor.CloseGroup(logic);
		}

		/// <summary>
		/// 关闭全部堆叠和固定界面。
		/// </summary>
		public static int CloseAll() {
			if (s_processor == null) { return 0; }
			return s_processor.CloseAll();
		}

		public static void SetUIRoot(UIRoot root) {
			if (ReferenceEquals(Root, root)) {
				return;
			}
			if (Root != null) {
				throw new InvalidOperationException();
			}
			if (root == null) {
				throw new ArgumentNullException(nameof(root));
			}
			Root = root;
			TryInit();
		}

		/// <summary>
		/// 关闭当前静态实例，供 EF 模块生命周期调用。
		/// </summary>
		public static void Shutdown() {
			try {
				if (s_processor != null) {
					s_processor.Shutdown();
				}
			} finally {
				ClearRuntimeCachesAndReferences();
				s_processor = null;
				s_uiloader = null;
				s_loading_overlay = null;
				s_logic_cache = null;
				s_logic_to_type = null;
				Root = null;
				ex = new UIManagerExtend();
			}
		}

		private static Processor s_processor;

		private static void TryInit() {
			if (Root == null || s_uiloader == null) { return; }
			if (s_processor != null) { return; }
			InitCameraRange();
			s_processor = new Processor();
		}

		private static bool OpenInternal(string id, object parameter, IUIEventHandler handler) {
			if (s_processor == null) { return false; }
			ParametersForUI cfg = s_uiloader.GetParameterForUI(id);
			if (cfg.id != id) { return false; }
			return s_processor.Open(cfg, parameter, handler);
		}

		private static bool OpenInternal(ParametersForUI cfg, object parameter, IUIEventHandler handler) {
			if (s_processor == null) { return false; }
			if (string.IsNullOrEmpty(cfg.id)) { return false; }
			return s_processor.Open(cfg, parameter, handler);
		}

		#region update dispatch

		public static void Update() {
			Update(Time.deltaTime, Time.unscaledDeltaTime, false);
		}

		/// <summary>
		/// 更新 UI 状态，并由外部输入系统传入 Escape 触发信息。
		/// </summary>
		public static void Update(bool escapePressed) {
			Update(Time.deltaTime, Time.unscaledDeltaTime, escapePressed);
		}

		/// <summary>
		/// 更新 UI 根状态并分发 Escape 输入。
		/// </summary>
		public static void Update(float elapseSeconds, float realElapseSeconds, bool escapePressed = false) {
			if (s_processor == null) { return; }
			CheckScreenOrCameraChanged();
			if (escapePressed) {
				ProcessEscape();
			}
		}

		/// <summary>
		/// 将 Escape 事件分发给当前焦点界面。
		/// </summary>
		public static bool ProcessEscape() {
			if (s_processor == null) { return false; }
			try {
				return s_processor.ProcessEscape();
			} catch (Exception e) {
				Debug.LogException(e);
				return false;
			}
		}

		#endregion

		private static IUILoader s_uiloader;
		private static IUILoadingOverlay s_loading_overlay;

	}

}

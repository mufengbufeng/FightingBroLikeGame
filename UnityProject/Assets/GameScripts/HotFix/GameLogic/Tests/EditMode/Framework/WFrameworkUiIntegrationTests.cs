using Cysharp.Threading.Tasks;
using EF.Resource;
using EF.UI.WFramework;
using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GameLogic.Tests
{
    /// <summary>
    /// 验证 W-Framework UI 源码接入 EF 后的基础生命周期契约。
    /// </summary>
    [TestFixture]
    public sealed class WFrameworkUiIntegrationTests
    {
        /// <summary>
        /// 每个测试后清理上游静态状态，避免影响其它 EditMode 测试。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UIManager.Shutdown();
            UIContentBind.Shutdown();
        }

        /// <summary>
        /// 静态 UI 管理器在 Overlay Canvas 下不会依赖空的 worldCamera，并可在关闭后再次初始化。
        /// </summary>
        [Test]
        public void CoreManager_OverlayCanvas_关闭后可再次初始化()
        {
            GameObject firstRoot = CreateRoot("WFrameworkTestRootA");
            try
            {
                InitializeCore(firstRoot);
                Assert.IsTrue(UIManager.IsInitialized);

                UIManager.Update(false);
                UIManager.Shutdown();
                Assert.IsFalse(UIManager.IsInitialized);

                GameObject secondRoot = CreateRoot("WFrameworkTestRootB");
                try
                {
                    InitializeCore(secondRoot);
                    Assert.IsTrue(UIManager.IsInitialized);
                }
                finally
                {
                    Object.DestroyImmediate(secondRoot);
                }
            }
            finally
            {
                Object.DestroyImmediate(firstRoot);
            }
        }

        /// <summary>
        /// EF 适配层应消费场景已注册的 UIRoot，而不再创建或配置根节点。
        /// </summary>
        [Test]
        public void IntegrationManager_初始化_使用场景已注册根节点()
        {
            GameObject sceneCanvas = CreateRoot("WFrameworkSceneCanvas");
            GameObject root = CreateRoot("WFrameworkDirectRoot");
            var manager = new WFrameworkUIManager(new ResourceManager());
            try
            {
                root.transform.SetParent(sceneCanvas.transform, false);
                root.GetComponent<Canvas>().overrideSorting = true;
                UIRoot registeredRoot = RegisterSerializedRoot(root);
                manager.Initialize(false);

                Assert.IsTrue(manager.IsInitialized);
                Assert.AreSame(registeredRoot, UIManager.Root);
                Assert.AreSame(root.GetComponent<Canvas>(), UIManager.Root.RootCanvas);
            }
            finally
            {
                manager.Shutdown();
                Object.DestroyImmediate(sceneCanvas);
            }
        }

        /// <summary>
        /// 场景未注册 UIRoot 时，适配层不得回退到运行时创建组件或 Canvas。
        /// </summary>
        [Test]
        public void IntegrationManager_初始化_缺少场景根节点时抛出配置错误()
        {
            var manager = new WFrameworkUIManager(new ResourceManager());

            System.InvalidOperationException exception = Assert.Throws<System.InvalidOperationException>(() => manager.Initialize(false));

            StringAssert.Contains("已注册的 W-Framework UIRoot", exception.Message);
        }

        /// <summary>
        /// 导入的堆叠管理器应能挂载、显示并关闭通过加载器提供的窗口实例。
        /// </summary>
        [Test]
        public void CoreManager_打开堆叠窗口_执行完整生命周期()
        {
            GameObject root = CreateRoot("WFrameworkLifecycleRoot");
            try
            {
                TestStackLogic.Reset();
                InitializeCore(root, new WindowLoader("TestWindow", typeof(TestStackLogic)));

                Assert.IsTrue(UIManager.Open("TestWindow"));
                Assert.AreEqual(1, TestStackLogic.CreatedCount);
                Assert.AreEqual(1, TestStackLogic.OpenedCount);
                Assert.AreEqual(1, TestStackLogic.ShownCount);

                Assert.IsTrue(UIManager.CloseSingle("TestWindow"));
                Assert.AreEqual(1, TestStackLogic.HiddenCount);
                Assert.AreEqual(1, TestStackLogic.ClosedCount);
                Assert.AreEqual(1, TestStackLogic.TerminatedCount);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Prefab 加载失败时也必须结束本次打开创建的加载遮罩。
        /// </summary>
        [Test]
        public void CoreManager_Prefab加载失败_结束加载遮罩()
        {
            GameObject root = CreateRoot("WFrameworkLoadingFailureRoot");
            try
            {
                var overlay = new RecordingLoadingOverlay();
                InitializeCore(root, new FailingWindowLoader("FailedWindow", typeof(TestSetActiveLogic)), overlay);

                Assert.IsTrue(UIManager.Open("FailedWindow"));
                Assert.That(overlay.BeginCount, Is.EqualTo(1));
                Assert.That(overlay.EndCount, Is.EqualTo(1));
                Assert.That(overlay.EndKey, Is.EqualTo(overlay.BeginKey));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// 用户在异步资源返回前关闭窗口时，加载遮罩必须立即结束并仅结束一次。
        /// </summary>
        [Test]
        public void CoreManager_加载中关闭窗口_结束加载遮罩()
        {
            GameObject root = CreateRoot("WFrameworkLoadingCloseRoot");
            try
            {
                var overlay = new RecordingLoadingOverlay();
                var loader = new DeferredWindowLoader("DeferredWindow", typeof(TestSetActiveLogic));
                InitializeCore(root, loader, overlay);

                Assert.IsTrue(UIManager.Open("DeferredWindow"));
                Assert.That(overlay.BeginCount, Is.EqualTo(1));
                Assert.That(overlay.EndCount, Is.EqualTo(0));

                Assert.IsTrue(UIManager.CloseSingle("DeferredWindow"));
                Assert.That(overlay.EndCount, Is.EqualTo(1));
                Assert.That(overlay.EndKey, Is.EqualTo(overlay.BeginKey));

                loader.Complete(new GameObject("DeferredWindowLateResult", typeof(RectTransform)));
                Assert.That(overlay.EndCount, Is.EqualTo(1));
                Assert.That(loader.UnloadCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// 动态焦点固定窗口在弹窗覆盖时应失去焦点，弹窗关闭后重新获得焦点。
        /// </summary>
        [Test]
        public void CoreManager_动态焦点_弹窗覆盖时转移并恢复()
        {
            GameObject root = CreateRoot("WFrameworkDynamicFocusRoot");
            try
            {
                TestDynamicFixedLogic.Reset();
                InitializeCore(root, new FocusWindowLoader());

                Assert.IsTrue(UIManager.Open("DynamicMain"));
                Assert.That(TestDynamicFixedLogic.GainedFocusCount, Is.EqualTo(1));
                Assert.That(TestDynamicFixedLogic.LostFocusCount, Is.EqualTo(0));

                Assert.IsTrue(UIManager.Open("DynamicDialog"));
                Assert.That(TestDynamicFixedLogic.LostFocusCount, Is.EqualTo(1));

                Assert.IsTrue(UIManager.CloseSingle("DynamicDialog"));
                Assert.That(TestDynamicFixedLogic.GainedFocusCount, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// W-Framework 实例应自动补齐独立 Canvas 与射线组件，并按 SetActive 策略隐藏。
        /// </summary>
        [Test]
        public void CoreManager_窗口根节点_自动具备Canvas和射线组件()
        {
            GameObject root = CreateRoot("WFrameworkCanvasRoot");
            try
            {
                var loader = new RecordingWindowLoader("CanvasWindow", typeof(TestSetActiveLogic));
                InitializeCore(root, loader);

                Assert.IsTrue(UIManager.Open("CanvasWindow"));
                Assert.IsNotNull(loader.LastInstance);
                Assert.IsNotNull(loader.LastInstance.GetComponent<Canvas>());
                Assert.IsTrue(loader.LastInstance.GetComponent<Canvas>().overrideSorting);
                Assert.IsNotNull(loader.LastInstance.GetComponent<GraphicRaycaster>());
                Assert.IsTrue(loader.LastInstance.activeSelf);

                Assert.IsTrue(UIManager.CloseSingle("CanvasWindow"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// NewGroup 应创建独立分组，关闭底部窗口时不能误关后续弹窗。
        /// </summary>
        [Test]
        public void CoreManager_NewGroup_关闭一个分组不影响其它分组()
        {
            GameObject root = CreateRoot("WFrameworkGroupRoot");
            try
            {
                TestNamedStackLogic.Reset();
                InitializeCore(root, new MultiWindowLoader());

                Assert.IsTrue(UIManager.Open("First", "First"));
                Assert.IsTrue(UIManager.Open("Second", "Second"));
                Assert.IsTrue(UIManager.CloseGroup("First"));

                Assert.That(TestNamedStackLogic.FirstClosedCount, Is.EqualTo(1));
                Assert.That(TestNamedStackLogic.SecondClosedCount, Is.EqualTo(0));
                Assert.IsTrue(UIManager.CloseSingle("Second"));
                Assert.That(TestNamedStackLogic.SecondClosedCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// 构造供静态 UI 管理器使用的独立 Overlay Canvas 根节点。
        /// </summary>
        private static GameObject CreateRoot(string name)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            return root;
        }

        /// <summary>
        /// 使用模拟场景已序列化、已注册的 UIRoot 初始化无资源加载副作用的测试加载器。
        /// </summary>
        private static void InitializeCore(GameObject rootObject)
        {
            RegisterSerializedRoot(rootObject);
            UIManager.Init(new EmptyLoader(), null, false);
        }

        /// <summary>
        /// 以指定加载器初始化核心 UI 管理器。
        /// </summary>
        private static void InitializeCore(GameObject rootObject, IUILoader loader)
        {
            RegisterSerializedRoot(rootObject);
            UIManager.Init(loader, null, false);
        }

        /// <summary>
        /// 以指定加载器和加载遮罩初始化核心 UI 管理器。
        /// </summary>
        private static void InitializeCore(GameObject rootObject, IUILoader loader, IUILoadingOverlay overlay)
        {
            RegisterSerializedRoot(rootObject);
            UIManager.Init(loader, overlay, false);
        }

        /// <summary>
        /// 模拟场景反序列化完成后，UIRoot 在 Awake 中注册到静态管理器的状态。
        /// </summary>
        private static UIRoot RegisterSerializedRoot(GameObject rootObject)
        {
            rootObject.SetActive(false);
            var root = rootObject.AddComponent<UIRoot>();
            SetSerializedField(root, "m_RootCanvas", rootObject.GetComponent<Canvas>());
            SetSerializedField(root, "m_ParentForUI", rootObject.GetComponent<RectTransform>());
            SetSerializedField(root, "m_LayerForHide", 2);
            SetSerializedField(root, "m_StandaloneUpdate", false);
            rootObject.SetActive(true);
            UIManager.SetUIRoot(root);
            return root;
        }

        /// <summary>
        /// EditMode 测试中复现 Unity 对私有序列化字段的反序列化写入。
        /// </summary>
        private static void SetSerializedField(UIRoot root, string fieldName, object value)
        {
            FieldInfo field = typeof(UIRoot).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"未找到 UIRoot 序列化字段：{fieldName}");
            field.SetValue(root, value);
        }

        /// <summary>
        /// 用于验证初始化过程的空加载器。
        /// </summary>
        private sealed class EmptyLoader : IUILoader
        {
            /// <summary>
            /// 测试中没有注册窗口，返回默认参数。
            /// </summary>
            public ParametersForUI GetParameterForUI(string id)
            {
                return default;
            }

            /// <summary>
            /// 测试中不实际加载资源。
            /// </summary>
            public UniTask<GameObject> LoadUIObject(string path)
            {
                return UniTask.FromResult<GameObject>(null);
            }

            /// <summary>
            /// 测试中没有需要卸载的对象。
            /// </summary>
            public void UnloadUIObject(GameObject go)
            {
            }
        }

        /// <summary>
        /// 提供同步 Prefab 实例的测试加载器。
        /// </summary>
        private sealed class WindowLoader : IUILoader
        {
            private readonly ParametersForUI _parameters;

            /// <summary>
            /// 创建指定窗口配置的加载器。
            /// </summary>
            public WindowLoader(string id, System.Type logicType)
            {
                _parameters = new ParametersForUI
                {
                    id = id,
                    prefab_path = "TestWindowPrefab",
                    logic_type = logicType
                };
            }

            /// <summary>
            /// 返回唯一的测试窗口配置。
            /// </summary>
            public ParametersForUI GetParameterForUI(string id)
            {
                return id == _parameters.id ? _parameters : default;
            }

            /// <summary>
            /// 生成带 RectTransform 的临时窗口实例。
            /// </summary>
            public UniTask<GameObject> LoadUIObject(string path)
            {
                var instance = new GameObject("WFrameworkTestWindow", typeof(RectTransform));
                return UniTask.FromResult(instance);
            }

            /// <summary>
            /// 立即回收临时窗口实例。
            /// </summary>
            public void UnloadUIObject(GameObject go)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        /// <summary>
        /// 模拟资源地址无法加载的窗口加载器。
        /// </summary>
        private sealed class FailingWindowLoader : IUILoader
        {
            private readonly ParametersForUI _parameters;

            public FailingWindowLoader(string id, System.Type logicType)
            {
                _parameters = new ParametersForUI
                {
                    id = id,
                    prefab_path = id + "Prefab",
                    logic_type = logicType
                };
            }

            public ParametersForUI GetParameterForUI(string id)
            {
                return id == _parameters.id ? _parameters : default;
            }

            public UniTask<GameObject> LoadUIObject(string path)
            {
                return UniTask.FromResult<GameObject>(null);
            }

            public void UnloadUIObject(GameObject go)
            {
            }
        }

        /// <summary>
        /// 模拟可控的异步 Prefab 加载，用于验证关闭与晚到结果的生命周期。
        /// </summary>
        private sealed class DeferredWindowLoader : IUILoader
        {
            private readonly ParametersForUI _parameters;
            private readonly UniTaskCompletionSource<GameObject> _completion = new();

            public DeferredWindowLoader(string id, System.Type logicType)
            {
                _parameters = new ParametersForUI
                {
                    id = id,
                    prefab_path = id + "Prefab",
                    logic_type = logicType
                };
            }

            public int UnloadCount { get; private set; }

            public ParametersForUI GetParameterForUI(string id)
            {
                return id == _parameters.id ? _parameters : default;
            }

            public UniTask<GameObject> LoadUIObject(string path)
            {
                return _completion.Task;
            }

            public void UnloadUIObject(GameObject go)
            {
                UnloadCount++;
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            public void Complete(GameObject instance)
            {
                _completion.TrySetResult(instance);
            }
        }

        /// <summary>
        /// 记录加载遮罩的开始与结束键，验证生命周期严格配对。
        /// </summary>
        private sealed class RecordingLoadingOverlay : IUILoadingOverlay
        {
            public int BeginCount { get; private set; }

            public int EndCount { get; private set; }

            public string BeginKey { get; private set; }

            public string EndKey { get; private set; }

            public void BeginLoading(string key)
            {
                BeginCount++;
                BeginKey = key;
            }

            public void EndLoading(string key)
            {
                EndCount++;
                EndKey = key;
            }
        }

        /// <summary>
        /// 记录实例化窗口，供 Canvas 自动补齐测试读取。
        /// </summary>
        private sealed class RecordingWindowLoader : IUILoader
        {
            private readonly ParametersForUI _parameters;

            public RecordingWindowLoader(string id, System.Type logicType)
            {
                _parameters = new ParametersForUI
                {
                    id = id,
                    prefab_path = id + "Prefab",
                    logic_type = logicType
                };
            }

            public GameObject LastInstance { get; private set; }

            public ParametersForUI GetParameterForUI(string id)
            {
                return id == _parameters.id ? _parameters : default;
            }

            public UniTask<GameObject> LoadUIObject(string path)
            {
                LastInstance = new GameObject("WFrameworkRecordedWindow", typeof(RectTransform));
                return UniTask.FromResult(LastInstance);
            }

            public void UnloadUIObject(GameObject go)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        /// <summary>
        /// 为分组回归测试提供两个不同窗口。
        /// </summary>
        private sealed class MultiWindowLoader : IUILoader
        {
            public ParametersForUI GetParameterForUI(string id)
            {
                if (id != "First" && id != "Second")
                {
                    return default;
                }

                return new ParametersForUI
                {
                    id = id,
                    prefab_path = id + "Prefab",
                    logic_type = typeof(TestNamedStackLogic)
                };
            }

            public UniTask<GameObject> LoadUIObject(string path)
            {
                return UniTask.FromResult(new GameObject(path, typeof(RectTransform)));
            }

            public void UnloadUIObject(GameObject go)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        /// <summary>
        /// 提供固定主窗口与堆叠弹窗的焦点切换测试配置。
        /// </summary>
        private sealed class FocusWindowLoader : IUILoader
        {
            public ParametersForUI GetParameterForUI(string id)
            {
                if (id == "DynamicMain")
                {
                    return new ParametersForUI
                    {
                        id = id,
                        prefab_path = id + "Prefab",
                        logic_type = typeof(TestDynamicFixedLogic)
                    };
                }

                if (id == "DynamicDialog")
                {
                    return new ParametersForUI
                    {
                        id = id,
                        prefab_path = id + "Prefab",
                        logic_type = typeof(TestFocusStackLogic)
                    };
                }

                return default;
            }

            public UniTask<GameObject> LoadUIObject(string path)
            {
                return UniTask.FromResult(new GameObject(path, typeof(RectTransform)));
            }

            public void UnloadUIObject(GameObject go)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        /// <summary>
        /// 用于窗口描述验证的最小堆叠逻辑。
        /// </summary>
        private sealed class TestStackLogic : UIStackLogicBase
        {
            /// <summary>
            /// 逻辑被创建的次数。
            /// </summary>
            public static int CreatedCount { get; private set; }

            /// <summary>
            /// 窗口打开回调次数。
            /// </summary>
            public static int OpenedCount { get; private set; }

            /// <summary>
            /// 窗口显示回调次数。
            /// </summary>
            public static int ShownCount { get; private set; }

            /// <summary>
            /// 窗口隐藏回调次数。
            /// </summary>
            public static int HiddenCount { get; private set; }

            /// <summary>
            /// 窗口关闭回调次数。
            /// </summary>
            public static int ClosedCount { get; private set; }

            /// <summary>
            /// 逻辑终止回调次数。
            /// </summary>
            public static int TerminatedCount { get; private set; }

            /// <summary>
            /// 测试窗口不是全屏窗口。
            /// </summary>
            protected override bool IsFullScreen => false;

            /// <summary>
            /// 测试窗口每次打开均创建新分组。
            /// </summary>
            protected override bool NewGroup => true;

            /// <summary>
            /// 测试中不播放进入动画。
            /// </summary>
            protected override string OpenAnim => null;

            /// <summary>
            /// 测试中不播放关闭动画。
            /// </summary>
            protected override string CloseAnim => null;

            /// <summary>
            /// 重置静态观测数据。
            /// </summary>
            public static void Reset()
            {
                CreatedCount = 0;
                OpenedCount = 0;
                ShownCount = 0;
                HiddenCount = 0;
                ClosedCount = 0;
                TerminatedCount = 0;
            }

            /// <summary>
            /// 记录逻辑创建回调。
            /// </summary>
            protected override bool OnCreate(object para)
            {
                CreatedCount++;
                return true;
            }

            /// <summary>
            /// 记录窗口打开回调。
            /// </summary>
            protected override void OnOpen(GameObject go, int baseSortingOrder)
            {
                OpenedCount++;
            }

            /// <summary>
            /// 记录窗口显示回调。
            /// </summary>
            protected override void OnShow(bool first)
            {
                ShownCount++;
            }

            /// <summary>
            /// 记录窗口隐藏回调。
            /// </summary>
            protected override void OnHide()
            {
                HiddenCount++;
            }

            /// <summary>
            /// 记录窗口关闭回调。
            /// </summary>
            protected override void OnClose()
            {
                ClosedCount++;
            }

            /// <summary>
            /// 记录逻辑终止回调。
            /// </summary>
            protected override void OnTerminated()
            {
                TerminatedCount++;
            }
        }

        /// <summary>
        /// 记录固定窗口的动态焦点回调。
        /// </summary>
        private sealed class TestDynamicFixedLogic : UIFixedLogicBase, IUIDynamicFocusable
        {
            public static int GainedFocusCount { get; private set; }

            public static int LostFocusCount { get; private set; }

            protected override int SortingOrderBias => 0;

            public static void Reset()
            {
                GainedFocusCount = 0;
                LostFocusCount = 0;
            }

            void IUIDynamicFocusable.SetDynamicFocusAgent(IUILogicDynamicFocusAgent agent)
            {
                agent.RequireFocus();
            }

            void IUIFocusable.OnGetFocus()
            {
                GainedFocusCount++;
            }

            void IUIFocusable.OnLoseFocus()
            {
                LostFocusCount++;
            }

            bool IUIFocusable.OnESC()
            {
                return false;
            }
        }

        /// <summary>
        /// 仅用于夺取固定窗口焦点的普通栈式弹窗。
        /// </summary>
        private sealed class TestFocusStackLogic : UIStackLogicBase
        {
            protected override bool IsFullScreen => false;

            protected override bool NewGroup => true;

            protected override string OpenAnim => null;

            protected override string CloseAnim => null;
        }

        /// <summary>
        /// 验证 SetActive 可见性策略和窗口根 Canvas。
        /// </summary>
        private sealed class TestSetActiveLogic : UIStackLogicBase
        {
            protected override bool IsFullScreen => false;

            protected override bool NewGroup => true;

            protected override eUIVisibleOperateType VisibleOperateType => eUIVisibleOperateType.SetActive;

            protected override string OpenAnim => null;

            protected override string CloseAnim => null;
        }

        /// <summary>
        /// 以窗口 id 记录关闭次数，验证独立分组行为。
        /// </summary>
        private sealed class TestNamedStackLogic : UIStackLogicBase
        {
            private string _id;

            public static int FirstClosedCount { get; private set; }

            public static int SecondClosedCount { get; private set; }

            protected override bool IsFullScreen => false;

            protected override bool NewGroup => true;

            protected override string OpenAnim => null;

            protected override string CloseAnim => null;

            protected override bool OnCreate(object parameter)
            {
                _id = parameter as string;
                return true;
            }

            protected override void OnClose()
            {
                if (_id == "First")
                {
                    FirstClosedCount++;
                }
                else if (_id == "Second")
                {
                    SecondClosedCount++;
                }
            }

            public static void Reset()
            {
                FirstClosedCount = 0;
                SecondClosedCount = 0;
            }
        }
    }
}

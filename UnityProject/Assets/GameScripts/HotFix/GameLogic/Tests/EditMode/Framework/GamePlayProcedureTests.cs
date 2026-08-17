using System;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using EF.Resource;
using EF.UI.WFramework;
using GameLogic.GamePlay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using YooAsset;
using SceneHandle = YooAsset.SceneHandle;

namespace GameLogic.Tests
{
    /// <summary>
    /// 验证主菜单与 GamePlay 流程切换契约。
    /// </summary>
    [TestFixture]
    public sealed class GamePlayProcedureTests
    {
        [TearDown]
        public void TearDown()
        {
            GameLogicEntry.SetWFrameworkUIManagerForTests(null);
            GameLogicEntry.SetResourceManagerForTests(null);
        }

        /// <summary>
        /// 主窗口流程切到 GamePlay 后应关闭主菜单分组。
        /// </summary>
        [Test]
        public void MainWindowProcedure_切换GamePlay后关闭主菜单分组()
        {
            var uiManager = new RecordingUiManager { IsInitialized = true };
            GameLogicEntry.SetWFrameworkUIManagerForTests(uiManager);
            GameLogicEntry.SetResourceManagerForTests(new PendingResourceManager());

            var fsmManager = new EF.Fsm.FsmManager();
            var procedureManager = new EF.Procedure.ProcedureManager();
            try
            {
                procedureManager.Initialize(fsmManager, new MainWindowProcedure(), new GamePlayProcedure());
                procedureManager.StartProcedure<MainWindowProcedure>();
                fsmManager.GetFsm<EF.Procedure.IProcedureManager>("Procedure").ChangeState<GamePlayProcedure>();

                Assert.That(uiManager.ClosedGroupId, Is.EqualTo("MainWindow"));
                Assert.That(procedureManager.CurrentProcedure, Is.TypeOf<GamePlayProcedure>());
            }
            finally
            {
                procedureManager.Shutdown();
                fsmManager.Shutdown();
            }
        }

        /// <summary>
        /// 资源管理器为空时，GamePlay 进入不得抛异常，并应回到主菜单流程。
        /// </summary>
        [Test]
        public void GamePlayProcedure_资源管理器为空时回到主菜单()
        {
            var uiManager = new RecordingUiManager { IsInitialized = true };
            GameLogicEntry.SetResourceManagerForTests(null);
            GameLogicEntry.SetWFrameworkUIManagerForTests(uiManager);

            var fsmManager = new EF.Fsm.FsmManager();
            var procedureManager = new EF.Procedure.ProcedureManager();
            try
            {
                LogAssert.Expect(LogType.Error, "[GamePlayProcedure] 资源管理器为空。");
                procedureManager.Initialize(fsmManager, new MainWindowProcedure(), new GamePlayProcedure());
                Assert.DoesNotThrow(() => procedureManager.StartProcedure<GamePlayProcedure>());
                Assert.That(procedureManager.CurrentProcedure, Is.TypeOf<MainWindowProcedure>());
            }
            finally
            {
                procedureManager.Shutdown();
                fsmManager.Shutdown();
            }
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

        /// <summary>
        /// 让关卡加载挂起，避免测试在断言前被失败回退带走。
        /// </summary>
        private sealed class PendingResourceManager : IResourceManager
        {
            public ResourceMode Mode => ResourceMode.EditorSimulate;

            public bool IsInitialized => true;

            public bool UsesYooAssets => true;

            public string DefaultPackageName => "DefaultPackage";

            public ResourceModeConfig Configuration => null;

            public IResourceBackgroundDownloadService BackgroundDownloads => null;

            public UniTask InitializeAsync(ResourceModeConfig overrideConfig = null, IProgress<float> progress = null)
            {
                return UniTask.CompletedTask;
            }

            public UniTask<T> Load<T>(string location, Action<float> progress = null, uint priority = 0)
                where T : UnityEngine.Object
            {
                return UniTask.Never<T>(CancellationToken.None);
            }

            public ResourcePackage GetPackage(string packageName) => throw new NotSupportedException();

            public ResourcePackage GetDefaultPackage() => throw new NotSupportedException();

            public UniTask<AssetHandle> LoadAssetAsync<T>(string location, Action<float> progress = null, uint priority = 0)
                where T : UnityEngine.Object
            {
                throw new NotSupportedException();
            }

            public AssetHandle LoadAssetSync<T>(string location, uint priority = 0)
                where T : UnityEngine.Object
            {
                throw new NotSupportedException();
            }

            public UniTask<SceneHandle> LoadSceneAsync(
                string location,
                LoadSceneMode sceneMode = LoadSceneMode.Single,
                LocalPhysicsMode physicsMode = LocalPhysicsMode.None,
                bool allowSceneActivation = true,
                uint priority = 0,
                Action<float> progress = null)
            {
                throw new NotSupportedException();
            }

            public void UnloadScene(SceneHandle handle)
            {
            }

            public void Release(HandleBase handle)
            {
            }

            public void Release(UnityEngine.Object asset)
            {
            }

            public void ReleaseAll()
            {
            }

            public void Shutdown()
            {
            }

            public void Update(float elapseSeconds, float realElapseSeconds)
            {
            }
        }
    }
}

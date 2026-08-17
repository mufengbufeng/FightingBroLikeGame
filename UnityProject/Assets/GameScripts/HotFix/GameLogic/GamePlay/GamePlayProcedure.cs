using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using EF.Debugger;
using EF.Entity;
using EF.Procedure;
using EF.Resource;
using UnityEngine;
using ProcedureOwner = EF.Fsm.IFsm<EF.Procedure.IProcedureManager>;

namespace GameLogic.GamePlay
{
    /// <summary>
    /// 加载并驻留 GamePlay 关卡 Prefab 的流程。
    /// </summary>
    public sealed class GamePlayProcedure : ProcedureBase
    {
        private CancellationTokenSource _loadCts;
        private GameObject _levelAsset;
        private GameObject _levelInstance;
        private LevelRoot _levelRoot;
        private int _playerEntityId;
        private Camera _entryCamera;
        private AudioListener _entryAudioListener;
        private bool _entryCameraWasEnabled;
        private bool _entryAudioListenerWasEnabled;
        private bool _disabledEntryCamera;
        private ProcedureOwner _procedureOwner;

        /// <summary>
        /// 读取关卡请求并开始异步加载。
        /// </summary>
        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
            _procedureOwner = procedureOwner;

            if (!procedureOwner.TryGetData(GameLogicEntry.GamePlayLevelRequestKey, out GamePlayLevelRequest request)
                || request == null)
            {
                request = new GamePlayLevelRequest("Level_01");
            }

            _loadCts = new CancellationTokenSource();
            LoadLevelAsync(request.Address, _loadCts.Token).Forget();
        }

        /// <summary>
        /// 取消加载、隐藏玩家实体、销毁关卡并恢复入口相机。可重入。
        /// </summary>
        protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
            base.OnLeave(procedureOwner, isShutdown);
            CancelLoad();
            HidePlayerEntity();
            DestroyLevelInstance();
            ReleaseLevelAsset();
            RestoreEntryCamera();
            _levelRoot = null;
            _procedureOwner = null;
        }

        private async UniTaskVoid LoadLevelAsync(string address, CancellationToken cancellationToken)
        {
            IResourceManager resource = GameLogicEntry.Resource;
            if (resource == null)
            {
                Log.Error("[GamePlayProcedure] 资源管理器为空。");
                ReturnToMainWindow(cancellationToken);
                return;
            }

            GameObject loadedAsset = null;
            try
            {
                loadedAsset = await resource.Load<GameObject>(address);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                Log.Error($"[GamePlayProcedure] 关卡加载失败：{address}");
                ReturnToMainWindow(cancellationToken);
                return;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                if (loadedAsset != null)
                {
                    resource.Release(loadedAsset);
                }

                return;
            }

            if (loadedAsset == null)
            {
                Log.Error($"[GamePlayProcedure] 关卡加载失败：{address}");
                ReturnToMainWindow(cancellationToken);
                return;
            }

            _levelAsset = loadedAsset;
            _levelInstance = UnityEngine.Object.Instantiate(_levelAsset);
            _levelRoot = _levelInstance.GetComponent<LevelRoot>();
            if (_levelRoot == null)
            {
                Log.Error("[GamePlayProcedure] 关卡 Prefab 缺少 LevelRoot。");
                DestroyLevelInstance();
                ReleaseLevelAsset();
                ReturnToMainWindow(cancellationToken);
                return;
            }

            if (!await ShowPlayerAsync(cancellationToken))
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    DestroyLevelInstance();
                    ReleaseLevelAsset();
                    ReturnToMainWindow(cancellationToken);
                }

                return;
            }

            DisableEntryCameraIfNeeded(_levelRoot.GamePlayCamera);
        }

        private async UniTask<bool> ShowPlayerAsync(CancellationToken cancellationToken)
        {
            IEntityManager entityManager = GameLogicEntry.Entity;
            if (entityManager == null)
            {
                Log.Error("[GamePlayProcedure] 实体管理器为空。");
                return false;
            }

            Transform playerSpawn = _levelRoot.PlayerSpawn;
            if (playerSpawn == null)
            {
                Log.Error("[GamePlayProcedure] 关卡未配置玩家出生点。");
                return false;
            }

            if (!entityManager.HasEntityGroup(PlayerEntity.GroupName)
                && !entityManager.AddEntityGroup(
                    PlayerEntity.GroupName,
                    new EntityGroupOptions
                    {
                        EntityFactory = () => new PlayerEntity(),
                        AllowMultiSpawn = false,
                        AutoRelease = false,
                        PoolCapacity = 1
                    }))
            {
                Log.Error("[GamePlayProcedure] 玩家实体组创建失败。");
                return false;
            }

            int playerEntityId = entityManager.GenerateEntityId();
            _playerEntityId = playerEntityId;
            var spawnData = new PlayerEntitySpawnData(
                playerSpawn.position,
                playerSpawn.rotation,
                playerSpawn.lossyScale);

            try
            {
                await entityManager.ShowEntityAsync(
                    playerEntityId,
                    PlayerEntity.AssetName,
                    PlayerEntity.GroupName,
                    spawnData);
            }
            catch (Exception exception)
            {
                if (_playerEntityId == playerEntityId)
                {
                    _playerEntityId = 0;
                }

                Log.Error($"[GamePlayProcedure] 玩家实体创建失败：{exception}");
                return false;
            }

            if (cancellationToken.IsCancellationRequested || _playerEntityId != playerEntityId)
            {
                entityManager.HideEntity(playerEntityId);
                return false;
            }

            return true;
        }

        private void HidePlayerEntity()
        {
            if (_playerEntityId == 0)
            {
                return;
            }

            int playerEntityId = _playerEntityId;
            _playerEntityId = 0;
            GameLogicEntry.Entity?.HideEntity(playerEntityId);
        }

        private void ReturnToMainWindow(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested || _procedureOwner == null)
            {
                return;
            }

            ChangeState<MainWindowProcedure>(_procedureOwner);
        }

        private void CancelLoad()
        {
            if (_loadCts == null)
            {
                return;
            }

            _loadCts.Cancel();
            _loadCts.Dispose();
            _loadCts = null;
        }

        private void DestroyLevelInstance()
        {
            if (_levelInstance == null)
            {
                return;
            }

            UnityEngine.Object.Destroy(_levelInstance);
            _levelInstance = null;
        }

        private void ReleaseLevelAsset()
        {
            if (_levelAsset == null)
            {
                return;
            }

            GameLogicEntry.Resource?.Release(_levelAsset);
            _levelAsset = null;
        }

        private void DisableEntryCameraIfNeeded(Camera gamePlayCamera)
        {
            if (gamePlayCamera == null)
            {
                Log.Warning("[GamePlayProcedure] 关卡未配置 GamePlayCamera，保留入口相机。");
                return;
            }

            GameObject mainCameraObject = GameObject.FindGameObjectWithTag("MainCamera");
            if (mainCameraObject == null)
            {
                return;
            }

            _entryCamera = mainCameraObject.GetComponent<Camera>();
            if (_entryCamera != null)
            {
                _entryCameraWasEnabled = _entryCamera.enabled;
                _entryCamera.enabled = false;
                _disabledEntryCamera = true;
            }

            _entryAudioListener = mainCameraObject.GetComponent<AudioListener>();
            if (_entryAudioListener != null)
            {
                _entryAudioListenerWasEnabled = _entryAudioListener.enabled;
                _entryAudioListener.enabled = false;
            }
        }

        private void RestoreEntryCamera()
        {
            if (_disabledEntryCamera && _entryCamera != null)
            {
                _entryCamera.enabled = _entryCameraWasEnabled;
            }

            if (_entryAudioListener != null)
            {
                _entryAudioListener.enabled = _entryAudioListenerWasEnabled;
            }

            _entryCamera = null;
            _entryAudioListener = null;
            _disabledEntryCamera = false;
        }
    }
}

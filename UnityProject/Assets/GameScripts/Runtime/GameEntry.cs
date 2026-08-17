using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using EF.Bootstrap;
using EF.Common;
using EF.Commercial;
using EF.Debugger;
using EF.Entity;
using EF.Fsm;
using EF.HotFix;
using EF.Model;
using EF.ObjectPool;
using EF.Procedure;
using EF.Resource;
using EF.Save;
using EF.Sound;
using EF.Timer;
using WFrameworkUI = EF.UI.WFramework;
using HybridCLR;
using UnityEngine;

public class GameEntry : MonoBehaviour
{
    private const string HotfixConfigResourcesPath = "HotFixConfig";
    private const float ResourceInitializationProgressEnd = 0.6f;
    private const float HotfixAssemblyProgressStart = 0.6f;
    private const float HotfixAssemblyProgressEnd = 0.85f;
    private const float GameUiProgress = 0.95f;
    // private const string HotfixDllAssetRoot = "Assets/AssetRaw/DLL";

    private readonly List<Assembly> _loadedHotfixAssemblies = new();
    private IResourceManager _resourceManager;
    private HotFixConfig _hotFixConfig;
    private IEntityManager _entityManager;
    private bool _moduleSystemUpdateEnabled;

    private void Awake()
    {
        BootstrapLoadingService.Show("正在准备游戏");

        // DontDestroyOnLoad(this);

        // 1. 先注册资源管理器（其他管理器可能依赖它）
        ModuleSystem.Register<IResourceManager>(new ResourceManager());
        _resourceManager = ModuleSystem.Get<IResourceManager>();

        // 2. 注册不需要依赖的管理器
        ModuleSystem.Register<ITimerManager>(new TimerManager());
        ModuleSystem.Register<IObjectPoolManager>(new ObjectPoolManager());
        ModuleSystem.Register<IFsmManager>(new FsmManager());
        ModuleSystem.Register<IProcedureManager>(new ProcedureManager());
        ModuleSystem.Register<ISaveManager>(new SaveManager());
        ModuleSystem.Register<ICommercialService>(new CommercialManager());
        ModuleSystem.Register(new ModelManager());
        // 3. 注册需要 ResourceManager 的管理器
        ModuleSystem.Register<WFrameworkUI.IWFrameworkUIManager>(new WFrameworkUI.WFrameworkUIManager(_resourceManager));
        ModuleSystem.Register<ISoundManager>(new SoundManager(_resourceManager));

        // 5. 注册 EntityManager（依赖 ObjectPoolManager 和 ResourceManager）
        var entityManager = new EntityManager();
        entityManager.SetObjectPoolManager(ModuleSystem.Get<IObjectPoolManager>());
        entityManager.SetResourceManager(_resourceManager);
        entityManager.SetEntityHelper(new DefaultEntityHelper());
        ModuleSystem.Register<IEntityManager>(entityManager);
        _entityManager = entityManager;

        Log.Info("[GameEntry] EF 框架管理器注册完成。");

        Init().Forget();
    }

    /// <summary>
    /// 初始化资源系统、加载热更新程序集并启动热更入口。
    /// </summary>
    private async UniTask Init()
    {
        await UniTask.NextFrame();

        try
        {
            await InitializeGameAsync();
        }
        catch (Exception exception)
        {
            BootstrapLoadingService.SetStatus("启动失败，请检查资源包");
            Log.Error($"[GameEntry] 热更初始化失败：{exception}");
        }
    }

    /// <summary>
    /// 执行资源、热更新程序集和游戏入口的启动流程。
    /// </summary>
    private async UniTask InitializeGameAsync()
    {
        BootstrapLoadingService.SetStatus("正在加载游戏资源");
        await _resourceManager.InitializeAsync(progress: new Progress<float>(UpdateResourceInitializationProgress));
        LoadHotfixConfig();
        if (!_hotFixConfig.EnableHotFix)
        {
            CompleteWithoutHotFix();
            return;
        }

        BootstrapLoadingService.SetProgress(HotfixAssemblyProgressStart, "正在加载运行时组件");
#if !UNITY_EDITOR
        await LoadAotMetadataAssembliesAsync();
#endif
        BootstrapLoadingService.SetProgress(HotfixAssemblyProgressEnd, "正在加载游戏逻辑");
        await LoadHotUpdateAssembliesAsync();
        BootstrapLoadingService.SetProgress(GameUiProgress, "正在打开游戏");
        InvokeHotfixEntry();

        // 热更入口初始化完成后，才开始驱动 ModuleSystem.Update，避免未初始化状态下的误更新。
        _moduleSystemUpdateEnabled = true;

        Log.Info("[GameEntry] 热更初始化流程完成。");
    }

    /// <summary>
    /// 在关闭热更新时从已编译进 Player 的 AOT 游戏程序集启动本地游戏。
    /// </summary>
    private void CompleteWithoutHotFix()
    {
        BootstrapLoadingService.SetProgress(GameUiProgress, "正在打开本地游戏");
        AotLocalGameEntry.Init();
        _moduleSystemUpdateEnabled = true;
        Log.Info("[GameEntry] 已关闭热更新，AOT 本地游戏初始化完成。");
    }

    private void Update()
    {
        if (!_moduleSystemUpdateEnabled)
        {
            return;
        }

        ModuleSystem.Update(Time.deltaTime, Time.unscaledDeltaTime);
    }

    /// <summary>
    /// 在物理帧中驱动实体系统，确保实体可与 Unity 物理系统同步。
    /// </summary>
    private void FixedUpdate()
    {
        if (!_moduleSystemUpdateEnabled)
        {
            return;
        }

        _entityManager?.FixedUpdate(Time.fixedDeltaTime);
    }

    /// <summary>
    /// 场景入口销毁时关闭全部模块，释放 W-Framework 的静态 UI 状态和资源句柄。
    /// </summary>
    private void OnDestroy()
    {
        _moduleSystemUpdateEnabled = false;
        ModuleSystem.ShutdownAll();
    }

    /// <summary>
    /// 读取资源后端初始化前必须可用的热更新启动配置，因此固定从 Resources 直读。
    /// </summary>
    private void LoadHotfixConfig()
    {
        if (_hotFixConfig != null)
        {
            return;
        }

        _hotFixConfig = Resources.Load<HotFixConfig>(HotfixConfigResourcesPath);
        if (_hotFixConfig == null)
        {
            throw new InvalidOperationException($"未找到热更配置资源：{HotfixConfigResourcesPath}");
        }
    }

    /// <summary>
    /// 将资源系统进度映射到启动界面的前半段。
    /// </summary>
    private static void UpdateResourceInitializationProgress(float progress)
    {
        BootstrapLoadingService.SetProgress(
            Mathf.Lerp(0f, ResourceInitializationProgressEnd, Mathf.Clamp01(progress)));
    }

    /// <summary>
    /// 异步读取并加载 HybridCLR AOT 补充元数据。
    /// </summary>
    private async UniTask LoadAotMetadataAssembliesAsync()
    {
        foreach (string dllName in _hotFixConfig.aotMetaDlls)
        {
            byte[] dllBytes = await LoadDllBytesAsync(dllName);
            LoadImageErrorCode result = RuntimeApi.LoadMetadataForAOTAssembly(dllBytes, HomologousImageMode.SuperSet);
            if (result != LoadImageErrorCode.OK)
            {
                Log.Warning($"[GameEntry] 加载AOT元数据失败：{dllName}，返回码：{result}");
            }
            else
            {
                Log.Info($"[GameEntry] 已加载AOT元数据：{dllName}");
            }
        }
    }

    /// <summary>
    /// 异步准备 Player 热更新程序集；编辑器中复用已经加载的程序集。
    /// </summary>
    private async UniTask LoadHotUpdateAssembliesAsync()
    {
        _loadedHotfixAssemblies.Clear();

#if !UNITY_EDITOR
        // 运行时环境：通过 ResourceManager 加载 DLL 字节码
        foreach (string dllName in _hotFixConfig.hotFixDlls)
        {
            byte[] dllBytes = await LoadDllBytesAsync(dllName);
            Assembly assembly = Assembly.Load(dllBytes);
            _loadedHotfixAssemblies.Add(assembly);
            Log.Info($"[GameEntry] 已加载热更程序集：{dllName}");
        }
#else
        // 编辑器环境：从 AppDomain 获取已加载的程序集，避免重复加载
        foreach (string dllName in _hotFixConfig.hotFixDlls)
        {
            string assemblyName = dllName.Replace(".dll.bytes", "").Replace(".dll", "");
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == assemblyName);

            if (assembly != null)
            {
                _loadedHotfixAssemblies.Add(assembly);
                Log.Info($"[GameEntry] 编辑器环境：已找到程序集 {assemblyName}");
            }
            else
            {
                Log.Warning($"[GameEntry] 编辑器环境：未找到程序集 {assemblyName}");
            }
        }
#endif
        await UniTask.CompletedTask;
    }

    private void InvokeHotfixEntry()
    {
        const string entryTypeName = "GameLogic.GameLogicEntry";
        const string entryMethodName = "Init";

        foreach (Assembly assembly in _loadedHotfixAssemblies)
        {
            Type entryType = assembly.GetType(entryTypeName);
            if (entryType == null)
            {
                continue;
            }

            MethodInfo initMethod = entryType.GetMethod(entryMethodName, BindingFlags.Public | BindingFlags.Static);
            if (initMethod == null)
            {
                throw new InvalidOperationException($"在类型 {entryTypeName} 中未找到静态方法 {entryMethodName}");
            }

            initMethod.Invoke(null, null);
            Log.Info("[GameEntry] 热更入口初始化完成。");
            return;
        }

        throw new InvalidOperationException($"未在任何热更程序集内找到入口类型 {entryTypeName}");
    }

    /// <summary>
    /// 通过统一资源接口异步读取 DLL 字节并及时释放资源引用。
    /// </summary>
    private async UniTask<byte[]> LoadDllBytesAsync(string dllName)
    {
        string assetPath = dllName;
        TextAsset textAsset = await Resource.Load<TextAsset>(assetPath);
        try
        {
            if (textAsset == null)
            {
                throw new InvalidOperationException($"无法读取热更DLL资源：{assetPath}");
            }

            return textAsset.bytes;
        }
        finally
        {
            if (textAsset != null)
            {
                Resource.Release(textAsset);
            }
        }
    }
}

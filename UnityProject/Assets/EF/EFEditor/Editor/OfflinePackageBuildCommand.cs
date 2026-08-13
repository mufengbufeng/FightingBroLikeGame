#if ENABLE_HYBRIDCLR
using HybridCLR.Editor.Commands;
#endif
using System.IO;
using EF.HotFix;
using EF.Resource;
using YooAsset;
using YooAsset.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// 为当前资源部署配置生成 HybridCLR DLL 与 YooAsset 资源包。
/// </summary>
public sealed class OfflinePackageBuildCommand : IPreprocessBuildWithReport
{
    private const string PackageName = "DefaultPackage";
    private const string BundleOutputRoot = "Bundles";
    private const string AssetRawDllPath = "Assets/AssetRaw/DLL";

    private static bool _isPreparingPlayerBuild;

    public int callbackOrder => 0;

    /// <summary>
    /// Player 构建期间不能启动 HybridCLR 或 YooAsset 构建：
    /// HybridCLR 的 AOT 裁剪会启动临时 Player Build，YooAsset 也不支持嵌套构建。
    /// </summary>
    public void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report)
    {
        // Packages must be prepared from the menu command before exporting the player.
    }

    /// <summary>
    /// 通过菜单手动生成当前平台的资源包。
    /// </summary>
    [MenuItem("YooAsset/Build Package For Current Resource Deployment", false, 103)]
    public static void BuildOfflinePackageForActiveTarget()
    {
        PreparePackage(EditorUserBuildSettings.activeBuildTarget, generateHybridClrDlls: true);
    }

    /// <summary>
    /// 防止 HybridCLR 的临时 Player 构建递归触发资源包构建。
    /// </summary>
    private static void PreparePackage(BuildTarget target, bool generateHybridClrDlls)
    {
        if (_isPreparingPlayerBuild)
        {
            return;
        }

        _isPreparingPlayerBuild = true;
        try
        {
            ResourceModeConfig deploymentConfig = LoadResourceModeConfig();
            ResourceRuntimePlatform platform = ResolveResourcePlatform(target);
            bool copyBuiltinResources = deploymentConfig.RequiresBuiltinPackage(platform);
            BuildPackage(target, deploymentConfig.PackageVersion, copyBuiltinResources, generateHybridClrDlls);
        }
        finally
        {
            _isPreparingPlayerBuild = false;
        }
    }

    /// <summary>
    /// 从 Resources 目录加载 Editor 与运行时共用的资源部署配置资产。
    /// </summary>
    private static ResourceModeConfig LoadResourceModeConfig()
    {
        string configPath = $"Assets/Resources/{ResourceModeConfig.DefaultResourcesPath}.asset";
        ResourceModeConfig config = AssetDatabase.LoadAssetAtPath<ResourceModeConfig>(configPath);
        if (config == null)
        {
            throw new BuildFailedException($"未找到资源部署配置资产：{configPath}");
        }

        return config;
    }

    /// <summary>
    /// 根据 Player 构建目标与脚本宏识别当前资源运行平台。
    /// </summary>
    private static ResourceRuntimePlatform ResolveResourcePlatform(BuildTarget target)
    {
        if (target != BuildTarget.WebGL)
        {
            return ResourceRuntimePlatform.Standard;
        }

        string defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.WebGL);
        if (ContainsScriptingDefine(defines, "WEIXINMINIGAME") ||
            ContainsScriptingDefine(defines, "UNITY_WECHATMINIGAME"))
        {
            return ResourceRuntimePlatform.WechatMiniGame;
        }

        return ContainsScriptingDefine(defines, "DOUYINMINIGAME")
            ? ResourceRuntimePlatform.TiktokMiniGame
            : ResourceRuntimePlatform.Standard;
    }

    /// <summary>
    /// 生成资源包；仅内置模式会复制文件到 StreamingAssets。
    /// </summary>
    private static void BuildPackage(
        BuildTarget target,
        string packageVersion,
        bool copyBuiltinResources,
        bool generateHybridClrDlls)
    {
        if (target == BuildTarget.NoTarget)
        {
            throw new BuildFailedException("未选择有效的正式构建平台。");
        }

        EnsureHybridClrDlls(generateHybridClrDlls);
        AssetDatabase.Refresh();

        var buildParameters = new LegacyBuildParameters
        {
            BuildOutputRoot = Path.GetFullPath(BundleOutputRoot),
            BundledFileRoot = BundleBuilderHelper.GetStreamingAssetsRoot(),
            BuildPipeline = EBuildPipeline.LegacyBuildPipeline.ToString(),
            BuildBundleType = (int)EBundleType.AssetBundle,
            BuildTarget = target,
            PackageName = PackageName,
            PackageVersion = packageVersion,
            EnableSharePackRule = true,
            VerifyBuildingResult = true,
            FileNameStyle = EFileNameStyle.HashName,
            BundledCopyOption = copyBuiltinResources
                ? EBundledCopyOption.ClearAndCopyAll
                : EBundledCopyOption.None,
            BundledCopyParams = string.Empty,
            CompressOption = ECompressOption.LZ4,
            ClearBuildCacheFiles = true,
            UseAssetDependencyDB = true
        };

        var pipeline = new LegacyBuildPipeline();
        BuildResult buildResult = pipeline.Run(buildParameters, true);
        if (!buildResult.Success)
        {
            throw new BuildFailedException($"YooAsset 资源构建失败：{buildResult.ErrorInfo}");
        }

        string delivery = copyBuiltinResources ? "内置资源包" : "远端 CDN 资源包";
        Debug.Log($"[ResourcePackageBuild] 已生成{delivery}：{buildResult.OutputPackageDirectory}");
    }

    /// <summary>
    /// 判断脚本宏集合是否包含指定宏。
    /// </summary>
    private static bool ContainsScriptingDefine(string defines, string define)
    {
        if (string.IsNullOrWhiteSpace(defines))
        {
            return false;
        }

        foreach (string value in defines.Split(';'))
        {
            if (string.Equals(value.Trim(), define, System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 将编译后的热更 DLL 和可用的 AOT 元数据放入 YooAsset 收集目录。
    /// </summary>
    private static void EnsureHybridClrDlls(bool generateHybridClrDlls)
    {
        HotFixConfig hotFixConfig = LoadHotFixConfig();
        HotFixBuildModeSynchronizer.Synchronize(hotFixConfig);
        if (!hotFixConfig.EnableHotFix)
        {
            Debug.Log("[ResourcePackageBuild] 已关闭热更新，跳过热更新 DLL 生成。");
            return;
        }

#if ENABLE_HYBRIDCLR
        if (generateHybridClrDlls)
        {
            SyncAssemblyContent.RefreshAssembly();
            PrebuildCommand.GenerateAll();
            BuildDLLCommand.CopyHotUpdateAssembliesToAssetPath();
            BuildDLLCommand.CopyAOTAssembliesToAssetPath();
        }
#else
        throw new BuildFailedException("正式离线包需要启用 HybridCLR 后再构建。");
#endif

        if (!Directory.Exists(AssetRawDllPath))
        {
            throw new BuildFailedException($"未找到热更 DLL 目录：{AssetRawDllPath}");
        }

        ValidateConfiguredDllAssets(hotFixConfig);
    }

    /// <summary>
    /// 从 Resources 目录读取 Editor 与运行时共用的热更新启动配置。
    /// </summary>
    private static HotFixConfig LoadHotFixConfig()
    {
        HotFixConfig hotFixConfig = Resources.Load<HotFixConfig>("HotFixConfig");
        if (hotFixConfig == null)
        {
            throw new BuildFailedException("未找到 Resources/HotFixConfig.asset");
        }

        return hotFixConfig;
    }

    /// <summary>
    /// 在构建阶段检测热更配置与实际打入 YooAsset 的 DLL 是否一致，避免运行时才报 Location is invalid。
    /// </summary>
    private static void ValidateConfiguredDllAssets(HotFixConfig hotFixConfig)
    {
        foreach (string dllName in hotFixConfig.GetAllDlls())
        {
            string assetPath = Path.Combine(AssetRawDllPath, dllName + ".bytes");
            if (!File.Exists(assetPath))
            {
                throw new BuildFailedException(
                    $"热更配置要求的 DLL 未生成：{assetPath}。请检查 HybridCLR AOT 元数据设置。");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using EF.HotFix;
using HybridCLR.Editor.Settings;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// 使热更新开关与 HybridCLR 的程序集分类保持一致。
/// </summary>
public sealed class HotFixBuildModeSynchronizer : IPreprocessBuildWithReport
{
    private const string HotFixConfigAssetPath = "Assets/Resources/HotFixConfig.asset";
    private const string GameLogicAssemblyDefinitionPath = "Assets/GameScripts/HotFix/GameLogic/GameLogic.asmdef";
    private const string GameProtoAssemblyDefinitionPath = "Assets/GameScripts/HotFix/GameProto/GameProto.asmdef";

    public int callbackOrder => -1000;

    /// <summary>
    /// 在正式 Player 构建前同步当前启动模式，确保本地模式将游戏程序集编译为 AOT。
    /// </summary>
    public void OnPreprocessBuild(BuildReport report)
    {
        SynchronizeForActiveConfiguration();
    }

    /// <summary>
    /// 从菜单手动同步当前 HotFixConfig 选择的构建模式。
    /// </summary>
    [MenuItem("EasyFramework/HotFix/同步 HybridCLR 构建模式", false, 120)]
    private static void SynchronizeForActiveConfiguration()
    {
        HotFixConfig config = AssetDatabase.LoadAssetAtPath<HotFixConfig>(HotFixConfigAssetPath);
        if (config == null)
        {
            throw new BuildFailedException($"未找到热更新配置资源：{HotFixConfigAssetPath}");
        }

        Synchronize(config);
    }

    /// <summary>
    /// 根据热更新开关增删 GameLogic 与 GameProto 的 HybridCLR 热更新程序集声明。
    /// </summary>
    public static void Synchronize(HotFixConfig config)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        AssemblyDefinitionAsset[] managedDefinitions = LoadManagedDefinitions();
        var hotUpdateDefinitions = new List<AssemblyDefinitionAsset>(
            HybridCLRSettings.Instance.hotUpdateAssemblyDefinitions ?? Array.Empty<AssemblyDefinitionAsset>());
        bool changed = config.EnableHotFix
            ? AddManagedDefinitions(hotUpdateDefinitions, managedDefinitions)
            : RemoveManagedDefinitions(hotUpdateDefinitions, managedDefinitions);
        if (!changed)
        {
            return;
        }

        HybridCLRSettings.Instance.hotUpdateAssemblyDefinitions = hotUpdateDefinitions.ToArray();
        HybridCLRSettings.Save();
        Debug.Log($"[HotFixBuildModeSynchronizer] 已同步为{(config.EnableHotFix ? "热更新" : "AOT 本地")}模式。");
    }

    /// <summary>
    /// 加载本地模式需要从 HybridCLR 热更新列表移除的程序集定义。
    /// </summary>
    private static AssemblyDefinitionAsset[] LoadManagedDefinitions()
    {
        return new[]
        {
            LoadAssemblyDefinition(GameLogicAssemblyDefinitionPath),
            LoadAssemblyDefinition(GameProtoAssemblyDefinitionPath)
        };
    }

    /// <summary>
    /// 按资源路径加载程序集定义，并在配置丢失时阻止构建。
    /// </summary>
    private static AssemblyDefinitionAsset LoadAssemblyDefinition(string assetPath)
    {
        AssemblyDefinitionAsset assemblyDefinition = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(assetPath);
        if (assemblyDefinition == null)
        {
            throw new BuildFailedException($"未找到程序集定义资源：{assetPath}");
        }

        return assemblyDefinition;
    }

    /// <summary>
    /// 将本地游戏程序集加入 HybridCLR 热更新程序集列表。
    /// </summary>
    private static bool AddManagedDefinitions(
        List<AssemblyDefinitionAsset> hotUpdateDefinitions,
        IEnumerable<AssemblyDefinitionAsset> managedDefinitions)
    {
        bool changed = false;
        foreach (AssemblyDefinitionAsset definition in managedDefinitions)
        {
            if (hotUpdateDefinitions.Contains(definition))
            {
                continue;
            }

            hotUpdateDefinitions.Add(definition);
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// 将本地游戏程序集从 HybridCLR 热更新程序集列表移除，使其随 Player 作为 AOT 代码编译。
    /// </summary>
    private static bool RemoveManagedDefinitions(
        List<AssemblyDefinitionAsset> hotUpdateDefinitions,
        IEnumerable<AssemblyDefinitionAsset> managedDefinitions)
    {
        bool changed = false;
        foreach (AssemblyDefinitionAsset definition in managedDefinitions)
        {
            while (hotUpdateDefinitions.Remove(definition))
            {
                changed = true;
            }
        }

        return changed;
    }
}

/// <summary>
/// 绘制热更新配置默认 Inspector，并在模式切换后同步 HybridCLR 构建配置。
/// </summary>
[CustomEditor(typeof(HotFixConfig))]
internal sealed class HotFixConfigInspector : Editor
{
    /// <summary>
    /// 绘制默认配置面板，并检测热更新开关变更。
    /// </summary>
    public override void OnInspectorGUI()
    {
        var config = (HotFixConfig)target;
        bool wasEnabled = config.EnableHotFix;
        DrawDefaultInspector();
        if (wasEnabled != config.EnableHotFix)
        {
            HotFixBuildModeSynchronizer.Synchronize(config);
        }
    }
}

using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 兼容微信小游戏转换 SDK 在 Unity 6 WebGL 构建时读取 Bee symbols 的目录差异。
/// </summary>
public sealed class WeChatMiniGameSymbolCompatibilityPostprocessor : IPostprocessBuildWithReport
{
    private const string WeChatMiniGameDefine = "WEIXINMINIGAME";
    private const string WeChatMiniGameLegacyDefine = "UNITY_WECHATMINIGAME";
    private const string BeeArtifactsDirectory = "Library/Bee/artifacts";
    private const string WebGlArtifactsDirectory = "WebGL/build";
    private const string WeChatMiniGameArtifactsDirectory = "WeixinMiniGame/build";
    private const string SymbolFileName = "build.js.symbols";
    private const string SymbolBuildDirectoryPattern = "debug_WebGL_wasm*";

    public int callbackOrder => 1000;

    /// <summary>
    /// WebGL 构建完成后，将 Unity 6 实际写入 WebGL 目录的 symbols 同步到 SDK 预期目录。
    /// </summary>
    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL || !IsWeChatMiniGameBuild())
        {
            return;
        }

        int synchronizedCount = SynchronizeSymbolArtifacts();
        if (synchronizedCount > 0)
        {
            Debug.Log($"[WeChatMiniGame] 已同步 {synchronizedCount} 个 Unity 6 Bee symbols 中间文件。");
        }
    }

    /// <summary>
    /// 判断当前 WebGL 构建是否启用了微信小游戏转换流程。
    /// </summary>
    private static bool IsWeChatMiniGameBuild()
    {
        string defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.WebGL);
        foreach (string define in defines.Split(';'))
        {
            string trimmedDefine = define.Trim();
            if (string.Equals(trimmedDefine, WeChatMiniGameDefine, StringComparison.Ordinal) ||
                string.Equals(trimmedDefine, WeChatMiniGameLegacyDefine, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 将 WebGL Bee 产物同步到微信 SDK 固定读取的目录，供转换器后续预处理 symbols。
    /// </summary>
    private static int SynchronizeSymbolArtifacts()
    {
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        if (string.IsNullOrEmpty(projectRoot))
        {
            return 0;
        }

        string sourceRoot = Path.Combine(projectRoot, BeeArtifactsDirectory, WebGlArtifactsDirectory);
        if (!Directory.Exists(sourceRoot))
        {
            return 0;
        }

        string destinationRoot = Path.Combine(projectRoot, BeeArtifactsDirectory, WeChatMiniGameArtifactsDirectory);
        int synchronizedCount = 0;
        foreach (string sourceDirectory in Directory.GetDirectories(sourceRoot, SymbolBuildDirectoryPattern))
        {
            string sourcePath = Path.Combine(sourceDirectory, SymbolFileName);
            if (!File.Exists(sourcePath))
            {
                continue;
            }

            string buildDirectoryName = Path.GetFileName(sourceDirectory);
            string destinationDirectory = Path.Combine(destinationRoot, buildDirectoryName);
            Directory.CreateDirectory(destinationDirectory);
            File.Copy(sourcePath, Path.Combine(destinationDirectory, SymbolFileName), true);
            synchronizedCount++;
        }

        return synchronizedCount;
    }
}

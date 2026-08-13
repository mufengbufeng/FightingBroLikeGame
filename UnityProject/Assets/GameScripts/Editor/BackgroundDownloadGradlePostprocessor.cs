#if UNITY_ANDROID
using System;
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

/// <summary>
/// 为旧版 Unity BackgroundDownload Android Library 补充 AGP 8 要求的 namespace。
/// </summary>
internal sealed class BackgroundDownloadGradlePostprocessor : IPostGenerateGradleAndroidProject
{
    private const int CallbackOrder = 1000;
    private const string AndroidBlock = "android {";
    private const string NamespaceDeclaration = "    namespace \"com.unity3d.backgrounddownload\"";
    private const string LegacyCompileSdk = "    compileSdkVersion 28";
    private const string UnityCompileSdk =
        "    compileSdk project.property(\"unity.compileSdkVersion\").toInteger()";
    private const string UnityBuildTools =
        "    buildToolsVersion project.property(\"unity.buildToolsVersion\")";
    private const string RelativeGradlePath = "backgrounddownload.androidlib/build.gradle";
    private const string RelativeManifestPath =
        "backgrounddownload.androidlib/src/main/AndroidManifest.xml";
    private const string AndroidXmlNamespace = "http://schemas.android.com/apk/res/android";
    private const string CompletionReceiver =
        "com.unity3d.backgrounddownload.CompletionReceiver";

    public int callbackOrder => CallbackOrder;

    /// <summary>
    /// 在 Unity 生成的子模块 Gradle 文件中按需插入 namespace。
    /// </summary>
    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string gradlePath = Path.Combine(path, RelativeGradlePath);
        if (File.Exists(gradlePath))
        {
            PatchGradleFile(gradlePath);
        }

        string manifestPath = Path.Combine(path, RelativeManifestPath);
        if (File.Exists(manifestPath))
        {
            PatchManifestFile(manifestPath);
        }
    }

    private static void PatchGradleFile(string gradlePath)
    {
        string contents = File.ReadAllText(gradlePath);
        int androidBlockIndex = contents.IndexOf(AndroidBlock, StringComparison.Ordinal);
        if (androidBlockIndex < 0)
        {
            Debug.LogWarning($"[AndroidBuild] 未找到 BackgroundDownload android 块：{gradlePath}");
            return;
        }

        string lineEnding = contents.Contains("\r\n") ? "\r\n" : "\n";
        if (contents.IndexOf("namespace ", StringComparison.Ordinal) < 0)
        {
            int insertIndex = androidBlockIndex + AndroidBlock.Length;
            contents = contents.Insert(insertIndex, lineEnding + NamespaceDeclaration);
        }

        if (contents.IndexOf(LegacyCompileSdk, StringComparison.Ordinal) >= 0)
        {
            contents = contents.Replace(
                LegacyCompileSdk,
                UnityCompileSdk + lineEnding + UnityBuildTools);
        }

        File.WriteAllText(gradlePath, contents);
        Debug.Log($"[AndroidBuild] 已修复 BackgroundDownload 的 AGP 8 配置：{gradlePath}");
    }

    private static void PatchManifestFile(string manifestPath)
    {
        var document = new XmlDocument { PreserveWhitespace = true };
        document.Load(manifestPath);

        foreach (XmlNode receiver in document.GetElementsByTagName("receiver"))
        {
            if (receiver.Attributes?["name", AndroidXmlNamespace]?.Value != CompletionReceiver)
            {
                continue;
            }

            var exported = document.CreateAttribute("android", "exported", AndroidXmlNamespace);
            exported.Value = "false";
            receiver.Attributes.SetNamedItem(exported);
            document.Save(manifestPath);
            Debug.Log($"[AndroidBuild] 已为 BackgroundDownload Receiver 设置 exported=false：{manifestPath}");
            return;
        }

        Debug.LogWarning($"[AndroidBuild] 未找到 BackgroundDownload CompletionReceiver：{manifestPath}");
    }
}
#endif

using System;
using System.IO;
using LitJson;
using UnityEngine;
using WeChatWASM;

/// <summary>
/// Keeps the generated mini-game compatible with DevTools isolated contexts.
/// This runs after the SDK has generated every minigame runtime file.
/// </summary>
public sealed class WeChatDevtoolsCompatibilityTemplate : LifeCycleBase
{
    private const string CryptoPolyfillFileName = "crypto-polyfill.js";
    private const string CryptoPolyfillImport = "import './crypto-polyfill';";
    private const string FrameworkImport = "import './webgl.wasm.framework.unityweb';";
    private const string CryptoPolyfillSource = "// @ts-nocheck\n"
        + "// Math.random 回退仅用于避免 Unity 启动失败，不具备密码学安全性。\n"
        + "const cryptoHost = typeof GameGlobal !== 'undefined' ? GameGlobal : globalThis;\n"
        + "const existingCrypto = cryptoHost.crypto;\n"
        + "const cryptoObject = existingCrypto && (typeof existingCrypto === 'object' || typeof existingCrypto === 'function')\n"
        + "    ? existingCrypto\n"
        + "    : {};\n"
        + "if (typeof cryptoObject.getRandomValues !== 'function') {\n"
        + "    cryptoObject.getRandomValues = function (array) {\n"
        + "        const bytes = new Uint8Array(array.buffer, array.byteOffset, array.byteLength);\n"
        + "        for (let index = 0; index < bytes.length; index += 1) {\n"
        + "            bytes[index] = (Math.random() * 256) | 0;\n"
        + "        }\n"
        + "        return array;\n"
        + "    };\n"
        + "}\n"
        + "cryptoHost.crypto = cryptoObject;\n"
        + "if (typeof globalThis !== 'undefined' && globalThis !== cryptoHost) {\n"
        + "    globalThis.crypto = cryptoObject;\n"
        + "}\n";

    public override void exportDone()
    {
        string minigameDirectory = BuildTemplateHelper.DstMinigameDir;
        if (string.IsNullOrWhiteSpace(minigameDirectory))
        {
            minigameDirectory = Path.Combine(UnityUtil.GetEditorConf().ProjectConf.DST, "minigame");
        }

        if (string.IsNullOrWhiteSpace(minigameDirectory) || !Directory.Exists(minigameDirectory))
        {
            throw new InvalidOperationException("WeChat minigame export directory is unavailable.");
        }

        PatchGameJson(Path.Combine(minigameDirectory, "game.json"));
        PatchCryptoRandomValues(minigameDirectory);
        PatchTimerFix(Path.Combine(minigameDirectory, "unity-sdk", "fix.js"));
        PatchSdkIndex(Path.Combine(minigameDirectory, "unity-sdk", "index.js"));
        PatchAdapter(Path.Combine(minigameDirectory, "weapp-adapter.js"));
        PatchGameBootstrap(Path.Combine(minigameDirectory, "game.js"));

        Debug.Log("[WeChatExportCompatibility] Applied generated minigame compatibility patches.");
    }

    // 某些微信小游戏运行环境不提供 Web Crypto，避免 Unity Emscripten 在启动阶段中止。
    private static void PatchCryptoRandomValues(string minigameDirectory)
    {
        string path = Path.Combine(minigameDirectory, CryptoPolyfillFileName);
        string original = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        WriteIfChanged(path, original, CryptoPolyfillSource);
    }

    private static void PatchGameJson(string path)
    {
        string source = ReadRequired(path);
        JsonData root = JsonMapper.ToObject(source);
        if (!root.IsObject || !root.ContainsKey("subpackages"))
        {
            return;
        }

        JsonData subpackages = root["subpackages"];
        if (!subpackages.IsArray)
        {
            throw new InvalidOperationException("WeChat game.json subpackages must be an array.");
        }

        bool changed = false;
        for (int index = 0; index < subpackages.Count; index++)
        {
            JsonData subpackage = subpackages[index];
            if (!subpackage.IsObject)
            {
                throw new InvalidOperationException("WeChat game.json contains an invalid subpackage entry.");
            }

            if (subpackage.ContainsKey("pages"))
            {
                continue;
            }

            JsonData pages = new JsonData();
            pages.SetJsonType(JsonType.Array);
            subpackage["pages"] = pages;
            changed = true;
        }

        if (changed)
        {
            File.WriteAllText(path, root.ToJson());
        }
    }

    private static void PatchTimerFix(string path)
    {
        string original = ReadRequired(path);
        string source = NormalizeLineEndings(original);
        if (source.Contains("const timerTarget ="))
        {
            return;
        }

        source = ReplaceRequired(
            source,
            "const wm = {};",
            "const wm = {};\n"
            + "        const timerTarget = typeof window !== 'undefined' && window ? window : globalThis;\n"
            + "        const timerHost = typeof timerTarget.setTimeout === 'function' ? timerTarget : globalThis;\n"
            + "        if (typeof timerHost.setTimeout !== 'function'\n"
            + "            || typeof timerHost.clearTimeout !== 'function'\n"
            + "            || typeof timerHost.setInterval !== 'function'\n"
            + "            || typeof timerHost.clearInterval !== 'function'\n"
            + "            || typeof timerHost.requestAnimationFrame !== 'function'\n"
            + "            || typeof timerHost.cancelAnimationFrame !== 'function') {\n"
            + "            return;\n"
            + "        }",
            path);
        source = ReplaceRequired(source, "const privateSetTimeout = window.setTimeout;", "const privateSetTimeout = timerHost.setTimeout.bind(timerHost);", path);
        source = ReplaceRequired(source, "const privateClearTimeout = window.clearTimeout;", "const privateClearTimeout = timerHost.clearTimeout.bind(timerHost);", path);
        source = ReplaceRequired(source, "const privateSetInterval = window.setInterval;", "const privateSetInterval = timerHost.setInterval.bind(timerHost);", path);
        source = ReplaceRequired(source, "const privateClearInterval = window.clearInterval;", "const privateClearInterval = timerHost.clearInterval.bind(timerHost);", path);
        source = ReplaceRequired(source, "const privateRequestAnimationFrame = window.requestAnimationFrame;", "const privateRequestAnimationFrame = timerHost.requestAnimationFrame.bind(timerHost);", path);
        source = ReplaceRequired(source, "const privateCancelAnimationFrame = window.cancelAnimationFrame;", "const privateCancelAnimationFrame = timerHost.cancelAnimationFrame.bind(timerHost);", path);
        source = ReplaceRequired(source, "window.setTimeout", "timerTarget.setTimeout", path);
        source = ReplaceRequired(source, "window.clearTimeout", "timerTarget.clearTimeout", path);
        source = ReplaceRequired(source, "window.setInterval", "timerTarget.setInterval", path);
        source = ReplaceRequired(source, "window.clearInterval", "timerTarget.clearInterval", path);
        source = ReplaceRequired(source, "window.requestAnimationFrame", "timerTarget.requestAnimationFrame", path);
        source = ReplaceRequired(source, "window.cancelAnimationFrame", "timerTarget.cancelAnimationFrame", path);
        WriteIfChanged(path, original, source);
    }

    private static void PatchSdkIndex(string path)
    {
        string original = ReadRequired(path);
        string source = NormalizeLineEndings(original);
        if (source.Contains("const window = GameGlobal;"))
        {
            return;
        }

        const string imports = "import storage from './storage';";
        source = ReplaceRequired(
            source,
            imports,
            "const window = GameGlobal;\n"
            + "const document = GameGlobal.document;\n"
            + imports,
            path);
        WriteIfChanged(path, original, source);
    }

    private static void PatchAdapter(string path)
    {
        string original = ReadRequired(path);
        string source = NormalizeLineEndings(original);
        if (source.Contains("Object.defineProperty(global, key,"))
        {
            return;
        }

        source = ReplaceRequired(source, "Object.defineProperty(window, key,", "Object.defineProperty(global, key,", path);

        source = ReplaceRequired(
            source,
            "\n                window.parent = window;\n",
            "\n                global.parent = global;\n",
            path);
        WriteIfChanged(path, original, source);
    }

    /// <summary>
    /// 仅注入 Web Crypto 兼容层，保持 UnityPlugin 使用 SDK 默认的隔离上下文环境。
    /// </summary>
    private static void PatchGameBootstrap(string path)
    {
        string original = ReadRequired(path);
        string source = NormalizeLineEndings(original);

        if (!source.Contains(CryptoPolyfillImport))
        {
            source = ReplaceRequired(
                source,
                FrameworkImport,
                CryptoPolyfillImport + "\n" + FrameworkImport,
                path);
        }

        WriteIfChanged(path, original, source);
    }

    private static string ReadRequired(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Expected WeChat export file is missing.", path);
        }

        return File.ReadAllText(path);
    }

    private static void WriteIfChanged(string path, string original, string updated)
    {
        if (!string.Equals(original, updated, StringComparison.Ordinal))
        {
            File.WriteAllText(path, updated);
        }
    }

    private static string NormalizeLineEndings(string source)
    {
        return source.Replace("\r\n", "\n");
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string path)
    {
        int occurrences = CountOccurrences(source, oldValue);
        if (occurrences != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one compatibility anchor in {Path.GetFileName(path)}, found {occurrences}.");
        }

        return source.Replace(oldValue, newValue);
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}

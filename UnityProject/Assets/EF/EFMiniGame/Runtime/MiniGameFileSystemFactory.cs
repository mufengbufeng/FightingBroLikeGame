using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using YooAsset;

namespace EF.MiniGame
{
    /// <summary>
    /// 创建微信或抖音小游戏使用的 YooAsset Web 文件系统参数。
    /// </summary>
    public static class MiniGameFileSystemFactory
    {
        /// <summary>
        /// 创建微信小游戏文件系统参数。
        /// </summary>
        public static FileSystemParameters CreateWechat(
            IRemoteService remoteService,
            bool disableUnityWebCache = true)
        {
            return Create(
                remoteService,
                MiniGameWebPlatformStrategy.CreateWechat(),
                WechatMiniGameFileSystem.FileSystemTypeName,
                WechatMiniGameSdk.GetDefaultCacheRoot(),
                disableUnityWebCache);
        }

        /// <summary>
        /// 创建微信小游戏包体内置资源使用的 WebServer 文件系统参数。
        /// 该文件系统没有远端服务，只会从小游戏包内路径读取版本、清单和资源包。
        /// </summary>
        public static FileSystemParameters CreateWechatBuiltinWebServer(bool disableUnityWebCache)
        {
            FileSystemParameters parameters =
                FileSystemParameters.CreateDefaultWebServerFileSystemParameters(disableUnityWebCache);
            parameters.AddParameter(
                EFileSystemParameter.WebPlatformStrategy,
                MiniGameWebPlatformStrategy.CreateWechat());
            return parameters;
        }

        /// <summary>
        /// 创建抖音小游戏文件系统参数。
        /// </summary>
        public static FileSystemParameters CreateTiktok(
            IRemoteService remoteService,
            bool disableUnityWebCache = true)
        {
            return Create(remoteService, MiniGameWebPlatformStrategy.CreateTiktok(), disableUnityWebCache: disableUnityWebCache);
        }

        /// <summary>
        /// 创建禁用 Unity Web Cache 并注入平台策略的网络文件系统参数。
        /// </summary>
        private static FileSystemParameters Create(
            IRemoteService remoteService,
            IWebPlatformStrategy strategy,
            string fileSystemTypeName = null,
            string packageRoot = null,
            bool disableUnityWebCache = true)
        {
            FileSystemParameters parameters = string.IsNullOrEmpty(fileSystemTypeName)
                ? FileSystemParameters.CreateDefaultWebNetworkFileSystemParameters(remoteService, disableUnityWebCache)
                : new FileSystemParameters(fileSystemTypeName, packageRoot);
            if (!string.IsNullOrEmpty(fileSystemTypeName))
            {
                parameters.AddParameter(EFileSystemParameter.RemoteService, remoteService);
                parameters.AddParameter(EFileSystemParameter.DisableUnityWebCache, disableUnityWebCache);
            }
            parameters.AddParameter(EFileSystemParameter.WebPlatformStrategy, strategy);
            return parameters;
        }
    }

    /// <summary>
    /// 通过平台 SDK 的公开 API 为 YooAsset 提供小游戏 AssetBundle 请求策略。
    /// </summary>
    internal sealed class MiniGameWebPlatformStrategy : IWebPlatformStrategy
    {
        private readonly string _assetBundleTypeName;
        private readonly string _unloadMethodName;
        private MethodInfo _getAssetBundleMethod;
        private MethodInfo _unloadMethod;

        /// <summary>
        /// 创建指定 SDK 类型和卸载扩展方法的反射策略。
        /// </summary>
        private MiniGameWebPlatformStrategy(string assetBundleTypeName, string unloadMethodName)
        {
            _assetBundleTypeName = assetBundleTypeName;
            _unloadMethodName = unloadMethodName;
        }

        /// <summary>
        /// 创建微信小游戏请求策略。
        /// </summary>
        public static MiniGameWebPlatformStrategy CreateWechat()
        {
            var strategy = new MiniGameWebPlatformStrategy("WeChatWASM.WXAssetBundle", "WXUnload");
            strategy.ValidateSdk();
            return strategy;
        }

        /// <summary>
        /// 创建抖音小游戏请求策略。
        /// </summary>
        public static MiniGameWebPlatformStrategy CreateTiktok()
        {
            var strategy = new MiniGameWebPlatformStrategy("TTSDK.TTAssetBundle", "TTUnload");
            strategy.ValidateSdk();
            return strategy;
        }

        /// <summary>
        /// 在资源包初始化阶段确认小游戏 SDK 的资源请求与卸载入口均已加载。
        /// </summary>
        private void ValidateSdk()
        {
            _getAssetBundleMethod = ResolveGetAssetBundleMethod();
            _unloadMethod = ResolveUnloadMethod();
        }

        /// <inheritdoc />
        public UnityWebRequest CreateAssetBundleRequest(WebAssetBundleRequestArgs args)
        {
            _getAssetBundleMethod ??= ResolveGetAssetBundleMethod();
            object result = _getAssetBundleMethod.Invoke(null, new object[] { args.Url });
            if (!(result is UnityWebRequest request))
            {
                throw new InvalidOperationException(
                    $"{_assetBundleTypeName}.GetAssetBundle 未返回 UnityWebRequest，请检查小游戏 SDK 版本");
            }

            request.disposeDownloadHandlerOnDispose = true;
            return request;
        }

        /// <inheritdoc />
        public AssetBundle ExtractAssetBundle(UnityWebRequest request)
        {
            if (request == null || request.downloadHandler == null)
            {
                throw new InvalidOperationException("小游戏 AssetBundle 请求没有可用的 DownloadHandler");
            }

            PropertyInfo assetBundleProperty = request.downloadHandler.GetType().GetProperty(
                "assetBundle",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (assetBundleProperty == null || !(assetBundleProperty.GetValue(request.downloadHandler) is AssetBundle bundle))
            {
                throw new InvalidOperationException("小游戏 DownloadHandler 未暴露 assetBundle，请检查小游戏 SDK 版本");
            }

            return bundle;
        }

        /// <inheritdoc />
        public void UnloadAssetBundle(AssetBundle assetBundle, bool unloadAll)
        {
            if (assetBundle == null)
            {
                return;
            }

            _unloadMethod ??= ResolveUnloadMethod();
            _unloadMethod.Invoke(null, new object[] { assetBundle, unloadAll });
        }

        /// <summary>
        /// 解析平台 SDK 的静态 GetAssetBundle(string) 方法。
        /// </summary>
        private MethodInfo ResolveGetAssetBundleMethod()
        {
            Type assetBundleType = FindType(_assetBundleTypeName);
            MethodInfo method = assetBundleType != null
                ? assetBundleType.GetMethod(
                    "GetAssetBundle",
                    BindingFlags.Static | BindingFlags.Public,
                    null,
                    new[] { typeof(string) },
                    null)
                : null;
            if (method == null)
            {
                throw new InvalidOperationException(
                    $"未找到 {_assetBundleTypeName}.GetAssetBundle(string)，请先安装对应小游戏 SDK");
            }

            return method;
        }

        /// <summary>
        /// 解析平台 SDK 的 AssetBundle 卸载扩展方法。
        /// </summary>
        private MethodInfo ResolveUnloadMethod()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in GetLoadableTypes(assembly))
                {
                    MethodInfo method = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                        .FirstOrDefault(candidate => IsUnloadMethod(candidate, _unloadMethodName));
                    if (method != null)
                    {
                        return method;
                    }
                }
            }

            throw new InvalidOperationException(
                $"未找到小游戏 SDK 的 {_unloadMethodName}(AssetBundle, bool)，请检查插件版本");
        }

        /// <summary>
        /// 判断候选方法是否匹配平台 AssetBundle 卸载签名。
        /// </summary>
        private static bool IsUnloadMethod(MethodInfo method, string methodName)
        {
            if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
            {
                return false;
            }

            ParameterInfo[] parameters = method.GetParameters();
            return parameters.Length == 2 &&
                   parameters[0].ParameterType == typeof(AssetBundle) &&
                   parameters[1].ParameterType == typeof(bool);
        }

        /// <summary>
        /// 在当前 AppDomain 已加载程序集内查找完整类型名。
        /// </summary>
        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        /// <summary>
        /// 返回程序集可加载类型，并容忍部分类型加载失败。
        /// </summary>
        private static Type[] GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null).ToArray();
            }
        }
    }
}

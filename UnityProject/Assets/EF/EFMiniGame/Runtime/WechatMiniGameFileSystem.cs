using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using YooAsset;

namespace EF.MiniGame
{
    /// <summary>
    /// 微信小游戏的 YooAsset 文件系统，补齐微信 SDK 缓存清理能力。
    /// SDK 类型通过反射访问，避免普通 Unity 构建依赖微信插件程序集。
    /// </summary>
    internal sealed class WechatMiniGameFileSystem : WebNetworkFileSystem
    {
        private string _cacheRoot;

        /// <summary>
        /// 供 FileSystemParameters 反射创建实例使用的完整类型名。
        /// </summary>
        internal static string FileSystemTypeName =>
            typeof(WechatMiniGameFileSystem).FullName + ", " + typeof(WechatMiniGameFileSystem).Assembly.GetName().Name;

        /// <inheritdoc />
        public override FSClearCacheOperation ClearCacheAsync(FSClearCacheOptions options)
        {
            if (options.ClearMethod == ClearCacheMethods.ClearAllBundleFiles)
            {
                return new ClearWechatAllBundleFilesOperation();
            }

            if (options.ClearMethod == ClearCacheMethods.ClearUnusedBundleFiles)
            {
                return new ClearWechatUnusedBundleFilesOperation(_cacheRoot, options.Manifest);
            }

            return new FSClearCacheCompleteOperation("不支持的微信小游戏缓存清理方式：" + options.ClearMethod);
        }

        /// <inheritdoc />
        public override void OnCreate(string packageName, string packageRoot)
        {
            _cacheRoot = packageRoot;
            base.OnCreate(packageName, packageRoot);
        }
    }

    /// <summary>
    /// 调用微信 SDK 清理全部文件缓存的异步操作。
    /// </summary>
    internal sealed class ClearWechatAllBundleFilesOperation : FSClearCacheOperation
    {
        private bool _started;

        /// <inheritdoc />
        protected override void InternalStart()
        {
        }

        /// <inheritdoc />
        protected override void InternalUpdate()
        {
            if (_started)
            {
                return;
            }

            _started = true;
            try
            {
                WechatMiniGameSdk.CleanAllFileCache(OnCompleted);
            }
            catch (Exception exception)
            {
                SetError("调用微信缓存清理失败：" + exception.Message);
            }
        }

        /// <summary>
        /// 接收微信 SDK 的缓存清理结果。
        /// </summary>
        private void OnCompleted(bool succeeded)
        {
            if (succeeded)
            {
                SetResult();
            }
            else
            {
                SetError("微信缓存清理失败");
            }
        }
    }

    /// <summary>
    /// 根据当前资源清单删除微信缓存中的无用 Bundle 文件。
    /// </summary>
    internal sealed class ClearWechatUnusedBundleFilesOperation : FSClearCacheOperation
    {
        private readonly string _cacheRoot;
        private readonly PackageManifest _manifest;
        private readonly List<string> _unusedFiles = new();

        private bool _statRequested;
        private bool _statCompleted;
        private string _statError;
        private int _totalCount;

        /// <summary>
        /// 创建无用缓存清理操作。
        /// </summary>
        public ClearWechatUnusedBundleFilesOperation(string cacheRoot, PackageManifest manifest)
        {
            _cacheRoot = cacheRoot;
            _manifest = manifest;
        }

        /// <inheritdoc />
        protected override void InternalStart()
        {
        }

        /// <inheritdoc />
        protected override void InternalUpdate()
        {
            if (!_statRequested)
            {
                _statRequested = true;
                try
                {
                    WechatMiniGameSdk.StatRecursively(_cacheRoot, OnStatCompleted, OnStatFailed);
                }
                catch (Exception exception)
                {
                    SetError("读取微信缓存目录失败：" + exception.Message);
                }

                return;
            }

            if (!_statCompleted)
            {
                return;
            }

            if (!string.IsNullOrEmpty(_statError))
            {
                SetError(_statError);
                return;
            }

            if (_totalCount == 0)
            {
                _totalCount = _unusedFiles.Count;
            }

            if (_unusedFiles.Count > 0)
            {
                int index = _unusedFiles.Count - 1;
                string filePath = _unusedFiles[index];
                _unusedFiles.RemoveAt(index);
                try
                {
                    WechatMiniGameSdk.RemoveFile(filePath);
                }
                catch (Exception exception)
                {
                    SetError("删除微信缓存文件失败：" + exception.Message);
                    return;
                }

                if (IsBusy)
                {
                    return;
                }
            }

            Progress = _totalCount == 0 ? 1f : 1f - (float)_unusedFiles.Count / _totalCount;
            if (_unusedFiles.Count == 0)
            {
                SetResult();
            }
        }

        /// <summary>
        /// 筛选当前清单不再引用的缓存 Bundle。
        /// </summary>
        private void OnStatCompleted(IReadOnlyList<string> filePaths)
        {
            foreach (string filePath in filePaths)
            {
                if (string.IsNullOrEmpty(filePath) || IsManifestFile(filePath))
                {
                    continue;
                }

                string bundleGuid = Path.GetFileNameWithoutExtension(filePath);
                int separatorIndex = bundleGuid.LastIndexOf('_');
                if (separatorIndex >= 0 && separatorIndex + 1 < bundleGuid.Length)
                {
                    bundleGuid = bundleGuid.Substring(separatorIndex + 1);
                }

                if (_manifest == null || !_manifest.TryGetPackageBundleByBundleGuid(bundleGuid, out _))
                {
                    _unusedFiles.Add(CombineCachePath(_cacheRoot, filePath));
                }
            }

            _statCompleted = true;
        }

        /// <summary>
        /// 记录微信 SDK 返回的目录遍历错误。
        /// </summary>
        private void OnStatFailed(string error)
        {
            _statError = string.IsNullOrEmpty(error) ? "微信缓存目录遍历失败" : error;
            _statCompleted = true;
        }

        /// <summary>
        /// 微信缓存中的 bytes 和 hash 文件属于资源清单，不能作为 Bundle 清理。
        /// </summary>
        private static bool IsManifestFile(string filePath)
        {
            string extension = Path.GetExtension(filePath);
            return string.IsNullOrEmpty(extension) ||
                   string.Equals(extension, ".bytes", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".hash", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 将 SDK 返回的相对路径转换为微信缓存根目录下的绝对路径。
        /// </summary>
        private static string CombineCachePath(string cacheRoot, string filePath)
        {
            return (cacheRoot ?? string.Empty).TrimEnd('/', '\\') + "/" + filePath.TrimStart('/', '\\');
        }
    }

    /// <summary>
    /// 微信小游戏 SDK 的反射桥接，集中处理可选插件 API。
    /// </summary>
    internal static class WechatMiniGameSdk
    {
        private const string WxTypeName = "WeChatWASM.WX";
        private const string StatOptionTypeName = "WeChatWASM.WXStatOption";
        // WX 将部分静态 API 定义在 WXBase，反射 WX 时必须展开继承层级。
        private const BindingFlags PublicMemberBindingFlags =
            BindingFlags.Static |
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.FlattenHierarchy;

        /// <summary>
        /// 根据微信 SDK 用户目录返回 YooAsset 的默认缓存根目录。
        /// </summary>
        public static string GetDefaultCacheRoot()
        {
            Type wxType = GetRequiredType(WxTypeName);
            object environment = GetMemberValue(wxType, null, "env");
            string userDataPath = GetMemberValue(environment.GetType(), environment, "USER_DATA_PATH") as string;
            if (string.IsNullOrWhiteSpace(userDataPath))
            {
                throw new InvalidOperationException("微信小游戏 SDK 未提供 WX.env.USER_DATA_PATH");
            }

            return userDataPath.TrimEnd('/', '\\') + "/__GAME_FILE_CACHE/yoo";
        }

        /// <summary>
        /// 调用微信 SDK 清理全部文件缓存。
        /// </summary>
        public static void CleanAllFileCache(Action<bool> completed)
        {
            Type wxType = GetRequiredType(WxTypeName);
            MethodInfo method = FindMethod(wxType, "CleanAllFileCache", 1);
            ParameterInfo parameter = method.GetParameters()[0];
            Delegate callback = CreateDelegate(parameter.ParameterType, completed);
            method.Invoke(null, new object[] { callback });
        }

        /// <summary>
        /// 递归返回微信 SDK 管理的缓存文件路径。
        /// </summary>
        public static void StatRecursively(
            string cacheRoot,
            Action<IReadOnlyList<string>> succeeded,
            Action<string> failed)
        {
            Type wxType = GetRequiredType(WxTypeName);
            Type optionType = GetRequiredType(StatOptionTypeName);
            object option = Activator.CreateInstance(optionType);
            SetMemberValue(optionType, option, "path", cacheRoot);
            SetMemberValue(optionType, option, "recursive", true);

            var callbacks = new StatCallbacks(succeeded, failed);
            SetMemberValue(optionType, option, "success", CreateDelegate(
                GetMemberType(optionType, "success"), new Action<object>(callbacks.OnSuccess)));
            SetMemberValue(optionType, option, "fail", CreateDelegate(
                GetMemberType(optionType, "fail"), new Action<object>(callbacks.OnFailed)));

            MethodInfo getFileSystemManager = FindMethod(wxType, "GetFileSystemManager", 0);
            object fileSystemManager = getFileSystemManager.Invoke(null, null);
            if (fileSystemManager == null)
            {
                throw new InvalidOperationException("微信小游戏 SDK 未返回 FileSystemManager");
            }

            MethodInfo statMethod = FindMethod(fileSystemManager.GetType(), "Stat", 1);
            statMethod.Invoke(fileSystemManager, new[] { option });
        }

        /// <summary>
        /// 请求微信 SDK 删除单个缓存文件。
        /// </summary>
        public static void RemoveFile(string filePath)
        {
            Type wxType = GetRequiredType(WxTypeName);
            MethodInfo method = FindMethod(wxType, "RemoveFile", 2);
            method.Invoke(null, new object[] { filePath, null });
        }

        /// <summary>
        /// 将 SDK stat 回调适配为纯路径和错误文本。
        /// </summary>
        private sealed class StatCallbacks
        {
            private readonly Action<IReadOnlyList<string>> _succeeded;
            private readonly Action<string> _failed;

            public StatCallbacks(Action<IReadOnlyList<string>> succeeded, Action<string> failed)
            {
                _succeeded = succeeded;
                _failed = failed;
            }

            public void OnSuccess(object response)
            {
                object stats = GetMemberValue(response.GetType(), response, "stats");
                var filePaths = new List<string>();
                if (stats is IEnumerable enumerable)
                {
                    foreach (object stat in enumerable)
                    {
                        if (stat == null)
                        {
                            continue;
                        }

                        string path = GetMemberValue(stat.GetType(), stat, "path") as string;
                        if (!string.IsNullOrEmpty(path))
                        {
                            filePaths.Add(path);
                        }
                    }
                }

                _succeeded(filePaths);
            }

            public void OnFailed(object response)
            {
                string error = response != null
                    ? GetMemberValue(response.GetType(), response, "errMsg") as string
                    : null;
                _failed(error);
            }
        }

        /// <summary>
        /// 查找微信 SDK 类型，缺失时给出明确的接入提示。
        /// </summary>
        private static Type GetRequiredType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            throw new InvalidOperationException("未找到微信小游戏 SDK 类型 " + fullName + "，请先安装 WX-WASM-SDK-V2");
        }

        /// <summary>
        /// 按名称和参数数量查找公开 SDK 方法。
        /// </summary>
        private static MethodInfo FindMethod(Type type, string methodName, int parameterCount)
        {
            foreach (MethodInfo method in type.GetMethods(PublicMemberBindingFlags))
            {
                if (method.Name == methodName && method.GetParameters().Length == parameterCount)
                {
                    return method;
                }
            }

            throw new InvalidOperationException("微信小游戏 SDK 未提供方法 " + type.FullName + "." + methodName);
        }

        /// <summary>
        /// 读取 SDK 类型公开字段或属性的值。
        /// </summary>
        private static object GetMemberValue(Type type, object target, string memberName)
        {
            PropertyInfo property = type.GetProperty(memberName, PublicMemberBindingFlags);
            if (property != null)
            {
                return property.GetValue(target);
            }

            FieldInfo field = type.GetField(memberName, PublicMemberBindingFlags);
            if (field != null)
            {
                return field.GetValue(target);
            }

            throw new InvalidOperationException("微信小游戏 SDK 未提供成员 " + type.FullName + "." + memberName);
        }

        /// <summary>
        /// 写入 SDK 选项对象的公开字段或属性。
        /// </summary>
        private static void SetMemberValue(Type type, object target, string memberName, object value)
        {
            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (property != null)
            {
                property.SetValue(target, value);
                return;
            }

            FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }

            throw new InvalidOperationException("微信小游戏 SDK 未提供成员 " + type.FullName + "." + memberName);
        }

        /// <summary>
        /// 获取 SDK 回调字段或属性的委托类型。
        /// </summary>
        private static Type GetMemberType(Type type, string memberName)
        {
            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (property != null)
            {
                return property.PropertyType;
            }

            FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (field != null)
            {
                return field.FieldType;
            }

            throw new InvalidOperationException("微信小游戏 SDK 未提供成员 " + type.FullName + "." + memberName);
        }

        /// <summary>
        /// 创建能被 SDK 回调字段接收的委托实例。
        /// </summary>
        private static Delegate CreateDelegate(Type delegateType, Delegate callback)
        {
            return callback.Target == null
                ? Delegate.CreateDelegate(delegateType, callback.Method)
                : Delegate.CreateDelegate(delegateType, callback.Target, callback.Method);
        }
    }
}

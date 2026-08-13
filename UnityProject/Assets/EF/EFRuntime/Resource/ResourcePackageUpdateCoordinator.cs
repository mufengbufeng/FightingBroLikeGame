using System;
using Cysharp.Threading.Tasks;

namespace EF.Resource
{
    /// <summary>
    /// 将资源更新流程与 YooAsset 具体操作隔离，便于验证弱联网回退顺序。
    /// </summary>
    internal interface IResourcePackageUpdateOperations
    {
        /// <summary>
        /// 请求当前主文件系统中的资源版本。
        /// </summary>
        UniTask<ResourceOperationResult<string>> RequestPackageVersionAsync(int timeoutSeconds);

        /// <summary>
        /// 加载指定版本的资源清单。
        /// </summary>
        UniTask<ResourceOperationResult> LoadManifestAsync(string packageVersion, int timeoutSeconds);

        /// <summary>
        /// 下载当前资源清单缺少的所有文件。
        /// </summary>
        UniTask<ResourceOperationResult> DownloadAsync(int maximumConcurrency, int retryCount);

        /// <summary>
        /// 获取最近一次完整更新成功的版本。
        /// </summary>
        string GetCompletedVersion();

        /// <summary>
        /// 获取包体内置资源版本。
        /// </summary>
        UniTask<ResourceOperationResult<string>> GetBuiltinVersionAsync();

        /// <summary>
        /// 检查当前本地清单需要的资源是否完整。
        /// </summary>
        bool IsLocalContentComplete();

        /// <summary>
        /// 保存已经完整下载成功的版本。
        /// </summary>
        void SaveCompletedVersion(string packageVersion);
    }

    /// <summary>
    /// 可选的后台下载导入能力，用于在已加载清单后将系统下载文件写入 YooAsset 缓存。
    /// </summary>
    internal interface IResourcePackageBackgroundImportOperations
    {
        /// <summary>
        /// 导入当前活动清单中已经完成的后台下载文件。
        /// </summary>
        UniTask<ResourceOperationResult> ImportCompletedAsync();
    }

    /// <summary>
    /// 无返回值资源操作的统一结果。
    /// </summary>
    internal readonly struct ResourceOperationResult
    {
        /// <summary>
        /// 创建无返回值的资源操作结果。
        /// </summary>
        private ResourceOperationResult(bool succeeded, string error)
        {
            Succeeded = succeeded;
            Error = error ?? string.Empty;
        }

        public bool Succeeded { get; }

        public string Error { get; }

        /// <summary>
        /// 创建成功结果。
        /// </summary>
        public static ResourceOperationResult Success()
        {
            return new ResourceOperationResult(true, string.Empty);
        }

        /// <summary>
        /// 创建失败结果。
        /// </summary>
        public static ResourceOperationResult Failure(string error)
        {
            return new ResourceOperationResult(false, error);
        }
    }

    /// <summary>
    /// 带返回值资源操作的统一结果。
    /// </summary>
    internal readonly struct ResourceOperationResult<T>
    {
        /// <summary>
        /// 创建带返回值的资源操作结果。
        /// </summary>
        private ResourceOperationResult(bool succeeded, T value, string error)
        {
            Succeeded = succeeded;
            Value = value;
            Error = error ?? string.Empty;
        }

        public bool Succeeded { get; }

        public T Value { get; }

        public string Error { get; }

        /// <summary>
        /// 创建成功结果。
        /// </summary>
        public static ResourceOperationResult<T> Success(T value)
        {
            return new ResourceOperationResult<T>(true, value, string.Empty);
        }

        /// <summary>
        /// 创建失败结果。
        /// </summary>
        public static ResourceOperationResult<T> Failure(string error)
        {
            return new ResourceOperationResult<T>(false, default, error);
        }
    }

    /// <summary>
    /// 单个资源包更新后的最终状态。
    /// </summary>
    internal readonly struct ResourcePackageUpdateResult
    {
        /// <summary>
        /// 创建资源包更新最终结果。
        /// </summary>
        private ResourcePackageUpdateResult(
            bool succeeded,
            bool usedLocalFallback,
            string activeVersion,
            string remoteError,
            string error)
        {
            Succeeded = succeeded;
            UsedLocalFallback = usedLocalFallback;
            ActiveVersion = activeVersion ?? string.Empty;
            RemoteError = remoteError ?? string.Empty;
            Error = error ?? string.Empty;
        }

        public bool Succeeded { get; }

        public bool UsedLocalFallback { get; }

        public string ActiveVersion { get; }

        public string RemoteError { get; }

        public string Error { get; }

        /// <summary>
        /// 创建远端更新成功结果。
        /// </summary>
        public static ResourcePackageUpdateResult RemoteSucceeded(string activeVersion)
        {
            return new ResourcePackageUpdateResult(true, false, activeVersion, string.Empty, string.Empty);
        }

        /// <summary>
        /// 创建包体内置资源激活成功结果。
        /// </summary>
        public static ResourcePackageUpdateResult BuiltinSucceeded(string activeVersion)
        {
            return new ResourcePackageUpdateResult(true, false, activeVersion, string.Empty, string.Empty);
        }

        /// <summary>
        /// 创建本地回退成功结果。
        /// </summary>
        public static ResourcePackageUpdateResult LocalSucceeded(string activeVersion, string remoteError)
        {
            return new ResourcePackageUpdateResult(true, true, activeVersion, remoteError, string.Empty);
        }

        /// <summary>
        /// 创建更新失败结果。
        /// </summary>
        public static ResourcePackageUpdateResult Failed(string remoteError, string localError)
        {
            string error = string.IsNullOrEmpty(localError)
                ? remoteError
                : $"远端资源更新失败：{remoteError}；本地资源不可用：{localError}";
            return new ResourcePackageUpdateResult(false, false, string.Empty, remoteError, error);
        }

        /// <summary>
        /// 创建包体内置资源激活失败结果。
        /// </summary>
        public static ResourcePackageUpdateResult BuiltinFailed(string error)
        {
            return new ResourcePackageUpdateResult(false, false, string.Empty, string.Empty, error);
        }
    }

    /// <summary>
    /// 按“远端完整成功后记录版本，否则回退本地完整版本”的规则编排更新。
    /// </summary>
    internal static class ResourcePackageUpdateCoordinator
    {
        /// <summary>
        /// 执行远端更新，并在允许时尝试本地完整版本回退。
        /// </summary>
        public static async UniTask<ResourcePackageUpdateResult> UpdateAsync(
            IResourcePackageUpdateOperations operations,
            ResourceUpdateSettings settings,
            bool allowLocalFallback)
        {
            if (operations == null)
            {
                throw new ArgumentNullException(nameof(operations));
            }

            settings ??= new ResourceUpdateSettings();
            ResourceOperationResult<string> versionResult =
                await operations.RequestPackageVersionAsync(settings.VersionRequestTimeoutSeconds);
            string remoteError;

            if (!versionResult.Succeeded)
            {
                remoteError = versionResult.Error;
            }
            else
            {
                ResourceOperationResult manifestResult = await operations.LoadManifestAsync(
                    versionResult.Value,
                    settings.ManifestLoadTimeoutSeconds);
                if (!manifestResult.Succeeded)
                {
                    remoteError = manifestResult.Error;
                }
                else
                {
                    ResourceOperationResult importResult = await ImportCompletedAsync(operations);
                    ResourceOperationResult downloadResult = await operations.DownloadAsync(
                        settings.DownloadMaximumConcurrency,
                        settings.DownloadRetryCount);
                    if (downloadResult.Succeeded)
                    {
                        operations.SaveCompletedVersion(versionResult.Value);
                        return ResourcePackageUpdateResult.RemoteSucceeded(versionResult.Value);
                    }

                    remoteError = importResult.Succeeded
                        ? downloadResult.Error
                        : $"后台下载导入失败：{importResult.Error}；普通下载失败：{downloadResult.Error}";
                }
            }

            if (!allowLocalFallback)
            {
                return ResourcePackageUpdateResult.Failed(remoteError, string.Empty);
            }

            return await TryLoadLocalAsync(operations, settings, remoteError);
        }

        /// <summary>
        /// 激活包体内置资源版本和清单，不执行远端下载、缓存导入或版本记录。
        /// </summary>
        public static async UniTask<ResourcePackageUpdateResult> ActivateBuiltinAsync(
            IResourcePackageUpdateOperations operations,
            ResourceUpdateSettings settings)
        {
            if (operations == null)
            {
                throw new ArgumentNullException(nameof(operations));
            }

            settings ??= new ResourceUpdateSettings();
            ResourceOperationResult<string> versionResult =
                await operations.RequestPackageVersionAsync(settings.VersionRequestTimeoutSeconds);
            if (!versionResult.Succeeded)
            {
                return ResourcePackageUpdateResult.BuiltinFailed(versionResult.Error);
            }

            ResourceOperationResult manifestResult = await operations.LoadManifestAsync(
                versionResult.Value,
                settings.ManifestLoadTimeoutSeconds);
            return manifestResult.Succeeded
                ? ResourcePackageUpdateResult.BuiltinSucceeded(versionResult.Value)
                : ResourcePackageUpdateResult.BuiltinFailed(manifestResult.Error);
        }

        /// <summary>
        /// 尝试加载上次完整版本或包体内置版本，并校验本地内容完整性。
        /// </summary>
        private static async UniTask<ResourcePackageUpdateResult> TryLoadLocalAsync(
            IResourcePackageUpdateOperations operations,
            ResourceUpdateSettings settings,
            string remoteError)
        {
            string localVersion = operations.GetCompletedVersion();
            if (string.IsNullOrWhiteSpace(localVersion))
            {
                ResourceOperationResult<string> builtinVersionResult = await operations.GetBuiltinVersionAsync();
                if (!builtinVersionResult.Succeeded)
                {
                    return ResourcePackageUpdateResult.Failed(remoteError, builtinVersionResult.Error);
                }

                localVersion = builtinVersionResult.Value;
            }

            ResourceOperationResult manifestResult =
                await operations.LoadManifestAsync(localVersion, settings.ManifestLoadTimeoutSeconds);
            if (!manifestResult.Succeeded)
            {
                return ResourcePackageUpdateResult.Failed(remoteError, manifestResult.Error);
            }

            ResourceOperationResult importResult = await ImportCompletedAsync(operations);
            if (!operations.IsLocalContentComplete())
            {
                string localError = importResult.Succeeded
                    ? "本地资源内容不完整。"
                    : $"本地资源内容不完整。后台下载导入失败：{importResult.Error}";
                return ResourcePackageUpdateResult.Failed(remoteError, localError);
            }

            return ResourcePackageUpdateResult.LocalSucceeded(localVersion, remoteError);
        }

        /// <summary>
        /// 在已加载资源清单后导入操作系统完成的后台下载文件。
        /// 未提供后台下载能力的既有实现保持原有更新行为。
        /// </summary>
        private static UniTask<ResourceOperationResult> ImportCompletedAsync(IResourcePackageUpdateOperations operations)
        {
            if (operations is IResourcePackageBackgroundImportOperations backgroundImportOperations)
            {
                return backgroundImportOperations.ImportCompletedAsync();
            }

            return UniTask.FromResult(ResourceOperationResult.Success());
        }
    }
}

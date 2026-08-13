using System;
using Cysharp.Threading.Tasks;
using EF.Debugger;
using UnityEngine;
using UnityEngine.Networking;
using YooAsset;

namespace EF.Resource
{
    /// <summary>
    /// 将 YooAsset 的版本、清单和下载 operation 适配到弱联网更新协调器。
    /// </summary>
    internal sealed class YooAssetResourcePackageUpdateOperations :
        IResourcePackageUpdateOperations,
        IResourcePackageBackgroundImportOperations
    {
        private const string CompletedVersionKeyPrefix = "EF.Resource.CompletedVersion.";

        private readonly ResourcePackage _package;
        private readonly ResourceBackgroundDownloadService _backgroundDownloads;
        private readonly ResourceUpdateSettings _settings;

        /// <summary>
        /// 创建指定资源包裹的更新操作适配器。
        /// </summary>
        public YooAssetResourcePackageUpdateOperations(
            ResourcePackage package,
            ResourceBackgroundDownloadService backgroundDownloads,
            ResourceUpdateSettings settings)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
            _backgroundDownloads = backgroundDownloads;
            _settings = settings ?? new ResourceUpdateSettings();
        }

        /// <inheritdoc />
        public async UniTask<ResourceOperationResult<string>> RequestPackageVersionAsync(int timeoutSeconds)
        {
            var options = new RequestPackageVersionOptions(true, timeoutSeconds);
            RequestPackageVersionOperation operation = _package.RequestPackageVersionAsync(options);
            await operation;
            return operation.Status == EOperationStatus.Succeeded
                ? ResourceOperationResult<string>.Success(operation.PackageVersion)
                : ResourceOperationResult<string>.Failure(operation.Error);
        }

        /// <inheritdoc />
        public async UniTask<ResourceOperationResult> LoadManifestAsync(string packageVersion, int timeoutSeconds)
        {
            var options = new LoadPackageManifestOptions(packageVersion, timeoutSeconds);
            LoadPackageManifestOperation operation = _package.LoadPackageManifestAsync(options);
            await operation;
            return operation.Status == EOperationStatus.Succeeded
                ? ResourceOperationResult.Success()
                : ResourceOperationResult.Failure(operation.Error);
        }

        /// <inheritdoc />
        public async UniTask<ResourceOperationResult> DownloadAsync(int maximumConcurrency, int retryCount)
        {
            if (!ResourceRuntimeCapabilities.Current.SupportsResourceDownloader)
            {
                return ResourceOperationResult.Success();
            }

            var options = new ResourceDownloaderOptions(maximumConcurrency, retryCount);
            ResourceDownloaderOperation downloader = _package.CreateResourceDownloader(options);
            if (downloader.TotalDownloadCount == 0)
            {
                return ResourceOperationResult.Success();
            }

            downloader.DownloadCompleted += OnDownloadCompleted;
            downloader.DownloadError += OnDownloadError;
            downloader.DownloadProgressChanged += OnDownloadProgressChanged;
            downloader.DownloadFileStarted += OnDownloadFileStarted;
            downloader.StartDownload();
            await downloader;

            return downloader.Status == EOperationStatus.Succeeded
                ? ResourceOperationResult.Success()
                : ResourceOperationResult.Failure(downloader.Error);
        }

        /// <inheritdoc />
        public async UniTask<ResourceOperationResult> ImportCompletedAsync()
        {
            if (!ResourceRuntimeCapabilities.Current.SupportsResourceDownloader || _backgroundDownloads == null)
            {
                return ResourceOperationResult.Success();
            }

            ResourceOperationResult result = await _backgroundDownloads.ImportCompletedAsync(
                _package,
                _package.PackageName,
                _settings);
            if (!result.Succeeded)
            {
                Log.Warning(
                    $"资源包裹 {_package.PackageName} 的后台下载文件导入失败，将继续使用普通下载：{result.Error}");
            }

            return result;
        }

        /// <inheritdoc />
        public string GetCompletedVersion()
        {
            return PlayerPrefs.GetString(GetCompletedVersionKey(_package.PackageName), string.Empty);
        }

        /// <inheritdoc />
        public async UniTask<ResourceOperationResult<string>> GetBuiltinVersionAsync()
        {
            string versionFileUrl = GetBuiltinVersionFileUrl(_package.PackageName);
            UnityWebRequest request = UnityWebRequest.Get(versionFileUrl);
            request.timeout = 60;
            try
            {
                await request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    return ResourceOperationResult<string>.Failure(request.error);
                }

                string packageVersion = request.downloadHandler != null
                    ? request.downloadHandler.text.Trim()
                    : string.Empty;
                return string.IsNullOrEmpty(packageVersion)
                    ? ResourceOperationResult<string>.Failure("包体内置版本文件内容为空")
                    : ResourceOperationResult<string>.Success(packageVersion);
            }
            finally
            {
                request.Dispose();
            }
        }

        /// <inheritdoc />
        public bool IsLocalContentComplete()
        {
            var options = new ResourceDownloaderOptions(1, 1);
            ResourceDownloaderOperation downloader = _package.CreateResourceDownloader(options);
            return downloader.TotalDownloadCount == 0;
        }

        /// <inheritdoc />
        public void SaveCompletedVersion(string packageVersion)
        {
            PlayerPrefs.SetString(GetCompletedVersionKey(_package.PackageName), packageVersion);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 返回按包裹隔离的完整版本存储键。
        /// </summary>
        internal static string GetCompletedVersionKey(string packageName)
        {
            return CompletedVersionKeyPrefix + packageName;
        }

        /// <summary>
        /// 按 YooAsset 默认 StreamingAssets 结构生成内置版本文件 URL。
        /// </summary>
        internal static string GetBuiltinVersionFileUrl(string packageName)
        {
            string root = Application.streamingAssetsPath.Replace('\\', '/').TrimEnd('/');
            string yooFolder = YooAssetConfiguration.GetYooFolderName().Trim('/');
            string versionFile = YooAssetConfiguration.GetPackageVersionFileName(packageName);
            string path = string.IsNullOrEmpty(yooFolder)
                ? $"{root}/{packageName}/{versionFile}"
                : $"{root}/{yooFolder}/{packageName}/{versionFile}";

            if (path.Contains("://") || path.StartsWith("jar:", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            return new Uri(path).AbsoluteUri;
        }

        /// <summary>
        /// 记录单个资源文件开始下载。
        /// </summary>
        private static void OnDownloadFileStarted(DownloadFileStartedEventArgs data)
        {
            Log.Info($"资源包裹 {data.PackageName} 开始下载文件：{data.FileName}，大小：{data.FileSize} 字节");
        }

        /// <summary>
        /// 记录资源包裹总体下载进度。
        /// </summary>
        private static void OnDownloadProgressChanged(DownloadProgressChangedEventArgs data)
        {
            Log.Info(
                $"资源包裹 {data.PackageName} 下载进度：{data.Progress:P2}，文件 {data.CurrentDownloadCount}/{data.TotalDownloadCount}，字节 {data.CurrentDownloadBytes}/{data.TotalDownloadBytes}");
        }

        /// <summary>
        /// 记录单个资源文件下载错误。
        /// </summary>
        private static void OnDownloadError(DownloadErrorEventArgs data)
        {
            Log.Error($"资源包裹 {data.PackageName} 下载错误，文件名称：{data.FileName}，错误信息：{data.ErrorInfo}");
        }

        /// <summary>
        /// 记录资源包裹下载最终状态。
        /// </summary>
        private static void OnDownloadCompleted(DownloadCompletedEventArgs data)
        {
            if (data.Succeeded)
            {
                Log.Info($"资源包裹 {data.PackageName} 下载完成");
                return;
            }

            Log.Error($"资源包裹 {data.PackageName} 下载失败：{data.Error}");
        }
    }
}

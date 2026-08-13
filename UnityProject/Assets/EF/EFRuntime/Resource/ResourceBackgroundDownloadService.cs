using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using EF.Debugger;
using UnityEngine;
using YooAsset;

namespace EF.Resource
{
    /// <summary>
    /// 后台下载元数据的持久化记录。
    /// </summary>
    [Serializable]
    internal sealed class ResourceBackgroundDownloadRecord
    {
        public string Id;
        public string PackageName;
        public string RemoteUrl;
        public string RelativeFilePath;
        public string BundleName;
        public string BundleGuid;
        public ResourceBackgroundDownloadPolicy Policy;
    }

    /// <summary>
    /// 后台下载元数据存储接口。
    /// </summary>
    internal interface IResourceBackgroundDownloadStore
    {
        /// <summary>
        /// 读取上次保存的后台任务。
        /// </summary>
        List<ResourceBackgroundDownloadRecord> Load();

        /// <summary>
        /// 覆盖保存当前后台任务。
        /// </summary>
        void Save(IReadOnlyList<ResourceBackgroundDownloadRecord> records);
    }

    /// <summary>
    /// YooAsset Bundle 导入器抽象。
    /// </summary>
    internal interface IResourceBundleImporter
    {
        /// <summary>
        /// 将外部已下载文件校验后导入 YooAsset 沙盒缓存。
        /// </summary>
        UniTask<ResourceOperationResult> ImportAsync(
            ResourcePackage package,
            ImportBundleInfo[] bundleInfos,
            int maximumConcurrency,
            int retryCount);
    }

    /// <summary>
    /// 管理系统后台下载、任务恢复以及 YooAsset 缓存导入。
    /// </summary>
    public sealed class ResourceBackgroundDownloadService : IResourceBackgroundDownloadService
    {
        private const string DownloadRoot = "EFResourceDownloads";

        private readonly IResourceBackgroundDownloadBackend _backend;
        private readonly IResourceBackgroundDownloadStore _store;
        private readonly IResourceBundleImporter _importer;
        private readonly string _persistentRoot;
        private readonly List<ResourceBackgroundDownloadRecord> _records;

        /// <summary>
        /// 使用当前平台注册 backend 和默认 JSON 存储创建服务。
        /// </summary>
        public ResourceBackgroundDownloadService()
            : this(
                ResourceBackgroundDownloadBackendRegistry.GetBackend(),
                new JsonResourceBackgroundDownloadStore(Application.persistentDataPath),
                new YooAssetResourceBundleImporter(),
                Application.persistentDataPath)
        {
        }

        /// <summary>
        /// 使用可替换依赖创建后台下载服务，供框架测试和平台适配使用。
        /// </summary>
        internal ResourceBackgroundDownloadService(
            IResourceBackgroundDownloadBackend backend,
            IResourceBackgroundDownloadStore store,
            IResourceBundleImporter importer,
            string persistentRoot)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _importer = importer ?? throw new ArgumentNullException(nameof(importer));
            _persistentRoot = string.IsNullOrWhiteSpace(persistentRoot)
                ? throw new ArgumentException("持久化根目录不能为空", nameof(persistentRoot))
                : persistentRoot;
            _records = _store.Load() ?? new List<ResourceBackgroundDownloadRecord>();
        }

        public bool IsSupported => _backend.IsSupported;

        /// <inheritdoc />
        public ResourceBackgroundDownloadInfo Start(ResourceBackgroundDownloadRequest request)
        {
            if (!IsSupported)
            {
                throw new PlatformNotSupportedException("后台下载仅支持 Android、iOS 和 UWP 真机平台");
            }

            ValidateRequest(request, out Uri remoteUri, out string packageName, out string bundleName);
            string id = Guid.NewGuid().ToString("N");
            string fileName = Path.GetFileName(remoteUri.LocalPath);
            string relativePath = $"{DownloadRoot}/{SanitizePathSegment(packageName)}/{id}_{fileName}";
            IResourceBackgroundDownloadHandle handle = _backend.Start(remoteUri, relativePath, request.Policy);

            var record = new ResourceBackgroundDownloadRecord
            {
                Id = id,
                PackageName = packageName,
                RemoteUrl = remoteUri.AbsoluteUri,
                RelativeFilePath = relativePath,
                BundleName = bundleName,
                BundleGuid = request.BundleGuid ?? string.Empty,
                Policy = request.Policy
            };
            _records.Add(record);
            _store.Save(_records);
            return CreateInfo(record, handle);
        }

        /// <inheritdoc />
        public IReadOnlyList<ResourceBackgroundDownloadInfo> GetDownloads()
        {
            Dictionary<string, IResourceBackgroundDownloadHandle> handles = GetHandleMap();
            var result = new List<ResourceBackgroundDownloadInfo>(_records.Count);
            foreach (ResourceBackgroundDownloadRecord record in _records)
            {
                handles.TryGetValue(record.RelativeFilePath, out IResourceBackgroundDownloadHandle handle);
                result.Add(CreateInfo(record, handle));
            }

            return result;
        }

        /// <inheritdoc />
        public bool Cancel(string id)
        {
            int index = _records.FindIndex(record => string.Equals(record.Id, id, StringComparison.Ordinal));
            if (index < 0)
            {
                return false;
            }

            ResourceBackgroundDownloadRecord record = _records[index];
            Dictionary<string, IResourceBackgroundDownloadHandle> handles = GetHandleMap();
            if (handles.TryGetValue(record.RelativeFilePath, out IResourceBackgroundDownloadHandle handle))
            {
                handle.Dispose();
            }

            DeleteSourceFile(record.RelativeFilePath);
            _records.RemoveAt(index);
            _store.Save(_records);
            return true;
        }

        /// <summary>
        /// 把指定包裹已经完成的后台文件导入 YooAsset 缓存。
        /// </summary>
        internal async UniTask<ResourceOperationResult> ImportCompletedAsync(
            ResourcePackage package,
            string packageName,
            ResourceUpdateSettings settings)
        {
            Dictionary<string, IResourceBackgroundDownloadHandle> handles = GetHandleMap();
            var completedRecords = new List<ResourceBackgroundDownloadRecord>();
            var completedHandles = new List<IResourceBackgroundDownloadHandle>();
            var importInfos = new List<ImportBundleInfo>();

            foreach (ResourceBackgroundDownloadRecord record in _records)
            {
                if (!string.Equals(record.PackageName, packageName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!handles.TryGetValue(record.RelativeFilePath, out IResourceBackgroundDownloadHandle handle) ||
                    handle.State != ResourceBackgroundDownloadState.Completed)
                {
                    continue;
                }

                completedRecords.Add(record);
                completedHandles.Add(handle);
                importInfos.Add(new ImportBundleInfo(
                    GetAbsoluteSourcePath(record.RelativeFilePath),
                    record.BundleName,
                    record.BundleGuid));
            }

            if (importInfos.Count == 0)
            {
                return ResourceOperationResult.Success();
            }

            settings ??= new ResourceUpdateSettings();
            ResourceOperationResult importResult = await _importer.ImportAsync(
                package,
                importInfos.ToArray(),
                settings.ImportMaximumConcurrency,
                settings.ImportRetryCount);
            if (!importResult.Succeeded)
            {
                return importResult;
            }

            for (int index = 0; index < completedRecords.Count; index++)
            {
                ResourceBackgroundDownloadRecord record = completedRecords[index];
                completedHandles[index].Dispose();
                DeleteSourceFile(record.RelativeFilePath);
                _records.Remove(record);
            }

            _store.Save(_records);
            return ResourceOperationResult.Success();
        }

        /// <summary>
        /// 校验并归一化后台下载请求的必要字段。
        /// </summary>
        private static void ValidateRequest(
            ResourceBackgroundDownloadRequest request,
            out Uri remoteUri,
            out string packageName,
            out string bundleName)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            packageName = request.PackageName != null ? request.PackageName.Trim() : string.Empty;
            bundleName = request.BundleName != null ? request.BundleName.Trim() : string.Empty;
            if (string.IsNullOrEmpty(packageName))
            {
                throw new ArgumentException("资源包名称不能为空", nameof(request));
            }

            if (string.IsNullOrEmpty(bundleName))
            {
                throw new ArgumentException("Bundle 名称不能为空", nameof(request));
            }

            if (!Uri.TryCreate(request.RemoteUrl, UriKind.Absolute, out remoteUri) ||
                (remoteUri.Scheme != Uri.UriSchemeHttp && remoteUri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException("后台下载地址必须是有效的 HTTP 或 HTTPS URL", nameof(request));
            }

            if (string.IsNullOrEmpty(Path.GetFileName(remoteUri.LocalPath)))
            {
                throw new ArgumentException("后台下载地址必须包含文件名", nameof(request));
            }
        }

        /// <summary>
        /// 将包裹名称转换为安全的单级目录名称。
        /// </summary>
        private static string SanitizePathSegment(string value)
        {
            char[] chars = value.ToCharArray();
            for (int index = 0; index < chars.Length; index++)
            {
                char valueChar = chars[index];
                if (!char.IsLetterOrDigit(valueChar) && valueChar != '-' && valueChar != '_' && valueChar != '.')
                {
                    chars[index] = '_';
                }
            }

            return new string(chars);
        }

        /// <summary>
        /// 合并持久化元数据与系统句柄状态生成查询快照。
        /// </summary>
        private ResourceBackgroundDownloadInfo CreateInfo(
            ResourceBackgroundDownloadRecord record,
            IResourceBackgroundDownloadHandle handle)
        {
            ResourceBackgroundDownloadState state = handle != null
                ? handle.State
                : ResourceBackgroundDownloadState.Unknown;
            float progress = handle != null ? handle.Progress : 0f;
            string error = handle != null ? handle.Error : string.Empty;
            return new ResourceBackgroundDownloadInfo(
                record.Id,
                record.PackageName,
                record.RemoteUrl,
                record.BundleName,
                state,
                progress,
                error);
        }

        /// <summary>
        /// 按归一化相对路径索引当前系统后台下载句柄。
        /// </summary>
        private Dictionary<string, IResourceBackgroundDownloadHandle> GetHandleMap()
        {
            var result = new Dictionary<string, IResourceBackgroundDownloadHandle>(StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<IResourceBackgroundDownloadHandle> handles = _backend.GetDownloads();
            if (handles == null)
            {
                return result;
            }

            foreach (IResourceBackgroundDownloadHandle handle in handles)
            {
                if (handle != null && !string.IsNullOrEmpty(handle.RelativeFilePath))
                {
                    result[NormalizeRelativePath(handle.RelativeFilePath)] = handle;
                }
            }

            return result;
        }

        /// <summary>
        /// 生成并校验位于 persistentDataPath 内部的源文件绝对路径。
        /// </summary>
        private string GetAbsoluteSourcePath(string relativePath)
        {
            string normalizedRelativePath = NormalizeRelativePath(relativePath)
                .Replace('/', Path.DirectorySeparatorChar);
            string fullRoot = Path.GetFullPath(_persistentRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(Path.Combine(fullRoot, normalizedRelativePath));
            if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("后台下载文件路径越过了持久化根目录");
            }

            return fullPath;
        }

        /// <summary>
        /// 删除导入完成或取消后的后台下载源文件。
        /// </summary>
        private void DeleteSourceFile(string relativePath)
        {
            string fullPath = GetAbsoluteSourcePath(relativePath);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }

        /// <summary>
        /// 将平台路径统一为后台任务使用的正斜杠相对路径。
        /// </summary>
        private static string NormalizeRelativePath(string path)
        {
            return path.Replace('\\', '/').TrimStart('/');
        }
    }

    /// <summary>
    /// 使用 Unity JsonUtility 保存后台下载恢复队列。
    /// </summary>
    internal sealed class JsonResourceBackgroundDownloadStore : IResourceBackgroundDownloadStore
    {
        private const string MetadataDirectory = "EFResource";
        private const string MetadataFileName = "background-downloads.json";

        private readonly string _filePath;

        /// <summary>
        /// 创建 persistentDataPath 下的 JSON 元数据存储。
        /// </summary>
        public JsonResourceBackgroundDownloadStore(string persistentRoot)
        {
            _filePath = Path.Combine(persistentRoot, MetadataDirectory, MetadataFileName);
        }

        /// <inheritdoc />
        public List<ResourceBackgroundDownloadRecord> Load()
        {
            if (!File.Exists(_filePath))
            {
                return new List<ResourceBackgroundDownloadRecord>();
            }

            try
            {
                string json = File.ReadAllText(_filePath);
                ResourceBackgroundDownloadRecordCollection collection =
                    JsonUtility.FromJson<ResourceBackgroundDownloadRecordCollection>(json);
                return collection != null && collection.Records != null
                    ? collection.Records
                    : new List<ResourceBackgroundDownloadRecord>();
            }
            catch (Exception exception)
            {
                Log.Warning("读取后台下载恢复队列失败，将使用空队列：" + exception.Message);
                return new List<ResourceBackgroundDownloadRecord>();
            }
        }

        /// <inheritdoc />
        public void Save(IReadOnlyList<ResourceBackgroundDownloadRecord> records)
        {
            string directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var collection = new ResourceBackgroundDownloadRecordCollection
            {
                Records = new List<ResourceBackgroundDownloadRecord>(records)
            };
            File.WriteAllText(_filePath, JsonUtility.ToJson(collection));
        }

        [Serializable]
        private sealed class ResourceBackgroundDownloadRecordCollection
        {
            public List<ResourceBackgroundDownloadRecord> Records = new();
        }
    }

    /// <summary>
    /// 使用 YooAsset ResourceImporterOperation 校验并导入外部 Bundle。
    /// </summary>
    internal sealed class YooAssetResourceBundleImporter : IResourceBundleImporter
    {
        /// <inheritdoc />
        public async UniTask<ResourceOperationResult> ImportAsync(
            ResourcePackage package,
            ImportBundleInfo[] bundleInfos,
            int maximumConcurrency,
            int retryCount)
        {
            if (package == null)
            {
                return ResourceOperationResult.Failure("资源包裹为空，无法导入后台下载文件");
            }

            var options = new BundleImporterOptions(bundleInfos, maximumConcurrency, retryCount);
            ResourceImporterOperation importer = package.CreateResourceImporter(options);
            if (importer.TotalDownloadCount > bundleInfos.Length)
            {
                return ResourceOperationResult.Failure(
                    $"后台文件导入数量异常：请求 {bundleInfos.Length}，实际 {importer.TotalDownloadCount}");
            }

            if (importer.TotalDownloadCount < bundleInfos.Length)
            {
                Log.Warning(
                    $"后台下载文件与当前资源清单只匹配 {importer.TotalDownloadCount}/{bundleInfos.Length} 项，未匹配项将作为陈旧任务清理");
            }

            importer.StartDownload();
            await importer;
            return importer.Status == EOperationStatus.Succeeded
                ? ResourceOperationResult.Success()
                : ResourceOperationResult.Failure(importer.Error);
        }
    }
}

using System;
using System.Collections.Generic;

namespace EF.Resource
{
    /// <summary>
    /// 后台下载允许使用的网络类型策略。
    /// </summary>
    public enum ResourceBackgroundDownloadPolicy
    {
        Default = 0,
        UnrestrictedOnly = 1,
        AllowMetered = 2,
        AlwaysAllow = 3
    }

    /// <summary>
    /// 后台下载任务状态。
    /// </summary>
    public enum ResourceBackgroundDownloadState
    {
        Unknown = 0,
        Downloading = 1,
        Completed = 2,
        Failed = 3
    }

    /// <summary>
    /// 创建后台资源下载任务所需的信息。
    /// </summary>
    public sealed class ResourceBackgroundDownloadRequest
    {
        /// <summary>
        /// 创建后台资源下载请求。
        /// </summary>
        public ResourceBackgroundDownloadRequest(
            string packageName,
            string remoteUrl,
            string bundleName,
            string bundleGuid = null,
            ResourceBackgroundDownloadPolicy policy = ResourceBackgroundDownloadPolicy.Default)
        {
            PackageName = packageName;
            RemoteUrl = remoteUrl;
            BundleName = bundleName;
            BundleGuid = bundleGuid;
            Policy = policy;
        }

        public string PackageName { get; }

        public string RemoteUrl { get; }

        public string BundleName { get; }

        public string BundleGuid { get; }

        public ResourceBackgroundDownloadPolicy Policy { get; }
    }

    /// <summary>
    /// 可供 UI 或业务层查询的后台下载任务快照。
    /// </summary>
    public readonly struct ResourceBackgroundDownloadInfo
    {
        /// <summary>
        /// 创建后台下载任务快照。
        /// </summary>
        public ResourceBackgroundDownloadInfo(
            string id,
            string packageName,
            string remoteUrl,
            string bundleName,
            ResourceBackgroundDownloadState state,
            float progress,
            string error)
        {
            Id = id;
            PackageName = packageName;
            RemoteUrl = remoteUrl;
            BundleName = bundleName;
            State = state;
            Progress = progress;
            Error = error ?? string.Empty;
        }

        public string Id { get; }

        public string PackageName { get; }

        public string RemoteUrl { get; }

        public string BundleName { get; }

        public ResourceBackgroundDownloadState State { get; }

        public float Progress { get; }

        public string Error { get; }
    }

    /// <summary>
    /// 资源系统对外暴露的移动端后台下载能力。
    /// </summary>
    public interface IResourceBackgroundDownloadService
    {
        bool IsSupported { get; }

        /// <summary>
        /// 启动一个可跨应用生命周期继续执行的后台下载任务。
        /// </summary>
        ResourceBackgroundDownloadInfo Start(ResourceBackgroundDownloadRequest request);

        /// <summary>
        /// 返回当前持久化任务及其系统下载状态快照。
        /// </summary>
        IReadOnlyList<ResourceBackgroundDownloadInfo> GetDownloads();

        /// <summary>
        /// 取消并移除指定后台下载任务。
        /// </summary>
        bool Cancel(string id);
    }

    /// <summary>
    /// 平台后台下载插件需要实现的最小 backend 契约。
    /// </summary>
    public interface IResourceBackgroundDownloadBackend
    {
        bool IsSupported { get; }

        /// <summary>
        /// 返回操作系统仍在管理的后台下载句柄。
        /// </summary>
        IReadOnlyList<IResourceBackgroundDownloadHandle> GetDownloads();

        /// <summary>
        /// 启动一个写入 persistentDataPath 相对路径的系统后台下载。
        /// </summary>
        IResourceBackgroundDownloadHandle Start(
            Uri remoteUri,
            string relativeFilePath,
            ResourceBackgroundDownloadPolicy policy);
    }

    /// <summary>
    /// 对平台后台下载句柄的统一包装。
    /// </summary>
    public interface IResourceBackgroundDownloadHandle : IDisposable
    {
        string RelativeFilePath { get; }

        ResourceBackgroundDownloadState State { get; }

        float Progress { get; }

        string Error { get; }
    }

    /// <summary>
    /// 允许受支持平台程序集在场景加载前注册后台下载 backend。
    /// </summary>
    public static class ResourceBackgroundDownloadBackendRegistry
    {
        private static IResourceBackgroundDownloadBackend _backend = new UnsupportedBackgroundDownloadBackend();

        /// <summary>
        /// 注册当前平台的后台下载 backend。
        /// </summary>
        public static void Register(IResourceBackgroundDownloadBackend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        /// <summary>
        /// 返回当前已注册的后台下载 backend。
        /// </summary>
        internal static IResourceBackgroundDownloadBackend GetBackend()
        {
            return _backend;
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        /// <summary>
        /// 在新的 Player 子系统启动时清除上次运行注册的 backend。
        /// </summary>
        private static void Reset()
        {
            _backend = new UnsupportedBackgroundDownloadBackend();
        }

        private sealed class UnsupportedBackgroundDownloadBackend : IResourceBackgroundDownloadBackend
        {
            public bool IsSupported => false;

            /// <inheritdoc />
            public IReadOnlyList<IResourceBackgroundDownloadHandle> GetDownloads()
            {
                return Array.Empty<IResourceBackgroundDownloadHandle>();
            }

            /// <inheritdoc />
            public IResourceBackgroundDownloadHandle Start(
                Uri remoteUri,
                string relativeFilePath,
                ResourceBackgroundDownloadPolicy policy)
            {
                throw new PlatformNotSupportedException("当前平台未注册后台下载 backend");
            }
        }
    }
}

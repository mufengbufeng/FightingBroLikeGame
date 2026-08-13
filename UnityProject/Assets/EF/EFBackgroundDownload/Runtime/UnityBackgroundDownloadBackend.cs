using System;
using System.Collections.Generic;
using EF.Resource;
using Unity.Networking;
using UnityEngine;
using UnityBackgroundDownload = Unity.Networking.BackgroundDownload;

namespace EF.BackgroundDownload
{
    /// <summary>
    /// 将 Unity Background Download 插件适配为 EF 资源后台下载 backend。
    /// </summary>
    internal sealed class UnityBackgroundDownloadBackend : IResourceBackgroundDownloadBackend
    {
        public bool IsSupported
        {
            get
            {
#if UNITY_ANDROID || UNITY_IOS || UNITY_WSA_10_0
                return true;
#else
                return false;
#endif
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<IResourceBackgroundDownloadHandle> GetDownloads()
        {
            if (!IsSupported)
            {
                return Array.Empty<IResourceBackgroundDownloadHandle>();
            }

            UnityBackgroundDownload[] downloads = UnityBackgroundDownload.backgroundDownloads;
            var result = new IResourceBackgroundDownloadHandle[downloads.Length];
            for (int index = 0; index < downloads.Length; index++)
            {
                result[index] = new UnityBackgroundDownloadHandle(downloads[index]);
            }

            return result;
        }

        /// <inheritdoc />
        public IResourceBackgroundDownloadHandle Start(
            Uri remoteUri,
            string relativeFilePath,
            ResourceBackgroundDownloadPolicy policy)
        {
            if (!IsSupported)
            {
                throw new PlatformNotSupportedException("Unity Background Download 在 Editor 中仅支持编译");
            }

            var config = new BackgroundDownloadConfig
            {
                url = remoteUri,
                filePath = relativeFilePath,
                policy = ConvertPolicy(policy)
            };
            return new UnityBackgroundDownloadHandle(UnityBackgroundDownload.Start(config));
        }

        /// <summary>
        /// 将 EF 网络策略映射为 Unity Background Download 策略。
        /// </summary>
        private static BackgroundDownloadPolicy ConvertPolicy(ResourceBackgroundDownloadPolicy policy)
        {
            return policy switch
            {
                ResourceBackgroundDownloadPolicy.UnrestrictedOnly => BackgroundDownloadPolicy.UnrestrictedOnly,
                ResourceBackgroundDownloadPolicy.AllowMetered => BackgroundDownloadPolicy.AllowMetered,
                ResourceBackgroundDownloadPolicy.AlwaysAllow => BackgroundDownloadPolicy.AlwaysAllow,
                _ => BackgroundDownloadPolicy.Default
            };
        }

        /// <summary>
        /// 包装 Unity 插件的下载句柄并统一状态枚举。
        /// </summary>
        private sealed class UnityBackgroundDownloadHandle : IResourceBackgroundDownloadHandle
        {
            private readonly UnityBackgroundDownload _download;

            /// <summary>
            /// 创建后台下载句柄包装。
            /// </summary>
            public UnityBackgroundDownloadHandle(UnityBackgroundDownload download)
            {
                _download = download ?? throw new ArgumentNullException(nameof(download));
            }

            public string RelativeFilePath => _download.config.filePath;

            public ResourceBackgroundDownloadState State
            {
                get
                {
                    return _download.status switch
                    {
                        BackgroundDownloadStatus.Downloading => ResourceBackgroundDownloadState.Downloading,
                        BackgroundDownloadStatus.Done => ResourceBackgroundDownloadState.Completed,
                        BackgroundDownloadStatus.Failed => ResourceBackgroundDownloadState.Failed,
                        _ => ResourceBackgroundDownloadState.Unknown
                    };
                }
            }

            public float Progress => _download.progress;

            public string Error => _download.error ?? string.Empty;

            /// <inheritdoc />
            public void Dispose()
            {
                _download.Dispose();
            }
        }
    }

    /// <summary>
    /// 在首个场景加载前注册 Unity 移动端后台下载 backend。
    /// </summary>
    internal static class UnityBackgroundDownloadBackendInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        /// <summary>
        /// 在首场景加载前向资源系统注册移动端 backend。
        /// </summary>
        private static void Register()
        {
            ResourceBackgroundDownloadBackendRegistry.Register(new UnityBackgroundDownloadBackend());
        }
    }
}

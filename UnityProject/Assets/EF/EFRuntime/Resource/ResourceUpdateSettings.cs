using System;
using UnityEngine;

namespace EF.Resource
{
    /// <summary>
    /// 资源版本请求、清单加载、下载和导入的统一参数。
    /// </summary>
    [Serializable]
    public sealed class ResourceUpdateSettings
    {
        [SerializeField]
        [Tooltip("Host 模式远端更新失败时，是否回退到上次完整版本或包体内置版本")]
        private bool _enableWeakNetworkFallback = true;

        [SerializeField]
        [Tooltip("远端版本请求超时时间（秒）")]
        private int _versionRequestTimeoutSeconds = 30;

        [SerializeField]
        [Tooltip("资源清单加载超时时间（秒）")]
        private int _manifestLoadTimeoutSeconds = 60;

        [SerializeField]
        [Tooltip("资源文件最大并发下载数量")]
        private int _downloadMaximumConcurrency = 10;

        [SerializeField]
        [Tooltip("单个资源文件下载失败后的重试次数")]
        private int _downloadRetryCount = 3;

        [SerializeField]
        [Tooltip("后台下载文件导入缓存时的最大并发数量")]
        private int _importMaximumConcurrency = 10;

        [SerializeField]
        [Tooltip("后台下载文件导入缓存失败后的重试次数")]
        private int _importRetryCount = 3;

        /// <summary>
        /// Host 模式是否启用弱联网本地回退。
        /// </summary>
        public bool EnableWeakNetworkFallback => _enableWeakNetworkFallback;

        /// <summary>
        /// 远端版本请求超时时间（秒）。
        /// </summary>
        public int VersionRequestTimeoutSeconds => Mathf.Clamp(_versionRequestTimeoutSeconds, 1, 300);

        /// <summary>
        /// 资源清单加载超时时间（秒）。
        /// </summary>
        public int ManifestLoadTimeoutSeconds => Mathf.Clamp(_manifestLoadTimeoutSeconds, 1, 300);

        /// <summary>
        /// 资源下载最大并发数量。
        /// </summary>
        public int DownloadMaximumConcurrency => Mathf.Clamp(_downloadMaximumConcurrency, 1, 32);

        /// <summary>
        /// 资源下载失败重试次数。
        /// </summary>
        public int DownloadRetryCount => Mathf.Clamp(_downloadRetryCount, 0, 10);

        /// <summary>
        /// 后台资源导入最大并发数量。
        /// </summary>
        public int ImportMaximumConcurrency => Mathf.Clamp(_importMaximumConcurrency, 1, 32);

        /// <summary>
        /// 后台资源导入失败重试次数。
        /// </summary>
        public int ImportRetryCount => Mathf.Clamp(_importRetryCount, 0, 10);
    }
}

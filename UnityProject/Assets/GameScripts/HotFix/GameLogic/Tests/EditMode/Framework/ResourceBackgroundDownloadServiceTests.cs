using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using EF.Resource;
using NUnit.Framework;
using YooAsset;

namespace GameLogic.Tests.EditMode.Framework
{
    /// <summary>
    /// 验证后台下载任务的持久化、恢复导入与成功清理行为。
    /// </summary>
    [TestFixture]
    public sealed class ResourceBackgroundDownloadServiceTests
    {
        /// <summary>
        /// 启动后台下载时应生成安全的相对路径并立即保存恢复元数据。
        /// </summary>
        [Test]
        public void Start_ValidRequest_StartsBackendAndPersistsRecord()
        {
            var backend = new FakeBackgroundDownloadBackend();
            var store = new FakeBackgroundDownloadStore();
            var service = new ResourceBackgroundDownloadService(
                backend,
                store,
                new FakeBundleImporter(),
                "C:/persistent");
            var request = new ResourceBackgroundDownloadRequest(
                "DefaultPackage",
                "https://cdn.example.com/bundles/hero.bundle",
                "hero.bundle",
                "hero-guid");

            ResourceBackgroundDownloadInfo info = service.Start(request);

            Assert.AreEqual(ResourceBackgroundDownloadState.Downloading, info.State);
            Assert.AreEqual(1, backend.StartedPaths.Count);
            StringAssert.StartsWith("EFResourceDownloads/DefaultPackage/", backend.StartedPaths[0]);
            StringAssert.EndsWith("_hero.bundle", backend.StartedPaths[0]);
            Assert.AreEqual(1, store.Records.Count);
            Assert.AreEqual(info.Id, store.Records[0].Id);
        }

        /// <summary>
        /// 已完成任务导入成功后应删除持久化记录并释放系统下载句柄。
        /// </summary>
        [Test]
        public async Task ImportCompletedAsync_ImportSucceeded_RemovesRecordAndDisposesHandle()
        {
            var backend = new FakeBackgroundDownloadBackend();
            var store = new FakeBackgroundDownloadStore();
            var importer = new FakeBundleImporter();
            var service = new ResourceBackgroundDownloadService(backend, store, importer, "C:/persistent");
            var request = new ResourceBackgroundDownloadRequest(
                "DefaultPackage",
                "https://cdn.example.com/bundles/hero.bundle",
                "hero.bundle",
                "hero-guid");
            service.Start(request);
            FakeBackgroundDownloadHandle handle = backend.Handles[0];
            handle.StateValue = ResourceBackgroundDownloadState.Completed;

            ResourceOperationResult result = await service.ImportCompletedAsync(
                null,
                "DefaultPackage",
                new ResourceUpdateSettings());

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(1, importer.ImportedBundles.Count);
            Assert.AreEqual("hero.bundle", importer.ImportedBundles[0].BundleName);
            Assert.AreEqual(0, store.Records.Count);
            Assert.IsTrue(handle.Disposed);
        }

        /// <summary>
        /// 纯内存后台下载 backend，用于模拟操作系统任务状态。
        /// </summary>
        private sealed class FakeBackgroundDownloadBackend : IResourceBackgroundDownloadBackend
        {
            public readonly List<string> StartedPaths = new();
            public readonly List<FakeBackgroundDownloadHandle> Handles = new();

            public bool IsSupported => true;

            /// <summary>
            /// 返回当前 Fake 句柄快照。
            /// </summary>
            public IReadOnlyList<IResourceBackgroundDownloadHandle> GetDownloads()
            {
                return Handles;
            }

            /// <summary>
            /// 创建一个处于下载中的 Fake 句柄。
            /// </summary>
            public IResourceBackgroundDownloadHandle Start(
                Uri remoteUri,
                string relativeFilePath,
                ResourceBackgroundDownloadPolicy policy)
            {
                StartedPaths.Add(relativeFilePath);
                var handle = new FakeBackgroundDownloadHandle(relativeFilePath);
                Handles.Add(handle);
                return handle;
            }
        }

        /// <summary>
        /// 可变状态的后台下载 Fake 句柄。
        /// </summary>
        private sealed class FakeBackgroundDownloadHandle : IResourceBackgroundDownloadHandle
        {
            /// <summary>
            /// 创建指定相对路径的 Fake 下载句柄。
            /// </summary>
            public FakeBackgroundDownloadHandle(string relativeFilePath)
            {
                RelativeFilePath = relativeFilePath;
            }

            public string RelativeFilePath { get; }

            public ResourceBackgroundDownloadState State => StateValue;

            public ResourceBackgroundDownloadState StateValue { get; set; } =
                ResourceBackgroundDownloadState.Downloading;

            public float Progress => State == ResourceBackgroundDownloadState.Completed ? 1f : 0.5f;

            public string Error => string.Empty;

            public bool Disposed { get; private set; }

            /// <summary>
            /// 标记句柄已经释放。
            /// </summary>
            public void Dispose()
            {
                Disposed = true;
            }
        }

        /// <summary>
        /// 纯内存任务元数据仓库。
        /// </summary>
        private sealed class FakeBackgroundDownloadStore : IResourceBackgroundDownloadStore
        {
            public List<ResourceBackgroundDownloadRecord> Records { get; private set; } = new();

            /// <summary>
            /// 返回记录副本，模拟磁盘反序列化。
            /// </summary>
            public List<ResourceBackgroundDownloadRecord> Load()
            {
                return new List<ResourceBackgroundDownloadRecord>(Records);
            }

            /// <summary>
            /// 保存记录副本，模拟磁盘持久化。
            /// </summary>
            public void Save(IReadOnlyList<ResourceBackgroundDownloadRecord> records)
            {
                Records = new List<ResourceBackgroundDownloadRecord>(records);
            }
        }

        /// <summary>
        /// 记录导入参数并始终返回成功的 Fake 导入器。
        /// </summary>
        private sealed class FakeBundleImporter : IResourceBundleImporter
        {
            public readonly List<ImportBundleInfo> ImportedBundles = new();

            /// <summary>
            /// 记录待导入 Bundle 并返回成功。
            /// </summary>
            public UniTask<ResourceOperationResult> ImportAsync(
                ResourcePackage package,
                ImportBundleInfo[] bundleInfos,
                int maximumConcurrency,
                int retryCount)
            {
                ImportedBundles.AddRange(bundleInfos);
                return UniTask.FromResult(ResourceOperationResult.Success());
            }
        }
    }
}

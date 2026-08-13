using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using EF.Resource;
using NUnit.Framework;

namespace GameLogic.Tests.EditMode.Framework
{
    /// <summary>
    /// 验证弱联网更新流程只记录完整版本，并能回退到本地可用内容。
    /// </summary>
    [TestFixture]
    public sealed class ResourcePackageUpdateCoordinatorTests
    {
        /// <summary>
        /// 远端流程完整成功后，必须在下载结束之后记录版本。
        /// </summary>
        [Test]
        public async Task RemoteUpdateSucceeded_SavesVersionAfterDownload()
        {
            var operations = new FakeUpdateOperations
            {
                RemoteVersionResult = ResourceOperationResult<string>.Success("2.0.0"),
                DownloadResult = ResourceOperationResult.Success()
            };
            operations.ManifestResults.Enqueue(ResourceOperationResult.Success());

            ResourcePackageUpdateResult result = await ResourcePackageUpdateCoordinator.UpdateAsync(
                operations,
                new ResourceUpdateSettings(),
                true);

            Assert.IsTrue(result.Succeeded);
            Assert.IsFalse(result.UsedLocalFallback);
            CollectionAssert.AreEqual(
                new[] { "request-package", "manifest:2.0.0", "import-completed", "download", "save:2.0.0" },
                operations.Calls);
        }

        /// <summary>
        /// 下载失败时不能保存远端版本，并应加载上次完整版本。
        /// </summary>
        [Test]
        public async Task RemoteDownloadFailed_FallsBackWithoutSavingRemoteVersion()
        {
            var operations = new FakeUpdateOperations
            {
                RemoteVersionResult = ResourceOperationResult<string>.Success("2.0.0"),
                DownloadResult = ResourceOperationResult.Failure("network interrupted"),
                CompletedVersion = "1.0.0",
                LocalContentComplete = true
            };
            operations.ManifestResults.Enqueue(ResourceOperationResult.Success());
            operations.ManifestResults.Enqueue(ResourceOperationResult.Success());

            ResourcePackageUpdateResult result = await ResourcePackageUpdateCoordinator.UpdateAsync(
                operations,
                new ResourceUpdateSettings(),
                true);

            Assert.IsTrue(result.Succeeded);
            Assert.IsTrue(result.UsedLocalFallback);
            Assert.AreEqual("network interrupted", result.RemoteError);
            CollectionAssert.DoesNotContain(operations.Calls, "save:2.0.0");
            CollectionAssert.Contains(operations.Calls, "manifest:1.0.0");
            CollectionAssert.Contains(operations.Calls, "check-local-content");
        }

        /// <summary>
        /// 首次安装没有完整版本记录时，应读取包体内置版本作为兜底。
        /// </summary>
        [Test]
        public async Task NoCompletedVersion_UsesBuiltinVersionForFallback()
        {
            var operations = new FakeUpdateOperations
            {
                RemoteVersionResult = ResourceOperationResult<string>.Failure("offline"),
                BuiltinVersionResult = ResourceOperationResult<string>.Success("builtin-1"),
                LocalContentComplete = true
            };
            operations.ManifestResults.Enqueue(ResourceOperationResult.Success());

            ResourcePackageUpdateResult result = await ResourcePackageUpdateCoordinator.UpdateAsync(
                operations,
                new ResourceUpdateSettings(),
                true);

            Assert.IsTrue(result.Succeeded);
            Assert.IsTrue(result.UsedLocalFallback);
            CollectionAssert.Contains(operations.Calls, "request-builtin");
            CollectionAssert.Contains(operations.Calls, "manifest:builtin-1");
        }

        /// <summary>
        /// 本地清单仍缺少资源文件时，不得把弱网回退判定为成功。
        /// </summary>
        [Test]
        public async Task LocalContentIncomplete_FallbackFails()
        {
            var operations = new FakeUpdateOperations
            {
                RemoteVersionResult = ResourceOperationResult<string>.Failure("offline"),
                CompletedVersion = "1.0.0",
                LocalContentComplete = false
            };
            operations.ManifestResults.Enqueue(ResourceOperationResult.Success());

            ResourcePackageUpdateResult result = await ResourcePackageUpdateCoordinator.UpdateAsync(
                operations,
                new ResourceUpdateSettings(),
                true);

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains("本地资源内容不完整", result.Error);
        }

        /// <summary>
        /// 远端不可用时，也必须在本地清单加载后优先导入已完成的后台下载文件。
        /// </summary>
        [Test]
        public async Task OfflineFallback_ImportsCompletedDownloadsBeforeCheckingLocalContent()
        {
            var operations = new FakeUpdateOperations
            {
                RemoteVersionResult = ResourceOperationResult<string>.Failure("offline"),
                CompletedVersion = "1.0.0",
                LocalContentComplete = true
            };
            operations.ManifestResults.Enqueue(ResourceOperationResult.Success());

            ResourcePackageUpdateResult result = await ResourcePackageUpdateCoordinator.UpdateAsync(
                operations,
                new ResourceUpdateSettings(),
                true);

            Assert.IsTrue(result.Succeeded);
            CollectionAssert.AreEqual(
                new[]
                {
                    "request-package",
                    "get-completed-version",
                    "manifest:1.0.0",
                    "import-completed",
                    "check-local-content"
                },
                operations.Calls);
        }

        /// <summary>
        /// 后台文件导入失败时，仍应允许普通下载完成当前远端更新。
        /// </summary>
        [Test]
        public async Task RemoteImportFailed_NormalDownloadStillCompletesUpdate()
        {
            var operations = new FakeUpdateOperations
            {
                RemoteVersionResult = ResourceOperationResult<string>.Success("2.0.0"),
                ImportResult = ResourceOperationResult.Failure("background import failed"),
                DownloadResult = ResourceOperationResult.Success()
            };
            operations.ManifestResults.Enqueue(ResourceOperationResult.Success());

            ResourcePackageUpdateResult result = await ResourcePackageUpdateCoordinator.UpdateAsync(
                operations,
                new ResourceUpdateSettings(),
                true);

            Assert.IsTrue(result.Succeeded);
            CollectionAssert.AreEqual(
                new[] { "request-package", "manifest:2.0.0", "import-completed", "download", "save:2.0.0" },
                operations.Calls);
        }

        /// <summary>
        /// 包体内置资源激活只读取版本与清单，不能触发下载、导入或版本记录。
        /// </summary>
        [Test]
        public async Task BuiltinActivation_OnlyLoadsVersionAndManifest()
        {
            var operations = new FakeUpdateOperations
            {
                RemoteVersionResult = ResourceOperationResult<string>.Success("builtin-1")
            };
            operations.ManifestResults.Enqueue(ResourceOperationResult.Success());

            ResourcePackageUpdateResult result = await ResourcePackageUpdateCoordinator.ActivateBuiltinAsync(
                operations,
                new ResourceUpdateSettings());

            Assert.IsTrue(result.Succeeded);
            CollectionAssert.AreEqual(
                new[] { "request-package", "manifest:builtin-1" },
                operations.Calls);
        }

        /// <summary>
        /// 用可编排结果的纯内存实现隔离 YooAsset 网络与文件系统。
        /// </summary>
        private sealed class FakeUpdateOperations : IResourcePackageUpdateOperations, IResourcePackageBackgroundImportOperations
        {
            public readonly List<string> Calls = new();
            public readonly Queue<ResourceOperationResult> ManifestResults = new();

            public ResourceOperationResult<string> RemoteVersionResult { get; set; } =
                ResourceOperationResult<string>.Failure("remote unavailable");

            public ResourceOperationResult<string> BuiltinVersionResult { get; set; } =
                ResourceOperationResult<string>.Failure("builtin unavailable");

            public ResourceOperationResult DownloadResult { get; set; } = ResourceOperationResult.Success();

            public ResourceOperationResult ImportResult { get; set; } = ResourceOperationResult.Success();

            public string CompletedVersion { get; set; } = string.Empty;

            public bool LocalContentComplete { get; set; }

            /// <summary>
            /// 返回预设的主文件系统版本请求结果。
            /// </summary>
            public UniTask<ResourceOperationResult<string>> RequestPackageVersionAsync(int timeoutSeconds)
            {
                Calls.Add("request-package");
                return UniTask.FromResult(RemoteVersionResult);
            }

            /// <summary>
            /// 按测试编排顺序返回清单加载结果。
            /// </summary>
            public UniTask<ResourceOperationResult> LoadManifestAsync(string packageVersion, int timeoutSeconds)
            {
                Calls.Add("manifest:" + packageVersion);
                ResourceOperationResult result = ManifestResults.Count > 0
                    ? ManifestResults.Dequeue()
                    : ResourceOperationResult.Failure("missing manifest result");
                return UniTask.FromResult(result);
            }

            /// <summary>
            /// 返回预设的资源下载结果。
            /// </summary>
            public UniTask<ResourceOperationResult> DownloadAsync(int maximumConcurrency, int retryCount)
            {
                Calls.Add("download");
                return UniTask.FromResult(DownloadResult);
            }

            /// <summary>
            /// 返回预设的后台下载导入结果。
            /// </summary>
            public UniTask<ResourceOperationResult> ImportCompletedAsync()
            {
                Calls.Add("import-completed");
                return UniTask.FromResult(ImportResult);
            }

            /// <summary>
            /// 返回最近一次完整更新成功的版本。
            /// </summary>
            public string GetCompletedVersion()
            {
                Calls.Add("get-completed-version");
                return CompletedVersion;
            }

            /// <summary>
            /// 返回预设的包体内置版本。
            /// </summary>
            public UniTask<ResourceOperationResult<string>> GetBuiltinVersionAsync()
            {
                Calls.Add("request-builtin");
                return UniTask.FromResult(BuiltinVersionResult);
            }

            /// <summary>
            /// 返回本地资源完整性检查结果。
            /// </summary>
            public bool IsLocalContentComplete()
            {
                Calls.Add("check-local-content");
                return LocalContentComplete;
            }

            /// <summary>
            /// 记录完整下载成功后的版本。
            /// </summary>
            public void SaveCompletedVersion(string packageVersion)
            {
                Calls.Add("save:" + packageVersion);
            }
        }
    }
}

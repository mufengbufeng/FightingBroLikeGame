using System;
using System.Reflection;
using System.Threading.Tasks;
using EF.Resource;
using EF.UI.WFramework;
using NUnit.Framework;
using UnityEngine;
using YooAsset;

namespace GameLogic.Tests.EditMode.Framework
{
    /// <summary>
    /// 验证资源管理器可在不初始化 YooAssets 的情况下直接读取 Resources 资源。
    /// </summary>
    [TestFixture]
    public sealed class ResourceManagerBackendTests
    {
        /// <summary>
        /// 关闭 YooAssets 后，资源管理器应跳过包裹初始化并通过统一 Load 接口读取 Resources。
        /// </summary>
        [Test]
        public async Task 关闭YooAssets后_初始化不创建YooAssets且Load读取Resources()
        {
            bool wasYooAssetsInitialized = YooAssets.IsInitialized;
            ResourceModeConfig config = CreateResourcesBackendConfig();
            var manager = new ResourceManager();

            try
            {
                await manager.InitializeAsync(config);

                Assert.IsTrue(manager.IsInitialized);
                Assert.IsFalse(manager.UsesYooAssets);
                if (!wasYooAssetsInitialized)
                {
                    Assert.IsFalse(YooAssets.IsInitialized, "Resources 后端不得初始化 YooAssets。");
                }

                ResourceModeConfig loadedConfig = await manager.Load<ResourceModeConfig>(
                    ResourceModeConfig.DefaultResourcesPath);

                Assert.IsNotNull(loadedConfig);
            }
            finally
            {
                manager.Shutdown();
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        /// <summary>
        /// Resources 后端应能按窗口 id 加载 W-Framework 所需的 Prefab，而不依赖 YooAssets 地址解析。
        /// </summary>
        [TestCase("Bootstrap/BootstrapLoading")]
        public async Task 关闭YooAssets后_WFramework资源加载器可按窗口Id创建窗口(string windowId)
        {
            ResourceModeConfig config = CreateResourcesBackendConfig();
            var manager = new ResourceManager();
            var loader = new WFrameworkResourceLoader(manager);
            GameObject instance = null;

            try
            {
                await manager.InitializeAsync(config);

                instance = await loader.LoadUIObject(windowId);

                Assert.IsNotNull(instance);
            }
            finally
            {
                if (instance != null)
                {
                    loader.UnloadUIObject(instance);
                }

                loader.Dispose();
                manager.Shutdown();
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        /// <summary>
        /// 创建关闭 YooAssets 的临时资源配置，供 Resources 后端测试使用。
        /// </summary>
        private static ResourceModeConfig CreateResourcesBackendConfig()
        {
            FieldInfo useYooAssetsField = typeof(ResourceModeConfig).GetField(
                "_useYooAssets",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(useYooAssetsField, "资源配置必须保存 YooAssets 开关。");

            var config = ScriptableObject.CreateInstance<ResourceModeConfig>();
            Assert.IsTrue(config.UseYooAssets, "资源配置应默认继续启用 YooAssets。");
            useYooAssetsField.SetValue(config, false);
            return config;
        }

    }
}

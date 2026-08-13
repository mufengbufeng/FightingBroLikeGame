using System;
using Cysharp.Threading.Tasks;
using EF.Common;
using UnityEngine;
using UnityEngine.SceneManagement;
using YooAsset;
using SceneHandle = YooAsset.SceneHandle;

namespace EF.Resource
{
    /// <summary>
    /// 资源管理器对外暴露的能力定义。
    /// </summary>
    public interface IResourceManager : IEFManager
    {
        /// <summary>
        /// 当前运行模式。
        /// </summary>
        ResourceMode Mode { get; }

        /// <summary>
        /// 是否已经完成初始化。
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// 当前是否使用 YooAssets 资源包后端。
        /// </summary>
        bool UsesYooAssets { get; }

        /// <summary>
        /// 默认资源包名称。
        /// </summary>
        string DefaultPackageName { get; }

        /// <summary>
        /// 当前使用的配置资产。
        /// </summary>
        ResourceModeConfig Configuration { get; }

        /// <summary>
        /// 移动端后台下载、恢复和缓存导入服务。
        /// </summary>
        IResourceBackgroundDownloadService BackgroundDownloads { get; }

        /// <summary>
        /// 初始化资源模块。
        /// </summary>
        /// <param name="overrideConfig">手动指定的配置，传入 null 时将按默认路径加载。</param>
        /// <param name="progress">初始化进度回调。</param>
        UniTask InitializeAsync(ResourceModeConfig overrideConfig = null, IProgress<float> progress = null);

        /// <summary>
        /// 通过当前配置的资源后端异步加载资源。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="location">资源定位地址；Resources 后端使用 Resources 相对路径。</param>
        /// <param name="progress">加载进度回调。</param>
        /// <param name="priority">YooAssets 后端的加载优先级；Resources 后端忽略该值。</param>
        /// <returns>加载完成的资源对象。</returns>
        UniTask<T> Load<T>(string location, Action<float> progress = null, uint priority = 0)
            where T : UnityEngine.Object;

        /// <summary>
        /// 获取指定名称的资源包。
        /// </summary>
        ResourcePackage GetPackage(string packageName);

        /// <summary>
        /// 获取默认资源包。
        /// </summary>
        ResourcePackage GetDefaultPackage();

        /// <summary>
        /// 异步加载资源。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="location">资源定位地址。</param>
        /// <param name="progress">进度回调。</param>
        /// <param name="priority">加载优先级。</param>
        UniTask<AssetHandle> LoadAssetAsync<T>(string location, Action<float> progress = null, uint priority = 0) where T : UnityEngine.Object;

        /// <summary>
        /// 同步加载资源。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="location">资源定位地址。</param>
        /// <param name="priority">加载优先级。</param>
        AssetHandle LoadAssetSync<T>(string location, uint priority = 0) where T : UnityEngine.Object;

        /// <summary>
        /// 异步加载场景。
        /// </summary>
        /// <param name="location">场景定位地址。</param>
        /// <param name="sceneMode">场景加载模式。</param>
        /// <param name="physicsMode">局部物理模式。</param>
        /// <param name="allowSceneActivation">是否允许场景加载完成后立即激活，语义与 YooAsset 3.0 保持一致。</param>
        /// <param name="priority">加载优先级。</param>
        /// <param name="progress">进度回调。</param>
        UniTask<SceneHandle> LoadSceneAsync(string location, LoadSceneMode sceneMode = LoadSceneMode.Single, LocalPhysicsMode physicsMode = LocalPhysicsMode.None, bool allowSceneActivation = true, uint priority = 0, Action<float> progress = null);

        /// <summary>
        /// 卸载场景。
        /// </summary>
        void UnloadScene(SceneHandle handle);

        /// <summary>
        /// 释放句柄引用。
        /// </summary>
        void Release(HandleBase handle);

        /// <summary>
        /// 释放通过统一 Load 接口取得的资源引用。
        /// Resources 后端不持有额外句柄，因此该调用仅在 YooAssets 后端释放对应引用。
        /// </summary>
        /// <param name="asset">要释放的资源对象。</param>
        void Release(UnityEngine.Object asset);

        /// <summary>
        /// 释放所有由资源管理器追踪的句柄。
        /// </summary>
        void ReleaseAll();
    }
}

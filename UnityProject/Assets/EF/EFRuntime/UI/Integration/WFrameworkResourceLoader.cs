using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using EF.Resource;
using UnityEngine;
using UnityEngine.U2D;

namespace EF.UI.WFramework
{
    /// <summary>
    /// 将 W-Framework 的加载协议桥接到 EF 资源管理器。
    /// </summary>
    internal sealed class WFrameworkResourceLoader : IUILoader, IUIContentBindLoader, IDisposable
    {
        private readonly IResourceManager _resourceManager;
        private readonly object _syncRoot = new();
        private readonly Dictionary<int, UnityEngine.Object> _instanceResources = new();
        private readonly Dictionary<int, Stack<UnityEngine.Object>> _assetResources = new();
        private bool _disposed;

        public WFrameworkResourceLoader(IResourceManager resourceManager)
        {
            _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
        }

        /// <summary>
        /// 按 Loader 命名约定构造上游框架需要的参数结构。
        /// </summary>
        public ParametersForUI GetParameterForUI(string id)
        {
            if (IsDisposed)
            {
                return default;
            }

            return WFrameworkWindowConvention.GetParameterForUI(id);
        }

        /// <summary>
        /// 加载并实例化窗口 Prefab，同时跟踪资源引用。
        /// </summary>
        public UniTask<GameObject> LoadUIObject(string path)
        {
            return LoadGameObjectInternal(path);
        }

        /// <summary>
        /// 销毁窗口实例并释放对应资源引用。
        /// </summary>
        public void UnloadUIObject(GameObject go)
        {
            UnloadGameObject(go);
        }

        /// <summary>
        /// 异步加载单个 Sprite。
        /// </summary>
        public async UniTask<Sprite> LoadSprite(string path)
        {
            if (IsDisposed)
            {
                return null;
            }

            Sprite sprite = await _resourceManager.Load<Sprite>(path);
            if (sprite == null || !TryTrackAsset(sprite, sprite))
            {
                Release(sprite);
                return null;
            }

            return sprite;
        }

        /// <summary>
        /// 从图集加载指定名称的 Sprite。
        /// </summary>
        public async UniTask<Sprite> LoadSprite(string atlasPath, string spriteName)
        {
            if (IsDisposed)
            {
                return null;
            }

            SpriteAtlas atlas = await _resourceManager.Load<SpriteAtlas>(atlasPath);
            Sprite sprite = atlas != null ? atlas.GetSprite(spriteName) : null;
            if (sprite == null || !TryTrackAsset(sprite, atlas))
            {
                Release(atlas);
                return null;
            }

            return sprite;
        }

        /// <summary>
        /// 释放由 <see cref="LoadSprite(string)"/> 或图集加载得到的 Sprite 资源引用。
        /// </summary>
        public void UnloadSprite(Sprite sprite)
        {
            ReleaseAsset(sprite);
        }

        /// <summary>
        /// 异步加载 Texture。
        /// </summary>
        public async UniTask<Texture> LoadTexture(string path)
        {
            if (IsDisposed)
            {
                return null;
            }

            Texture texture = await _resourceManager.Load<Texture>(path);
            if (texture == null || !TryTrackAsset(texture, texture))
            {
                Release(texture);
                return null;
            }

            return texture;
        }

        /// <summary>
        /// 释放由 <see cref="LoadTexture"/> 加载得到的 Texture 资源引用。
        /// </summary>
        public void UnloadTexture(Texture texture)
        {
            ReleaseAsset(texture);
        }

        /// <summary>
        /// 加载并实例化用于动态内容绑定的 Prefab。
        /// </summary>
        public UniTask<GameObject> LoadGameObject(string path)
        {
            return LoadGameObjectInternal(path);
        }

        /// <summary>
        /// 销毁动态内容实例并释放对应资源引用。
        /// </summary>
        public void UnloadGameObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            int instanceId = gameObject.GetInstanceID();
            Release(TakeInstanceResource(instanceId));

            DestroyObject(gameObject);
        }

        /// <summary>
        /// 释放尚未由上游生命周期回收的资源引用。
        /// </summary>
        public void Dispose()
        {
            var resourcesToRelease = new List<UnityEngine.Object>();
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                resourcesToRelease.AddRange(_instanceResources.Values);
                _instanceResources.Clear();

                foreach (Stack<UnityEngine.Object> resources in _assetResources.Values)
                {
                    resourcesToRelease.AddRange(resources);
                }
                _assetResources.Clear();
            }

            foreach (UnityEngine.Object resource in resourcesToRelease)
            {
                Release(resource);
            }
        }

        private async UniTask<GameObject> LoadGameObjectInternal(string path)
        {
            if (IsDisposed)
            {
                return null;
            }

            GameObject prefab = await _resourceManager.Load<GameObject>(path);
            if (prefab == null)
            {
                Release(prefab);
                return null;
            }

            try
            {
                GameObject instance = UnityEngine.Object.Instantiate(prefab);
                if (!TryTrackInstance(instance, prefab))
                {
                    DestroyObject(instance);
                    Release(prefab);
                    return null;
                }

                return instance;
            }
            catch
            {
                Release(prefab);
                throw;
            }
        }

        private bool TryTrackAsset(UnityEngine.Object asset, UnityEngine.Object resource)
        {
            if (asset == null || resource == null)
            {
                return false;
            }

            int instanceId = asset.GetInstanceID();
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return false;
                }

                if (!_assetResources.TryGetValue(instanceId, out Stack<UnityEngine.Object> resources))
                {
                    resources = new Stack<UnityEngine.Object>();
                    _assetResources.Add(instanceId, resources);
                }

                resources.Push(resource);
            }

            return true;
        }

        private bool TryTrackInstance(GameObject instance, UnityEngine.Object resource)
        {
            if (instance == null || resource == null)
            {
                return false;
            }

            int instanceId = instance.GetInstanceID();
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return false;
                }

                _instanceResources.Add(instanceId, resource);
            }

            return true;
        }

        private void ReleaseAsset(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return;
            }

            int instanceId = asset.GetInstanceID();
            UnityEngine.Object resource = null;
            lock (_syncRoot)
            {
                if (!_assetResources.TryGetValue(instanceId, out Stack<UnityEngine.Object> resources)
                    || resources.Count == 0)
                {
                    return;
                }

                resource = resources.Pop();
                if (resources.Count == 0)
                {
                    _assetResources.Remove(instanceId);
                }
            }

            Release(resource);
        }

        private UnityEngine.Object TakeInstanceResource(int instanceId)
        {
            lock (_syncRoot)
            {
                if (!_instanceResources.TryGetValue(instanceId, out UnityEngine.Object resource))
                {
                    return null;
                }

                _instanceResources.Remove(instanceId);
                return resource;
            }
        }

        private bool IsDisposed
        {
            get
            {
                lock (_syncRoot)
                {
                    return _disposed;
                }
            }
        }

        private void Release(UnityEngine.Object resource)
        {
            if (resource != null)
            {
                _resourceManager.Release(resource);
            }
        }

        private static void DestroyObject(GameObject gameObject)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }
    }
}

using System;
using Cysharp.Threading.Tasks;
using EF.Common;
using UnityEngine;

namespace EF.Resource
{
    /// <summary>
    /// 资源加载门面，统一转发到当前注册的资源管理器。
    /// </summary>
    public static class Resource
    {
        /// <summary>
        /// 通过当前资源后端异步加载资源。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="location">资源定位地址。</param>
        /// <param name="progress">加载进度回调。</param>
        /// <param name="priority">YooAssets 后端的加载优先级。</param>
        /// <returns>加载完成的资源对象。</returns>
        public static UniTask<T> Load<T>(string location, Action<float> progress = null, uint priority = 0)
            where T : UnityEngine.Object
        {
            return ModuleSystem.Get<IResourceManager>().Load<T>(location, progress, priority);
        }

        /// <summary>
        /// 释放通过统一 Load 接口取得的资源引用。
        /// </summary>
        /// <param name="asset">要释放的资源对象。</param>
        public static void Release(UnityEngine.Object asset)
        {
            ModuleSystem.Get<IResourceManager>().Release(asset);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace EF.HotFix
{
    [CreateAssetMenu(fileName = "HotFixConfig", menuName = "EasyFramework/HotFixConfig")]
    public class HotFixConfig : ScriptableObject
    {
        [Header("热更新控制")]
        [InspectorName("启用热更新")]
        [Tooltip("关闭后将 GameLogic/GameProto 作为 AOT 本地程序集启动，跳过 AOT 元数据和热更新 DLL。构建配置会自动同步。")]
        [SerializeField]
        private bool _enableHotFix = true;

        /// <summary>
        /// 是否在启动阶段加载 HybridCLR 元数据和热更新程序集。
        /// </summary>
        public bool EnableHotFix => _enableHotFix;

        [Header("热更新DLL配置")]
        [Tooltip("需要加载的热更新DLL列表")]
        public List<string> hotFixDlls = new List<string>
        {
            "GameLogic.dll",
            "GameProto.dll"
        };

        [Header("AOT元数据DLL配置")]
        [Tooltip("需要加载AOT元数据的DLL列表")]
        public List<string> aotMetaDlls = new List<string>
        {
            "mscorlib.dll",
            "System.dll",
            "System.Core.dll",
            "YooAsset.dll",
            "UniTask.dll",
            "EF.Runtime.dll",
            "LitMotion.dll",
            "LitMotion.Extensions.dll",
            "DOTween.dll",
            "Unity.TextMeshPro.dll"
        };

        /// <summary>
        /// 获取当前启动模式需要打包和加载的 DLL 列表。
        /// </summary>
        public List<string> GetAllDlls()
        {
            if (!EnableHotFix)
            {
                return new List<string>();
            }

            var allDlls = new List<string>();
            allDlls.AddRange(hotFixDlls);
            allDlls.AddRange(aotMetaDlls);
            return allDlls;
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace EF.Bootstrap
{
    /// <summary>
    /// 资源系统和 W-Framework 根节点就绪前使用的轻量启动界面。
    /// </summary>
    public static class BootstrapLoadingService
    {
        private const string PrefabResourcesPath = "Bootstrap/BootstrapLoading";
        private const string StatusTextPath = "Panel/StatusText";
        private const string ProgressFillPath = "Panel/ProgressTrack/ProgressFill";
        private const string DefaultStatus = "正在加载";

        private static GameObject _instance;
        private static Text _statusText;
        private static Image _progressFill;

        /// <summary>
        /// 从 Resources 加载并显示启动界面。
        /// </summary>
        public static void Show(string status = DefaultStatus)
        {
            if (_instance == null)
            {
                GameObject prefab = Resources.Load<GameObject>(PrefabResourcesPath);
                if (prefab == null)
                {
                    Debug.LogError($"未找到启动界面 Prefab：Resources/{PrefabResourcesPath}");
                    return;
                }

                _instance = Object.Instantiate(prefab);
                _instance.name = prefab.name;
                Object.DontDestroyOnLoad(_instance);
                CacheComponents();
            }

            _instance.SetActive(true);
            SetProgress(0f, status);
        }

        /// <summary>
        /// 更新启动阶段提示文案。
        /// </summary>
        public static void SetStatus(string status)
        {
            if (_statusText != null)
            {
                _statusText.text = status;
            }
        }

        /// <summary>
        /// 更新启动阶段的可视化进度。
        /// </summary>
        public static void SetProgress(float progress, string status = null)
        {
            if (_progressFill != null)
            {
                _progressFill.fillAmount = Mathf.Clamp01(progress);
            }

            if (!string.IsNullOrEmpty(status))
            {
                SetStatus(status);
            }
        }

        /// <summary>
        /// 在首个 W-Framework 游戏窗口真正可见后销毁启动界面。
        /// </summary>
        public static void Hide()
        {
            if (_instance == null)
            {
                return;
            }

            Object.Destroy(_instance);
            _instance = null;
            _statusText = null;
            _progressFill = null;
        }

        /// <summary>
        /// 缓存 Prefab 内部的动态显示组件。
        /// </summary>
        private static void CacheComponents()
        {
            Transform statusTransform = _instance.transform.Find(StatusTextPath);
            Transform progressTransform = _instance.transform.Find(ProgressFillPath);
            _statusText = statusTransform != null ? statusTransform.GetComponent<Text>() : null;
            _progressFill = progressTransform != null ? progressTransform.GetComponent<Image>() : null;
        }
    }
}

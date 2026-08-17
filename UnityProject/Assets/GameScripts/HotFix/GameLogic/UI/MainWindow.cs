#pragma warning disable 649

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// MainWindow 的序列化组件绑定，由 W-Framework 窗口逻辑持有并管理生命周期。
    /// </summary>
    public class MainWindow : MonoBehaviour
    {
        [SerializeField]
        private Button_Image_Set m_StartExpeditionButton;

        [SerializeField]
        private Button_Image_Set m_ExplorerButton;

        [SerializeField]
        private Button_Image_Set m_RelicLibraryButton;

        [SerializeField]
        private Button_Image_Set m_SettingsButton;

        /// <summary>
        /// 开始远征按钮绑定。
        /// </summary>
        public Button_Image_Set StartExpeditionButton => m_StartExpeditionButton;

        /// <summary>
        /// 探索者按钮绑定。
        /// </summary>
        public Button_Image_Set ExplorerButton => m_ExplorerButton;

        /// <summary>
        /// 遗物库按钮绑定。
        /// </summary>
        public Button_Image_Set RelicLibraryButton => m_RelicLibraryButton;

        /// <summary>
        /// 设置按钮绑定。
        /// </summary>
        public Button_Image_Set SettingsButton => m_SettingsButton;

        /// <summary>
        /// 激活由绑定组件管理的 UI 内容。
        /// </summary>
        public void Open()
        {
        }

        private UnityEvent mOnClear;

        /// <summary>
        /// 清理窗口逻辑添加的 UnityEvent 监听。
        /// </summary>
        public UnityEvent onClear
        {
            get
            {
                if (mOnClear == null)
                {
                    mOnClear = new UnityEvent();
                }

                return mOnClear;
            }
        }

        /// <summary>
        /// 清理绑定组件持有的事件监听。
        /// </summary>
        public void Clear()
        {
            m_StartExpeditionButton?.button?.onClick.RemoveAllListeners();
            m_ExplorerButton?.button?.onClick.RemoveAllListeners();
            m_RelicLibraryButton?.button?.onClick.RemoveAllListeners();
            m_SettingsButton?.button?.onClick.RemoveAllListeners();
            mOnClear?.Invoke();
            mOnClear?.RemoveAllListeners();
        }

        /// <summary>
        /// 单个按钮的序列化组件绑定。
        /// </summary>
        [System.Serializable]
        public class Button_Image_Set
        {
            [SerializeField]
            private GameObject m_GameObject;

            /// <summary>
            /// 按钮节点对象。
            /// </summary>
            public GameObject gameObject => m_GameObject;

            [SerializeField]
            private Button m_button;

            /// <summary>
            /// 按钮组件。
            /// </summary>
            public Button button => m_button;

            [SerializeField]
            private Image m_image;

            /// <summary>
            /// 按钮背景图片组件。
            /// </summary>
            public Image image => m_image;
        }
    }
}

#pragma warning restore 649

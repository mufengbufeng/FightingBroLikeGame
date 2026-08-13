using UnityEngine;

namespace EF.UI.WFramework
{
    /// <summary>
    /// W-Framework UI 的根节点配置。
    /// </summary>
    public class UIRoot : MonoBehaviour
    {

        [SerializeField]
        private Canvas m_RootCanvas;
        [SerializeField]
        private RectTransform m_ParentForUI;
        [SerializeField]
        private int m_LayerForHide;
        [SerializeField]
        private int m_SortingOrderMin = 100;
        [SerializeField]
        private int m_SortingOrderMax = 32767;
        [SerializeField]
        private int m_SortingOrderRangePerUI = 100;
        [SerializeField]
        private float m_PositionZInterval = 1000f;
        [SerializeField]
        private Vector2 m_OffScreenPositionDelta = new Vector2(3000f, 3000f);
        [SerializeField]
        private bool m_StandaloneUpdate;

		public Canvas RootCanvas { get { return m_RootCanvas; } }

        public RectTransform ParentForUI { get { return m_ParentForUI; } }

        public int LayerForShow { get { return mLayerForShow; } }

		public int LayerForHide { get { return m_LayerForHide; } }

        public int SortingOrderMin { get { return m_SortingOrderMin; } }

        public int SortingOrderMax { get { return m_SortingOrderMax; } }

        public int SortingOrderRangePerUI { get { return m_SortingOrderRangePerUI; } }

        public float PositionZInterval { get {  return m_PositionZInterval; } }

        public Vector2 OffScreenPositionDelta { get { return m_OffScreenPositionDelta; } }

        private int mLayerForShow;

        private void Awake()
        {
            try
            {
                ValidateSceneConfiguration();
                mLayerForShow = m_RootCanvas.gameObject.layer;
                UIManager.SetUIRoot(this);
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[WFrameworkUI] UIRoot 场景配置无效：{exception.Message}", this);
                enabled = false;
            }
        }

        /// <summary>
        /// 校验由场景序列化保存的根节点配置。
        /// </summary>
        internal void ValidateSceneConfiguration()
        {
            if (m_RootCanvas == null)
            {
                throw new System.InvalidOperationException("缺少 RootCanvas 引用。");
            }

            if (m_ParentForUI == null)
            {
                throw new System.InvalidOperationException("缺少 ParentForUI 引用。");
            }

            if (m_RootCanvas.gameObject != gameObject)
            {
                throw new System.InvalidOperationException("RootCanvas 必须位于挂载 UIRoot 的同一对象上。");
            }

            if (m_ParentForUI != transform && !m_ParentForUI.IsChildOf(transform))
            {
                throw new System.InvalidOperationException("ParentForUI 必须是 UIRoot 自身或其子节点。");
            }

            if (!m_RootCanvas.overrideSorting)
            {
                throw new System.InvalidOperationException("RootCanvas 必须启用 Override Sorting，以隔离 W-Framework 排序。");
            }

            if (m_LayerForHide < 0 || m_LayerForHide > 31)
            {
                throw new System.InvalidOperationException("LayerForHide 必须是有效的 Unity Layer。");
            }

            if (m_StandaloneUpdate)
            {
                throw new System.InvalidOperationException("ModuleSystem 驱动时必须关闭 Standalone Update。");
            }
        }

        private void Update()
        {
            if (m_StandaloneUpdate)
            {
                UIManager.Update();
            }
        }
    }
}

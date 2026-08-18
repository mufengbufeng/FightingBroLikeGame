using UnityEngine;

namespace GameLogic.GamePlay
{
    /// <summary>
    /// 玩家预制体上的脚部卡边校正范围配置。
    /// </summary>
    public sealed class PlayerFootCatchSettings : MonoBehaviour
    {
        [SerializeField, Min(0f)]
        private float _footCatchDepth = 0.10f;

        [SerializeField, Min(0f)]
        private float _maxSnapCorrection = 0.12f;

        [SerializeField, Min(0f)]
        private float _landingInset = 0.02f;

        [SerializeField]
        private Vector2 _queryPadding = new Vector2(0.24f, 0.24f);
        private static readonly Color QueryRangeColor = new Color(0.15f, 0.85f, 1f, 0.85f);
        private static readonly Color FootCatchRangeColor = new Color(1f, 0.8f, 0.1f, 0.9f);
        private static readonly Color CorrectionRangeColor = new Color(1f, 0.25f, 0.75f, 0.75f);


        /// <summary>
        /// 脚部可陷入平台顶缘的最大深度。
        /// </summary>
        public float FootCatchDepth => _footCatchDepth;

        /// <summary>
        /// 单轴可执行的最大位置校正距离。
        /// </summary>
        public float MaxSnapCorrection => _maxSnapCorrection;

        /// <summary>
        /// 校正后脚部高于平台顶面的间隙。
        /// </summary>
        public float LandingInset => _landingInset;

        /// <summary>
        /// 搜索脚部卡边候选平台的 Bounds 扩展范围。
        /// </summary>
        public Vector2 QueryPadding => _queryPadding;

        /// <summary>
        /// 读取并规整预制体序列化的脚部卡边范围。
        /// </summary>
        /// <returns>可供移动电机使用的无分配范围值。</returns>
        internal PlatformFootCatchConfig CreateConfig()
        {
            float landingInset = Mathf.Max(0f, _landingInset);
            return new PlatformFootCatchConfig(
                Mathf.Max(0f, _footCatchDepth),
                Mathf.Max(landingInset, _maxSnapCorrection),
                landingInset,
                new Vector2(
                    Mathf.Max(0f, _queryPadding.x),
                    Mathf.Max(0f, _queryPadding.y)));
        }
        /// <summary>
        /// 在编辑器选中玩家预制体时绘制候选查询、脚部卡边和最大校正范围。
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying)
            {
                return;
            }

            CapsuleCollider2D capsule = GetComponent<CapsuleCollider2D>();
            if (capsule == null)
            {
                return;
            }

            Bounds bounds = capsule.bounds;
            float footCatchDepth = Mathf.Max(0f, _footCatchDepth);
            float landingInset = Mathf.Max(0f, _landingInset);
            float maxSnapCorrection = Mathf.Max(landingInset, _maxSnapCorrection);
            Vector2 queryPadding = new Vector2(
                Mathf.Max(0f, _queryPadding.x),
                Mathf.Max(0f, _queryPadding.y));

            Gizmos.color = QueryRangeColor;
            Gizmos.DrawWireCube(
                bounds.center,
                bounds.size + new Vector3(queryPadding.x, queryPadding.y, 0f));

            float footRangeHeight = footCatchDepth + landingInset;
            Gizmos.color = FootCatchRangeColor;
            Gizmos.DrawWireCube(
                new Vector3(
                    bounds.center.x,
                    bounds.min.y + (footCatchDepth - landingInset) * 0.5f,
                    bounds.center.z),
                new Vector3(bounds.size.x, footRangeHeight, 0f));

            Gizmos.color = CorrectionRangeColor;
            Gizmos.DrawWireCube(
                bounds.center,
                bounds.size + new Vector3(
                    maxSnapCorrection * 2f,
                    maxSnapCorrection * 2f,
                    0f));
        }
    }
}

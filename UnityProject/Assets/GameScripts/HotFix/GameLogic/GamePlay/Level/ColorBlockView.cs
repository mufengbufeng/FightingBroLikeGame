using UnityEngine;

namespace GameLogic.GamePlay
{
    /// <summary>
    /// 默认白色色块。Awake 时补齐缺失 Sprite，并应用排序层。
    /// </summary>
    public sealed class ColorBlockView : MonoBehaviour
    {
        [SerializeField] private string _sortingLayerName;

        private void Awake()
        {
            SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                return;
            }

            if (spriteRenderer.sprite == null)
            {
                spriteRenderer.sprite = GamePlaySprites.White;
            }

            if (!string.IsNullOrEmpty(_sortingLayerName))
            {
                spriteRenderer.sortingLayerName = _sortingLayerName;
            }
        }

        /// <summary>
        /// 配置排序层，供工厂写入序列化字段。
        /// </summary>
        public void Configure(string sortingLayerName)
        {
            _sortingLayerName = sortingLayerName;
        }
    }
}

using UnityEngine;

namespace GameLogic.GamePlay
{
    /// <summary>
    /// GamePlay 运行时生成的默认色块贴图。
    /// </summary>
    public static class GamePlaySprites
    {
        private static Sprite _white;

        /// <summary>
        /// 1x1 白色 Sprite，世界尺寸由 localScale 决定。
        /// </summary>
        public static Sprite White
        {
            get
            {
                if (_white != null)
                {
                    return _white;
                }

                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    name = "GamePlayWhite",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply(false, true);

                _white = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, 1f, 1f),
                    new Vector2(0.5f, 0.5f),
                    1f);
                _white.name = "GamePlayWhite";
                _white.hideFlags = HideFlags.HideAndDontSave;
                return _white;
            }
        }
    }
}

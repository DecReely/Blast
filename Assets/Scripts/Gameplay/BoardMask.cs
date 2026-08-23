using UnityEngine;

namespace Blast.Gameplay
{
    /// <summary>
    /// Clips board items to the playfield.
    ///
    /// Refilled cubes are stacked above the grid and fall in, so without a mask they are visible
    /// hovering over the background before they enter the board. Masking clips them properly as they
    /// cross the top edge, instead of popping into existence.
    ///
    /// The mask sprite is a plain square, stretched to the grid. A rounded sprite cannot be used
    /// here: <see cref="SpriteMask"/> has no 9-slicing, so scaling one to board size scales its
    /// corner radius too and the playfield ends up clipped to an ellipse. The rounded frame drawn on
    /// top is what gives the board its shape.
    /// </summary>
    [RequireComponent(typeof(SpriteMask))]
    public sealed class BoardMask : MonoBehaviour
    {
        [Header("Padding around the grid, in cells")]
        [SerializeField] private float _horizontalPadding = 0.28f;
        [SerializeField] private float _bottomPadding = 0.32f;
        [SerializeField] private float _topPadding = 0.32f;

        private static Sprite _squareSprite;

        private SpriteMask _mask;

        /// <summary>
        /// A 1x1 opaque square built from Unity's built-in white texture, shared by every instance.
        /// Avoids shipping a texture asset whose only job is to be solid white.
        /// </summary>
        private static Sprite SquareSprite
        {
            get
            {
                if (_squareSprite == null)
                {
                    var texture = Texture2D.whiteTexture;
                    _squareSprite = Sprite.Create(
                        texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        texture.width);
                    _squareSprite.name = "BoardMaskSquare";
                }

                return _squareSprite;
            }
        }

        private void Awake()
        {
            EnsureMask();
        }

        /// <summary>Stretches the mask to cover the board.</summary>
        public void Fit(Board board)
        {
            EnsureMask();

            var spriteSize = _mask.sprite.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f)
            {
                return;
            }

            var width = board.Width + (_horizontalPadding * 2f);
            var height = board.Height + _bottomPadding + _topPadding;

            transform.localScale = new Vector3(width / spriteSize.x, height / spriteSize.y, 1f);

            var verticalShift = (_topPadding - _bottomPadding) * 0.5f;
            transform.position = board.CenterWorld + new Vector3(0f, verticalShift, 0f);
        }

        private void EnsureMask()
        {
            if (_mask == null)
            {
                _mask = GetComponent<SpriteMask>();
            }

            _mask.sprite = SquareSprite;
        }
    }
}

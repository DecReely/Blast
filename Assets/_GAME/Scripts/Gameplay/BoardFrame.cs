using UnityEngine;

namespace Blast.Gameplay
{
    /// <summary>
    /// Stretches the 9-sliced board background around whatever size the current level's grid is.
    ///
    /// Item art overhangs its cell slightly on the top and bottom edges, so the vertical padding is a
    /// little larger than the horizontal to keep the inset looking even.
    /// </summary>
    public sealed class BoardFrame : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;

        [Header("Padding around the grid, in cells")]
        [SerializeField] private float _horizontalPadding = 0.3f;
        [SerializeField] private float _bottomPadding = 0.34f;
        [SerializeField] private float _topPadding = 0.34f;

        private void Awake()
        {
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }
        }

        /// <summary>Resizes and recentres the frame to enclose the board.</summary>
        public void Fit(Board board)
        {
            if (_spriteRenderer == null)
            {
                return;
            }

            _spriteRenderer.size = new Vector2(
                board.Width + (_horizontalPadding * 2f),
                board.Height + _bottomPadding + _topPadding);

            // Uneven vertical padding shifts the frame's centre off the board's centre by half the
            // difference.
            var verticalShift = (_topPadding - _bottomPadding) * 0.5f;
            transform.position = board.CenterWorld + new Vector3(0f, verticalShift, 0f);
        }
    }
}

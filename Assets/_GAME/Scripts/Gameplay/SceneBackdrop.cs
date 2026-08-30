using UnityEngine;

namespace Blast.Gameplay
{
    /// <summary>
    /// Fills the camera's view with a backdrop sprite sitting behind everything else.
    ///
    /// A world sprite rather than a Canvas image, because the level's canvas renders in
    /// Screen Space - Overlay: that mode always draws over the camera's own output, so a backdrop
    /// placed in it would cover the board instead of sitting behind it.
    ///
    /// The sprite is scaled to <em>cover</em> the viewport and never stretched to it. One uniform
    /// scale taken from the larger of the two axis ratios fills the screen on any shape while keeping
    /// the artwork's proportions — the same behaviour the main menu gets from an
    /// <see cref="UnityEngine.UI.AspectRatioFitter"/> in envelope mode, which has no world-space
    /// equivalent.
    ///
    /// Covering the view is the only thing this owns. How far back it sits and how far it is dimmed
    /// are the renderer's own sorting order and colour, left authored in the scene: they are what an
    /// artist actually wants to nudge, and the inspector already previews both without this having to
    /// mirror them.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SceneBackdrop : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;

        [Tooltip("Camera to cover. Falls back to the main camera.")]
        [SerializeField] private Camera _camera;

        [Tooltip("Covers a little more than the viewport, so rounding can never leave a hairline " +
                 "of camera background along an edge.")]
        [SerializeField] private float _overscan = 1.02f;

        /// <summary>What the current scale was computed for; see <see cref="LateUpdate"/>.</summary>
        private float _appliedAspect = -1f;

        private float _appliedOrthographicSize = -1f;

        private void OnEnable()
        {
            Fit();
        }

        /// <summary>
        /// Refits when the view changes shape or zoom. Both matter: the aspect covers device rotation
        /// and resizing the editor's Game view, while the orthographic size is recomputed from that
        /// aspect by <see cref="BoardCamera"/> in its own LateUpdate. Whichever of the two runs first,
        /// comparing against what was last applied means the backdrop catches up on the next frame
        /// rather than being left sized for the old viewport.
        /// </summary>
        private void LateUpdate()
        {
            if (_camera == null)
            {
                return;
            }

            if (!Mathf.Approximately(_camera.aspect, _appliedAspect) ||
                !Mathf.Approximately(_camera.orthographicSize, _appliedOrthographicSize))
            {
                Fit();
            }
        }

        /// <summary>
        /// Scales the backdrop to cover the camera. Public so editor tooling that renders the scene
        /// offscreen can apply it without entering play mode.
        /// </summary>
        public void Fit()
        {
            EnsureReferences();

            if (_spriteRenderer == null || _camera == null)
            {
                return;
            }

            var sprite = _spriteRenderer.sprite;
            if (sprite == null)
            {
                return;
            }

            var spriteSize = sprite.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f)
            {
                return;
            }

            // orthographicSize is half the visible height; the aspect turns that into the width.
            var viewHeight = _camera.orthographicSize * 2f;
            var viewWidth = viewHeight * _camera.aspect;

            var scale = Mathf.Max(viewWidth / spriteSize.x, viewHeight / spriteSize.y) * _overscan;
            transform.localScale = new Vector3(scale, scale, 1f);

            // Centred on the camera's axis. Depth is left alone so the backdrop can be pushed back in
            // the scene without this fighting the author over it.
            var view = _camera.transform.position;
            transform.position = new Vector3(view.x, view.y, transform.position.z);

            _appliedAspect = _camera.aspect;
            _appliedOrthographicSize = _camera.orthographicSize;
        }

        private void EnsureReferences()
        {
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (_camera == null)
            {
                _camera = Camera.main;
            }
        }
    }
}

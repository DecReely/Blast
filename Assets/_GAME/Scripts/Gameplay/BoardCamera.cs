using UnityEngine;

namespace Blast.Gameplay
{
    /// <summary>
    /// Sizes the orthographic camera so that a cell is always the same size on screen.
    ///
    /// Deliberately <em>not</em> a fit-to-board camera: the zoom never depends on the level, so a
    /// 6x6 level and a 10x9 level draw identically sized cubes and the board simply occupies less of
    /// the screen when it is small. The camera shows a fixed number of cells across and derives its
    /// orthographic size from the aspect ratio, which keeps that guarantee on any screen shape
    /// instead of only at exactly 9:16.
    /// </summary>
    public sealed class BoardCamera : MonoBehaviour
    {
        [SerializeField] private Camera _camera;

        [Tooltip("How many cells fit across the screen width. The widest level is 10 cells, so " +
                 "anything above that leaves a margin at the sides.")]
        [SerializeField] private float _visibleCellsAcross = 11f;

        private float _appliedAspect = -1f;

        private void Awake()
        {
            if (_camera == null)
            {
                _camera = GetComponent<Camera>();
            }

            Apply();
        }

        /// <summary>
        /// Recomputes when the viewport changes shape, which covers both device rotation and simply
        /// resizing the editor's Game view.
        /// </summary>
        private void LateUpdate()
        {
            if (!Mathf.Approximately(_camera.aspect, _appliedAspect))
            {
                Apply();
            }
        }

        /// <summary>
        /// Recomputes the orthographic size for the camera's current aspect ratio. Public so editor
        /// tooling that renders the board offscreen can apply it without entering play mode.
        /// </summary>
        public void Apply()
        {
            var aspect = _camera.aspect;
            if (aspect <= 0f)
            {
                return;
            }

            // orthographicSize is half the visible height; halving the target width and dividing by
            // the aspect converts it into the matching height.
            _camera.orthographic = true;
            _camera.orthographicSize = _visibleCellsAcross * 0.5f / aspect;
            _appliedAspect = aspect;
        }
    }
}

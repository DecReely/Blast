using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Blast.UI
{
    /// <summary>
    /// Sends a chalice flying from the box that gave it up to the goal counter in the top bar.
    ///
    /// Runs on the Canvas rather than on the board, because the destination is a UI element: the
    /// board position is projected to screen space once at launch and the flight happens entirely in
    /// screen coordinates, so it lands accurately whatever the camera is doing.
    ///
    /// A single collection can yield up to ten chalices, so the number actually flown is capped —
    /// beyond a handful the effect reads as a stream anyway and the extra objects add nothing.
    /// </summary>
    public sealed class ChaliceFlightView : MonoBehaviour
    {
        [SerializeField] private Image _chalicePrefab;
        [SerializeField] private RectTransform _container;

        [Header("Flight")]
        [SerializeField] private float _duration = 0.5f;
        [SerializeField] private float _staggerPerChalice = 0.06f;

        [Tooltip("Most chalices shown for one collection, however many were actually taken.")]
        [SerializeField] private int _maxConcurrent = 4;

        [Tooltip("How far the chalice is thrown sideways on the way, giving the flight an arc.")]
        [SerializeField] private float _arcSpread = 90f;

        [Tooltip("Eased so the chalice drifts out of the board before accelerating to the counter.")]
        [SerializeField]
        private AnimationCurve _ease = new(
            new Keyframe(0f, 0f),
            new Keyframe(0.35f, 0.18f),
            new Keyframe(1f, 1f));

        private Camera _boardCamera;

        public void Initialize(Camera boardCamera)
        {
            _boardCamera = boardCamera;
        }

        /// <summary>Launches chalices from a board position towards <paramref name="targetScreenPosition"/>.</summary>
        public void Fly(int count, Vector3 fromWorldPosition, Vector3 targetScreenPosition)
        {
            if (_chalicePrefab == null || _container == null || _boardCamera == null)
            {
                return;
            }

            var origin = RectTransformUtility.WorldToScreenPoint(_boardCamera, fromWorldPosition);
            var shown = Mathf.Clamp(count, 1, _maxConcurrent);

            for (var i = 0; i < shown; i++)
            {
                StartCoroutine(FlyOne(origin, targetScreenPosition, i * _staggerPerChalice));
            }
        }

        private IEnumerator FlyOne(Vector2 origin, Vector3 target, float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            // The template is kept inactive in the scene so it is never drawn; clones must be woken.
            var chalice = Instantiate(_chalicePrefab, _container);
            chalice.gameObject.SetActive(true);
            chalice.transform.position = origin;

            // A sideways control point turns the straight line into a curve, so several chalices
            // launched together fan out instead of overlapping.
            var sideways = Random.Range(-_arcSpread, _arcSpread);
            var control = ((Vector3)origin + target) * 0.5f + new Vector3(sideways, _arcSpread, 0f);

            var elapsed = 0f;

            while (elapsed < _duration)
            {
                elapsed += Time.deltaTime;
                var progress = _ease.Evaluate(Mathf.Clamp01(elapsed / _duration));

                // Quadratic Bezier through the control point.
                var a = Vector3.Lerp(origin, control, progress);
                var b = Vector3.Lerp(control, target, progress);
                chalice.transform.position = Vector3.Lerp(a, b, progress);

                yield return null;
            }

            Destroy(chalice.gameObject);
        }
    }
}

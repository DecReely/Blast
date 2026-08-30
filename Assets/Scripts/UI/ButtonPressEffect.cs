using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Blast.UI
{
    /// <summary>
    /// Presses a button's face down into its frame while it is held, and lets it spring back out when
    /// released.
    ///
    /// Only the face moves, never the button itself. The frame behind it stays put, so the press
    /// reads as a cap sinking into a socket rather than the whole control shrinking away from the
    /// finger — which is the difference between a button that feels physical and one that just gets
    /// smaller.
    ///
    /// It lives on the button root rather than on the artwork. The graphics are children, and the
    /// event system walks up to the first handler it finds, so a tap anywhere on the button reaches
    /// this regardless of which piece of the artwork was actually hit.
    ///
    /// The motion is a hand-written curve rather than an animation clip, matching how the rest of the
    /// game animates. The release deliberately overshoots: easing straight back to rest feels soft,
    /// while a small bounce past full size reads as the button popping back out.
    /// </summary>
    public sealed class ButtonPressEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        /// <summary>Child looked for when no face is assigned, so the component works dropped on.</summary>
        private const string FaceChildName = "Face";

        [Tooltip("The part of the button that moves. Falls back to a child named 'Face'.")]
        [SerializeField] private RectTransform _face;

        [Header("Held")]
        [SerializeField] private float _pressedScale = 0.95f;

        [Tooltip("Pixels the face sinks towards the bottom of its frame while held.")]
        [SerializeField] private float _pressedOffset = 7f;

        [Tooltip("Kept very short, so the button answers the finger instead of trailing behind it.")]
        [SerializeField] private float _pressDuration = 0.06f;

        [Header("Released")]
        [Tooltip("Longer than the press: the button gives way at once but takes its time coming back.")]
        [SerializeField] private float _releaseDuration = 0.26f;

        [Tooltip("Passes above one on the way back, so the face overshoots full size and settles.")]
        [SerializeField] private AnimationCurve _releaseCurve = DefaultReleaseCurve();

        private Selectable _selectable;
        private Vector2 _restPosition;
        private Coroutine _animation;

        private void Awake()
        {
            // Optional: the effect is useful on anything tappable, not only on a Button.
            _selectable = GetComponent<Selectable>();

            if (_face == null)
            {
                _face = transform.Find(FaceChildName) as RectTransform;
            }

            if (_face == null)
            {
                Debug.LogError($"[ButtonPressEffect] {name} has no face to animate.", this);
                enabled = false;
                return;
            }

            // Wherever the scene author put the face is the rest pose, so the effect adds to a
            // hand-placed button instead of snapping it to an assumed origin.
            _restPosition = _face.anchoredPosition;

            // A curve left empty in the inspector evaluates to zero everywhere, which would collapse
            // the face rather than release it. Falling back keeps a half-configured component
            // harmless instead of visibly broken.
            if (_releaseCurve == null || _releaseCurve.length == 0)
            {
                _releaseCurve = DefaultReleaseCurve();
            }
        }

        private void OnDisable()
        {
            // Coroutines do not survive the object being disabled, so without this the face would be
            // left frozen mid-press the next time it is shown.
            _animation = null;

            if (_face != null)
            {
                Apply(1f, _restPosition);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // A button that cannot be pressed must not look like it can.
            if (_selectable != null && !_selectable.IsInteractable())
            {
                return;
            }

            Play(_pressedScale, _restPosition + new Vector2(0f, -_pressedOffset), _pressDuration, null);
        }

        /// <summary>
        /// Raised on the object that received the press even when the finger has since moved off it,
        /// so a drag away from the button still releases it rather than leaving it stuck down.
        /// </summary>
        public void OnPointerUp(PointerEventData eventData)
        {
            Play(1f, _restPosition, _releaseDuration, _releaseCurve);
        }

        private void Play(float toScale, Vector2 toPosition, float duration, AnimationCurve curve)
        {
            if (_animation != null)
            {
                StopCoroutine(_animation);
                _animation = null;
            }

            if (duration <= 0f)
            {
                Apply(toScale, toPosition);
                return;
            }

            _animation = StartCoroutine(Animate(toScale, toPosition, duration, curve));
        }

        /// <summary>
        /// Runs from wherever the face currently is, so tapping again mid-bounce carries on from that
        /// pose instead of snapping back to rest first.
        /// </summary>
        private IEnumerator Animate(float toScale, Vector2 toPosition, float duration, AnimationCurve curve)
        {
            var fromScale = _face.localScale.x;
            var fromPosition = _face.anchoredPosition;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                // Unscaled, so button feedback stays responsive whatever the game's time scale does.
                elapsed += Time.unscaledDeltaTime;

                var progress = Mathf.Clamp01(elapsed / duration);
                var eased = curve != null ? curve.Evaluate(progress) : SmoothStep(progress);

                // Unclamped, because the release curve is allowed to pass one on its way back and
                // clamping is exactly what would flatten the overshoot away.
                Apply(
                    Mathf.LerpUnclamped(fromScale, toScale, eased),
                    Vector2.LerpUnclamped(fromPosition, toPosition, eased));

                yield return null;
            }

            Apply(toScale, toPosition);
            _animation = null;
        }

        private void Apply(float scale, Vector2 position)
        {
            _face.localScale = new Vector3(scale, scale, 1f);
            _face.anchoredPosition = position;
        }

        /// <summary>Eased at both ends, so the press has no hard start or stop.</summary>
        private static float SmoothStep(float progress)
        {
            return progress * progress * (3f - (2f * progress));
        }

        /// <summary>
        /// Out past full size, a shallow dip back under it, then rest: two settling steps rather than
        /// one, which is what stops the bounce reading as a single mechanical bump.
        /// </summary>
        private static AnimationCurve DefaultReleaseCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.45f, 1.12f),
                new Keyframe(0.72f, 0.97f),
                new Keyframe(1f, 1f));
        }
    }
}

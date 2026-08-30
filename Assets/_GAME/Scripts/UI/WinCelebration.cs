using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Blast.UI
{
    /// <summary>
    /// The win sequence: a star spins up over a dimmed board inside a burst of sparkles, then waits
    /// for the player to tap before handing back to the caller.
    ///
    /// The sparkles are Canvas images rather than a <see cref="ParticleSystem"/>. A Screen Space
    /// Overlay canvas always draws over world geometry, so real particles would sit behind the dim;
    /// the alternative — moving the canvas to Screen Space Camera — would put every UI element into
    /// world coordinates and break the chalice flight's screen-space maths for no visual gain.
    ///
    /// Animated by hand in a coroutine for the same reason the board is, and the completion callback
    /// keeps the decision about what happens next in the flow controller rather than here.
    /// </summary>
    public sealed class WinCelebration : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private RectTransform _star;
        [SerializeField] private Text _title;

        [Tooltip("Whole-screen button: the case study's reference ends on 'Tap To Continue'.")]
        [SerializeField] private Button _continueButton;

        [SerializeField] private Text _continuePrompt;

        [Header("Sparkles")]
        [SerializeField] private Image _sparklePrefab;
        [SerializeField] private RectTransform _sparkleContainer;
        [SerializeField] private int _sparkleCount = 14;
        [SerializeField] private float _sparkleTravel = 460f;
        [SerializeField] private float _sparkleDuration = 0.9f;

        [Header("Timing")]
        [SerializeField] private float _starDuration = 0.55f;

        [SerializeField] private float _spinDegrees = 380f;

        [Tooltip("Star scale over the sequence, overshooting before it settles.")]
        [SerializeField]
        private AnimationCurve _scaleCurve = new(
            new Keyframe(0f, 0f),
            new Keyframe(0.65f, 1.15f),
            new Keyframe(1f, 1f));

        private Action _onComplete;

        private void Awake()
        {
            if (_continueButton != null)
            {
                _continueButton.onClick.AddListener(Continue);
            }

            Hide();
        }

        public void Hide()
        {
            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        /// <summary>Plays the sequence and invokes <paramref name="onComplete"/> once tapped.</summary>
        public void Play(Action onComplete)
        {
            if (_root == null)
            {
                onComplete?.Invoke();
                return;
            }

            _onComplete = onComplete;

            _root.SetActive(true);
            StartCoroutine(Sequence());
        }

        private IEnumerator Sequence()
        {
            if (_title != null)
            {
                _title.text = "Perfect!";
            }

            // The prompt is withheld until the animation has played, so the player is not invited to
            // tap through the celebration before seeing it.
            SetContinueEnabled(false);
            BurstSparkles();

            var elapsed = 0f;

            while (elapsed < _starDuration)
            {
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / _starDuration);

                if (_star != null)
                {
                    var scale = _scaleCurve.Evaluate(progress);
                    _star.localScale = new Vector3(scale, scale, 1f);
                    _star.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-_spinDegrees, 0f, progress));
                }

                yield return null;
            }

            if (_star != null)
            {
                _star.localScale = Vector3.one;
                _star.localRotation = Quaternion.identity;
            }

            SetContinueEnabled(true);
        }

        private void SetContinueEnabled(bool enabled)
        {
            if (_continueButton != null)
            {
                _continueButton.interactable = enabled;
            }

            if (_continuePrompt != null)
            {
                _continuePrompt.enabled = enabled;
            }
        }

        private void Continue()
        {
            // Cleared first so a second tap in the same frame cannot advance twice.
            var callback = _onComplete;
            _onComplete = null;

            SetContinueEnabled(false);
            callback?.Invoke();
        }

        /// <summary>Throws sparkles outwards from the star in an even ring, each slightly varied.</summary>
        private void BurstSparkles()
        {
            if (_sparklePrefab == null || _sparkleContainer == null)
            {
                return;
            }

            for (var i = 0; i < _sparkleCount; i++)
            {
                var angle = (i / (float)_sparkleCount * Mathf.PI * 2f) + Random.Range(-0.2f, 0.2f);
                var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

                StartCoroutine(AnimateSparkle(direction * Random.Range(0.6f, 1f) * _sparkleTravel));
            }
        }

        private IEnumerator AnimateSparkle(Vector3 offset)
        {
            // The template is kept inactive in the scene so it is never drawn; clones must be woken.
            var sparkle = Instantiate(_sparklePrefab, _sparkleContainer);
            sparkle.gameObject.SetActive(true);

            var rect = sparkle.rectTransform;
            rect.anchoredPosition = Vector2.zero;

            var elapsed = 0f;
            var duration = _sparkleDuration * Random.Range(0.8f, 1.2f);
            var spin = Random.Range(-200f, 200f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);

                // Decelerating flight outwards, fading and shrinking as it goes.
                var eased = 1f - ((1f - progress) * (1f - progress));
                rect.anchoredPosition = offset * eased;
                rect.localScale = Vector3.one * Mathf.Lerp(1.1f, 0.2f, progress);
                rect.localRotation = Quaternion.Euler(0f, 0f, spin * eased);

                var color = sparkle.color;
                color.a = 1f - progress;
                sparkle.color = color;

                yield return null;
            }

            Destroy(sparkle.gameObject);
        }
    }
}

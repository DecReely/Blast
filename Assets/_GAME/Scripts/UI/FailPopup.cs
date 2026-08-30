using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Blast.UI
{
    /// <summary>
    /// Shown when the moves run out. Offers the two choices the case study asks for: close to return
    /// to the main menu, or try the level again.
    ///
    /// The pop-in is a short hand-written scale curve rather than an animation clip, matching how the
    /// rest of the game animates.
    /// </summary>
    public sealed class FailPopup : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private RectTransform _panel;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _retryButton;

        [Header("Pop-in")]
        [SerializeField] private float _duration = 0.28f;

        [Tooltip("Overshoots past full size before settling, so the popup lands with some weight.")]
        [SerializeField]
        private AnimationCurve _scaleCurve = new(
            new Keyframe(0f, 0f),
            new Keyframe(0.7f, 1.08f),
            new Keyframe(1f, 1f));

        private void Awake()
        {
            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(SceneLoader.GoToMainMenu);
            }

            if (_retryButton != null)
            {
                _retryButton.onClick.AddListener(SceneLoader.GoToLevel);
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

        public void Show()
        {
            if (_root == null)
            {
                return;
            }

            _root.SetActive(true);
            StartCoroutine(PopIn());
        }

        private IEnumerator PopIn()
        {
            if (_panel == null)
            {
                yield break;
            }

            var elapsed = 0f;

            while (elapsed < _duration)
            {
                elapsed += Time.deltaTime;
                var scale = _scaleCurve.Evaluate(Mathf.Clamp01(elapsed / _duration));
                _panel.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            _panel.localScale = Vector3.one;
        }
    }
}

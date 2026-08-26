using UnityEngine;
using UnityEngine.UI;

namespace Blast.UI
{
    /// <summary>
    /// One goal in the top bar: an obstacle icon with how many are left, replaced by a tick once
    /// that goal is met.
    ///
    /// Kept as its own component so the top bar does not care which goals a level has — it simply
    /// shows or hides one of these per obstacle type.
    /// </summary>
    public sealed class GoalItemView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Text _countLabel;
        [SerializeField] private Image _checkmark;

        /// <summary>World position of the icon, used as the target for collected chalices.</summary>
        public Vector3 IconWorldPosition => _icon != null ? _icon.transform.position : transform.position;

        public void SetIcon(Sprite sprite)
        {
            if (_icon != null)
            {
                _icon.sprite = sprite;
            }
        }

        /// <summary>Updates the count, swapping to a tick when nothing is left.</summary>
        public void SetRemaining(int remaining)
        {
            var complete = remaining <= 0;

            if (_countLabel != null)
            {
                _countLabel.enabled = !complete;
                _countLabel.text = remaining.ToString();
            }

            if (_checkmark != null)
            {
                _checkmark.enabled = complete;
            }
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }
    }
}

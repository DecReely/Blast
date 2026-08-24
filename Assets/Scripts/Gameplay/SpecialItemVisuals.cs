using Blast.Items;
using UnityEngine;

namespace Blast.Gameplay
{
    /// <summary>
    /// Spawns the short-lived visuals an explosion needs, currently the two halves a rocket splits
    /// into.
    ///
    /// The half sprites live here rather than on the <see cref="Rocket"/> prefab because combos fire
    /// rockets that were never items on the board — the TNT-Rocket combo launches six of them — and
    /// they need the same artwork. Keeping one owner avoids the two paths drifting apart.
    ///
    /// A rocket half is not a board item either: it occupies no cell and cannot be tapped.
    /// </summary>
    public sealed class SpecialItemVisuals : MonoBehaviour
    {
        [Tooltip("Sprite-only prefab used for each half of a splitting rocket.")]
        [SerializeField] private SpriteRenderer _rocketHalfPrefab;

        [Header("Rocket halves")]
        [SerializeField] private Sprite _horizontalLeft;
        [SerializeField] private Sprite _horizontalRight;
        [SerializeField] private Sprite _verticalDown;
        [SerializeField] private Sprite _verticalUp;

        /// <summary>
        /// Creates the half travelling in the given direction at a world position. Returns null if no
        /// prefab is configured, which callers treat as "run the sweep without a visual".
        /// </summary>
        /// <param name="positive">True for the half heading right or up.</param>
        public Transform SpawnRocketHalf(Rocket.Axis axis, bool positive, Vector3 position)
        {
            if (_rocketHalfPrefab == null)
            {
                return null;
            }

            var instance = Instantiate(_rocketHalfPrefab, position, Quaternion.identity, transform);
            instance.sprite = SpriteFor(axis, positive);
            return instance.transform;
        }

        private Sprite SpriteFor(Rocket.Axis axis, bool positive)
        {
            if (axis == Rocket.Axis.Horizontal)
            {
                return positive ? _horizontalRight : _horizontalLeft;
            }

            return positive ? _verticalUp : _verticalDown;
        }
    }
}

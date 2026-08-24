using UnityEngine;

namespace Blast.Gameplay
{
    /// <summary>
    /// Spawns the short-lived visuals an explosion needs, currently the two halves a rocket splits
    /// into.
    ///
    /// A rocket half is not a board item — it occupies no cell and cannot be tapped — so it is
    /// created here rather than through the item catalog, which exists only for things that live on
    /// the grid.
    /// </summary>
    public sealed class SpecialItemVisuals : MonoBehaviour
    {
        [Tooltip("Sprite-only prefab used for each half of a splitting rocket.")]
        [SerializeField] private SpriteRenderer _rocketHalfPrefab;

        /// <summary>
        /// Creates a rocket half showing <paramref name="sprite"/> at a world position. Returns null
        /// if no prefab is configured, which callers treat as "run the sweep without a visual".
        /// </summary>
        public Transform SpawnRocketHalf(Sprite sprite, Vector3 position)
        {
            if (_rocketHalfPrefab == null)
            {
                return null;
            }

            var instance = Instantiate(_rocketHalfPrefab, position, Quaternion.identity, transform);
            instance.sprite = sprite;
            return instance.transform;
        }
    }
}

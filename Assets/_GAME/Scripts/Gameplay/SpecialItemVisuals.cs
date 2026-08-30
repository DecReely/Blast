using Blast.Items;
using UnityEngine;

namespace Blast.Gameplay
{
    /// <summary>
    /// Owns the presentation a special item needs but cannot supply itself.
    ///
    /// Everything here belongs to a moment rather than to an instance: a combo fires rockets that
    /// were never items on the board — the TNT-Rocket combo launches six of them — and both the
    /// combo blast and the puff a newly created special arrives in happen when no item exists to
    /// carry the prefab. Keeping one owner stops the item and combo paths drifting apart.
    ///
    /// The half's own artwork is deliberately not here: it lives on the half prefab, so a rocket
    /// half is a single asset that fully describes how it looks.
    /// </summary>
    public sealed class SpecialItemVisuals : MonoBehaviour
    {
        [Tooltip("Prefab spawned for each half of a splitting rocket. It supplies its own artwork.")]
        [SerializeField] private RocketHalfView _rocketHalfPrefab;

        [Header("One-shot effects")]
        [Tooltip("Played at the centre of a combo, whose members are consumed without exploding.")]
        [SerializeField] private ParticleSystem _comboEffectPrefab;

        [Tooltip("Played where a blast collapses into a new special item.")]
        [SerializeField] private ParticleSystem _specialCreatedEffectPrefab;

        public ParticleSystem ComboEffectPrefab => _comboEffectPrefab;

        public ParticleSystem SpecialCreatedEffectPrefab => _specialCreatedEffectPrefab;

        /// <summary>
        /// Creates the half travelling in the given direction at a world position. Returns null if no
        /// prefab is configured, which callers treat as "run the sweep without a visual".
        /// </summary>
        /// <param name="positive">True for the half heading right or up.</param>
        public RocketHalfView SpawnRocketHalf(Rocket.Axis axis, bool positive, Vector3 position)
        {
            if (_rocketHalfPrefab == null)
            {
                return null;
            }

            var instance = Instantiate(_rocketHalfPrefab, position, Quaternion.identity, transform);
            instance.Show(axis, positive);
            return instance;
        }
    }
}

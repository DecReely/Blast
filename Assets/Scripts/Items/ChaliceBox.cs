using UnityEngine;

namespace Blast.Items
{
    /// <summary>
    /// Chalice box. The only multi-cell item: a static 2x2 obstacle authored in the level files as
    /// four corner codes.
    ///
    /// It clears in two phases. While the doors are intact every damage source deals exactly one
    /// damage regardless of how many of its four cells were hit. Once the doors are destroyed each
    /// point of damage collects one chalice, and damage is counted differently: a special item
    /// explosion deals damage equal to the number of box cells it covers, while an adjacent blast
    /// deals damage equal to the number of adjacent blasted cubes.
    /// </summary>
    public sealed class ChaliceBox : Obstacle
    {
        /// <summary>Which of the two clear stages the box is currently in.</summary>
        public enum Phase
        {
            Doors,
            Chalices
        }

        [Header("Renderers")]
        [SerializeField] private SpriteRenderer _boxRenderer;

        [Tooltip("Hidden once the doors are destroyed.")]
        [SerializeField] private SpriteRenderer _doorsRenderer;

        [Header("Health")]
        [Tooltip("Damage sources needed to destroy the doors.")]
        [SerializeField] private int _doorHitPoints = 1;

        [Tooltip("Chalices held by the box once its doors are open.")]
        [SerializeField] private int _chaliceHitPoints = 10;

        /// <summary>Occupies a 2x2 block anchored at its bottom-left cell.</summary>
        public override Vector2Int Size => new(2, 2);

        /// <summary>The box is fixed in place and blocks falls, like stone.</summary>
        public override bool CanFall => false;

        public int DoorHitPoints => _doorHitPoints;

        public int ChaliceHitPoints => _chaliceHitPoints;

        public SpriteRenderer BoxRenderer => _boxRenderer;

        /// <summary>
        /// Not yet damageable. Its two-phase rule needs chalice counting wired into the level goals,
        /// so it is implemented together with goal tracking; until then the box behaves as an
        /// indestructible blocker rather than pretending to be clearable.
        /// </summary>
        public override bool TryTakeDamage(DamageInfo damage)
        {
            return false;
        }

        /// <summary>Shows or hides the doors to reflect the current phase.</summary>
        public void ShowPhase(Phase phase)
        {
            if (_doorsRenderer != null)
            {
                _doorsRenderer.enabled = phase == Phase.Doors;
            }
        }
    }
}

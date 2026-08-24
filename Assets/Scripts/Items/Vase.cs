using UnityEngine;

namespace Blast.Items
{
    /// <summary>
    /// Vase. Damaged by an adjacent cube blast or by a special item explosion, and cleared after
    /// two points of damage. A single blast can never deal more than one damage no matter how many
    /// of its cubes touch the vase.
    /// </summary>
    public sealed class Vase : Obstacle
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;

        [Tooltip("Total damage needed to clear the vase.")]
        [SerializeField] private int _maxHitPoints = 2;

        [Tooltip("Artwork per remaining hit point, most healthy first.")]
        [SerializeField] private Sprite[] _healthStateSprites;

        private int _remainingHitPoints = -1;

        /// <summary>Unlike the other obstacles, a vase obeys gravity.</summary>
        public override bool CanFall => true;

        public int MaxHitPoints => _maxHitPoints;

        /// <summary>
        /// Takes one point from any source and cracks on the way.
        ///
        /// The "at most one damage per blast" rule is enforced by the caller, which delivers a single
        /// hit per damage source rather than one per adjacent blasted cube — so the vase itself does
        /// not need to track which blast it came from.
        /// </summary>
        public override bool TryTakeDamage(DamageInfo damage)
        {
            if (_remainingHitPoints < 0)
            {
                _remainingHitPoints = _maxHitPoints;
            }

            _remainingHitPoints -= Mathf.Max(1, damage.Amount);

            if (_remainingHitPoints > 0)
            {
                ShowHealthState(_remainingHitPoints);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Shows the artwork matching the remaining hit points. Kept separate from the damage rule
        /// so the visual state can be driven from a single place once damage lands.
        /// </summary>
        public void ShowHealthState(int remainingHitPoints)
        {
            if (_healthStateSprites == null || _healthStateSprites.Length == 0)
            {
                return;
            }

            var index = Mathf.Clamp(_maxHitPoints - remainingHitPoints, 0, _healthStateSprites.Length - 1);
            _spriteRenderer.sprite = _healthStateSprites[index];
        }
    }
}

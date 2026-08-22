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

        /// <summary>Unlike the other obstacles, a vase obeys gravity.</summary>
        public override bool CanFall => true;

        public int MaxHitPoints => _maxHitPoints;

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

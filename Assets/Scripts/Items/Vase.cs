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

        /// <summary>
        /// "Not started yet", resolved to <see cref="_maxHitPoints"/> on first use.
        ///
        /// A field initializer cannot be used because it runs before Unity deserializes the value
        /// authored on the prefab, and <c>Awake</c> cannot be used because the editor verification
        /// tools damage items outside play mode, where it never runs. A sentinel resolved lazily is
        /// correct in both cases; the chalice box does the same for the same reason.
        /// </summary>
        private const int Uninitialised = -1;

        private int _remainingHitPoints = Uninitialised;

        /// <summary>Unlike the other obstacles, a vase obeys gravity.</summary>
        public override bool CanFall => true;

        public int MaxHitPoints => _maxHitPoints;

        /// <summary>Hit points left, reported as full before the vase has taken anything.</summary>
        public int RemainingHitPoints =>
            _remainingHitPoints == Uninitialised ? _maxHitPoints : _remainingHitPoints;

        /// <summary>
        /// Takes exactly one point per hit and cracks on the way.
        ///
        /// Deliberately ignores <see cref="DamageInfo.Amount"/>: a blast reports how many of its cubes
        /// touched the item, but the vase "takes no more than one damage from a single blast". Owning
        /// that rule here rather than having the caller pre-clamp keeps it next to the rest of the
        /// vase's behaviour, and lets the chalice box use the same figure for its own purposes.
        /// </summary>
        public override DamageResult ApplyDamage(DamageInfo damage)
        {
            if (_remainingHitPoints == Uninitialised)
            {
                _remainingHitPoints = _maxHitPoints;
            }

            _remainingHitPoints--;

            if (_remainingHitPoints <= 0)
            {
                return DamageResult.Destroyed;
            }

            ShowHealthState(_remainingHitPoints);
            return DamageResult.Damaged;
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

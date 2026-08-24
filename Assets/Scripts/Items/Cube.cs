using UnityEngine;

namespace Blast.Items
{
    /// <summary>
    /// A coloured cube. Blasting a group of at least two same-coloured, orthogonally adjacent cubes
    /// is the core move.
    /// </summary>
    public sealed class Cube : GridItem
    {
        /// <summary>
        /// Which special item the cube's current group would create. Shown directly on the cube
        /// artwork, which ships with a dedicated sprite per state.
        /// </summary>
        public enum Hint
        {
            None,
            Rocket,
            Tnt
        }

        [SerializeField] private CubeColor _color;
        [SerializeField] private SpriteRenderer _spriteRenderer;

        [Header("Hint states")]
        [SerializeField] private Sprite _defaultSprite;
        [SerializeField] private Sprite _rocketHintSprite;
        [SerializeField] private Sprite _tntHintSprite;

        private Hint _currentHint = Hint.None;

        public CubeColor Color => _color;

        public override bool CanFall => true;

        /// <summary>Cubes have no health: anything that reaches them clears them.</summary>
        public override bool TryTakeDamage(DamageInfo damage)
        {
            return true;
        }

        /// <summary>
        /// Swaps the cube artwork to advertise the special item its group is eligible to create.
        /// No-ops when the hint has not changed so recomputing hints every settle is cheap.
        /// </summary>
        public void ShowHint(Hint hint)
        {
            if (_currentHint == hint)
            {
                return;
            }

            _currentHint = hint;
            _spriteRenderer.sprite = hint switch
            {
                Hint.Rocket => _rocketHintSprite,
                Hint.Tnt => _tntHintSprite,
                _ => _defaultSprite
            };
        }
    }
}

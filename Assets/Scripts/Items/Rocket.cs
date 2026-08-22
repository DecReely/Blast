using UnityEngine;

namespace Blast.Items
{
    /// <summary>
    /// A rocket. On exploding it splits into two halves that travel in opposite directions,
    /// damaging cells one at a time as they pass over them.
    /// </summary>
    public sealed class Rocket : SpecialItem
    {
        /// <summary>Axis the rocket's two halves travel along.</summary>
        public enum Axis
        {
            Horizontal,
            Vertical
        }

        [SerializeField] private Axis _axis;

        [Header("Split halves")]
        [Tooltip("Half that travels left (horizontal) or down (vertical).")]
        [SerializeField] private Sprite _negativeHalfSprite;

        [Tooltip("Half that travels right (horizontal) or up (vertical).")]
        [SerializeField] private Sprite _positiveHalfSprite;

        public Axis Orientation => _axis;

        public Sprite NegativeHalfSprite => _negativeHalfSprite;

        public Sprite PositiveHalfSprite => _positiveHalfSprite;

        /// <summary>Unit step the two halves travel along, negated for the opposite half.</summary>
        public Vector2Int TravelStep => _axis == Axis.Horizontal ? Vector2Int.right : Vector2Int.up;
    }
}

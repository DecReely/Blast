using Blast.Gameplay;
using UnityEngine;

namespace Blast.Items
{
    /// <summary>
    /// A rocket. On exploding it splits into two halves that travel in opposite directions along its
    /// axis, damaging cells one at a time as they pass.
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

        public Axis Orientation => _axis;

        /// <summary>
        /// The half sprites are owned by the visuals spawner rather than by this component, because a
        /// combo fires rockets that never existed as items and still needs the same artwork.
        /// </summary>
        public override IExplosionPattern CreateExplosionPattern()
        {
            return new RocketPattern(_axis);
        }
    }
}

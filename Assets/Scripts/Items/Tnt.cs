using Blast.Gameplay;
using UnityEngine;

namespace Blast.Items
{
    /// <summary>
    /// A TNT. On exploding it damages a square area centred on its own cell.
    /// </summary>
    public sealed class Tnt : SpecialItem
    {
        /// <summary>
        /// Cells from the centre to the edge of the blast. The case study specifies a 5x5 area,
        /// which is a radius of two. Serialized so the TNT-TNT combo's larger 7x7 blast is expressed
        /// as data rather than a second hard-coded number.
        /// </summary>
        [SerializeField] private int _blastRadius = 2;

        public int BlastRadius => _blastRadius;

        public override IExplosionPattern CreateExplosionPattern()
        {
            return new AreaPattern(_blastRadius);
        }
    }
}

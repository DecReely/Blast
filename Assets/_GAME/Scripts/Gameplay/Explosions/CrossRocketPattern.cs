using Blast.Items;
using UnityEngine;

namespace Blast.Gameplay
{
    /// <summary>
    /// Rockets fired outwards along rows and columns through the centre, forming a plus.
    ///
    /// One class covers both rocket-firing combos, which differ only in how thick the plus is:
    /// the Rocket-Rocket combo is "2 perpendicular rockets in a + shape" (thickness one), and the
    /// TNT-Rocket combo is a "3x3 array of exploding rockets in a + shape" (thickness three, so three
    /// rows and three columns).
    /// </summary>
    public sealed class CrossRocketPattern : IExplosionPattern
    {
        private readonly int _thickness;

        /// <param name="thickness">How many rows and columns the plus spans. Expected to be odd.</param>
        public CrossRocketPattern(int thickness)
        {
            _thickness = Mathf.Max(1, thickness);
        }

        public void Detonate(Vector2Int center, ExplosionSystem explosions)
        {
            var reach = (_thickness - 1) / 2;

            for (var offset = -reach; offset <= reach; offset++)
            {
                explosions.LaunchRocketSweeps(
                    new Vector2Int(center.x, center.y + offset), Rocket.Axis.Horizontal);

                explosions.LaunchRocketSweeps(
                    new Vector2Int(center.x + offset, center.y), Rocket.Axis.Vertical);
            }
        }
    }
}

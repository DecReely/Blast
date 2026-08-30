using UnityEngine;

namespace Blast.Gameplay
{
    /// <summary>
    /// A square blast centred on the cell.
    ///
    /// Radius two is a lone TNT's 5x5; radius three is the TNT-TNT combo's 7x7. The two differ only
    /// by this number, so they need no separate class.
    /// </summary>
    public sealed class AreaPattern : IExplosionPattern
    {
        private readonly int _radius;

        public AreaPattern(int radius)
        {
            _radius = radius;
        }

        public void Detonate(Vector2Int center, ExplosionSystem explosions)
        {
            explosions.DamageArea(center, _radius);
        }
    }
}

using Blast.Items;
using UnityEngine;

namespace Blast.Gameplay
{
    /// <summary>
    /// A lone rocket: two halves travelling out along one axis.
    /// </summary>
    public sealed class RocketPattern : IExplosionPattern
    {
        private readonly Rocket.Axis _axis;

        public RocketPattern(Rocket.Axis axis)
        {
            _axis = axis;
        }

        public void Detonate(Vector2Int center, ExplosionSystem explosions)
        {
            explosions.LaunchRocketSweeps(center, _axis);
        }
    }
}

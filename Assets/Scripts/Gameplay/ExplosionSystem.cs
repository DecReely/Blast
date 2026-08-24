using Blast.Effects;
using Blast.Items;
using UnityEngine;

namespace Blast.Gameplay
{
    /// <summary>
    /// Detonates special items and delivers the resulting damage to the board.
    ///
    /// Chain reactions are made safe structurally rather than with flags: a special is taken off the
    /// board <em>before</em> its explosion is created, so nothing that the explosion touches
    /// afterwards can find it and trigger it a second time. That single rule is what keeps a
    /// rocket-into-TNT-into-rocket cascade from looping forever.
    /// </summary>
    public sealed class ExplosionSystem
    {
        private readonly Board _board;
        private readonly BoardActionRunner _actionRunner;
        private readonly ParticleEffectPool _effectPool;
        private readonly SpecialItemVisuals _visuals;

        public ExplosionSystem(
            Board board,
            BoardActionRunner actionRunner,
            ParticleEffectPool effectPool,
            SpecialItemVisuals visuals)
        {
            _board = board;
            _actionRunner = actionRunner;
            _effectPool = effectPool;
            _visuals = visuals;
        }

        /// <summary>
        /// Sets off a special item, whether the player tapped it or another explosion reached it.
        /// Safe to call twice: the second call sees the item is no longer on the board and returns.
        /// </summary>
        public void Detonate(SpecialItem special)
        {
            if (special == null)
            {
                return;
            }

            var origin = special.Origin;
            
            if (_board.GetItem(origin) != special)
            {
                return;
            }
            
            _board.Remove(special);

            _effectPool.Play(special.ClearEffectPrefab, special.transform.position);

            switch (special)
            {
                case Rocket rocket:
                    LaunchRocketHalves(rocket, origin);
                    break;

                case Tnt tnt:
                    DamageArea(origin, tnt.BlastRadius);
                    break;
            }

            ObjectLifetime.Destroy(special.gameObject);
        }

        /// <summary>
        /// Damages every cell in a square centred on <paramref name="center"/>. A radius of two is
        /// the 5x5 area a TNT clears.
        /// </summary>
        public void DamageArea(Vector2Int center, int radius)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    DamageCell(center + new Vector2Int(dx, dy), DamageInfo.FromExplosion());
                }
            }
        }

        /// <summary>
        /// Applies one hit to whatever occupies a cell. A special item found there is detonated
        /// instead of damaged, which is how the chain spreads.
        /// </summary>
        public void DamageCell(Vector2Int cell, DamageInfo damage)
        {
            var item = _board.GetItem(cell);
            if (item == null)
            {
                return;
            }

            if (item is SpecialItem special)
            {
                Detonate(special);
                return;
            }

            if (!item.TryTakeDamage(damage))
            {
                return;
            }

            _board.Remove(item);
            _effectPool.Play(item.ClearEffectPrefab, item.transform.position);
            ObjectLifetime.Destroy(item.gameObject);
        }

        /// <summary>
        /// Sends the two halves off in opposite directions. Each is its own action, so they travel
        /// and damage independently and the turn waits for both.
        /// </summary>
        private void LaunchRocketHalves(Rocket rocket, Vector2Int origin)
        {
            var step = rocket.TravelStep;

            _actionRunner.Add(new RocketSweepAction(
                _board, this, _visuals, origin, step, rocket.PositiveHalfSprite));

            _actionRunner.Add(new RocketSweepAction(
                _board, this, _visuals, origin, -step, rocket.NegativeHalfSprite));
        }
    }
}

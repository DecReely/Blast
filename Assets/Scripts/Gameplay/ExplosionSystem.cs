using Blast.Effects;
using Blast.Items;
using UnityEngine;

namespace Blast.Gameplay
{
    /// <summary>
    /// Runs explosions and delivers the resulting damage to the board.
    ///
    /// Chain reactions are made safe structurally rather than with flags: a special is taken off the
    /// board <em>before</em> its explosion is created, so nothing that the explosion touches
    /// afterwards can find it and trigger it a second time. That single rule is what keeps a
    /// rocket-into-TNT-into-rocket cascade from looping forever, and it is the same rule that lets a
    /// combo discard its members' individual explosions.
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
        /// Sets off a single special item using its own pattern, whether the player tapped it or
        /// another explosion reached it. Safe to call twice: the second call sees the item is no
        /// longer on the board and returns.
        /// </summary>
        public void Detonate(SpecialItem special)
        {
            if (special == null || _board.GetItem(special.Origin) != special)
            {
                return;
            }

            var origin = special.Origin;

            // Captured before the item is destroyed, since the pattern comes from the instance.
            var pattern = special.CreateExplosionPattern();

            Consume(special);
            pattern.Detonate(origin, this);
        }

        /// <summary>
        /// Removes a special from the board and plays its effect without exploding it. Used by combos,
        /// which replace their members' individual explosions with one shared pattern.
        /// </summary>
        public void Consume(SpecialItem special)
        {
            if (special == null)
            {
                return;
            }

            _board.Remove(special);
            _effectPool.Play(special.ClearEffectPrefab, special.transform.position);
            ObjectLifetime.Destroy(special.gameObject);
        }

        /// <summary>
        /// Damages every cell in a square centred on <paramref name="center"/>. A radius of two is
        /// the 5x5 a TNT clears; three is the TNT-TNT combo's 7x7.
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
        /// Fires a rocket outwards from a cell in both directions along an axis.
        ///
        /// The origin cell is damaged directly, because the halves only start damaging from the next
        /// cell on. For a lone rocket that cell is already empty, but for a combo's plus shape the
        /// origins form the centre of the cross and must not be left untouched.
        /// </summary>
        public void LaunchRocketSweeps(Vector2Int origin, Rocket.Axis axis)
        {
            DamageCell(origin, DamageInfo.FromExplosion());

            _actionRunner.Add(new RocketSweepAction(_board, this, _visuals, origin, axis, positive: true));
            _actionRunner.Add(new RocketSweepAction(_board, this, _visuals, origin, axis, positive: false));
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
    }
}

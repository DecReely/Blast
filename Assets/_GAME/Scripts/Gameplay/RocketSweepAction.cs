using Blast.Items;
using UnityEngine;

namespace Blast.Gameplay
{
    /// <summary>
    /// One half of a split rocket travelling across the board, damaging each cell as it reaches it.
    ///
    /// The case study asks for cells to be damaged "one by one as they pass over them", so damage is
    /// applied per cell boundary crossed rather than all at once when the rocket fires. The visual
    /// moves smoothly between cells while the damage stays quantised to the grid.
    /// </summary>
    public sealed class RocketSweepAction : IBoardAction
    {
        private readonly Board _board;
        private readonly ExplosionSystem _explosions;
        private readonly RocketHalfView _half;
        private readonly Vector2Int _step;
        private readonly float _cellsPerSecond;
        private readonly DamageInfo _damage;

        private Vector2Int _cell;
        private float _progressIntoNextCell;

        /// <param name="positive">True for the half heading right or up.</param>
        public RocketSweepAction(
            Board board,
            ExplosionSystem explosions,
            SpecialItemVisuals visuals,
            Vector2Int origin,
            Rocket.Axis axis,
            bool positive,
            DamageInfo damage,
            float cellsPerSecond = 16f)
        {
            _board = board;
            _explosions = explosions;
            _cellsPerSecond = cellsPerSecond;
            _cell = origin;

            // Shared with the rocket's other half, so the pair reads as one damage source.
            _damage = damage;

            var forward = axis == Rocket.Axis.Horizontal ? Vector2Int.right : Vector2Int.up;
            _step = positive ? forward : -forward;

            _half = visuals != null
                ? visuals.SpawnRocketHalf(axis, positive, board.CellToWorld(origin))
                : null;
        }

        public bool Tick(float deltaTime)
        {
            _progressIntoNextCell += _cellsPerSecond * deltaTime;

            // A large delta could span more than one cell, so damage every boundary crossed rather
            // than skipping over cells.
            while (_progressIntoNextCell >= 1f)
            {
                _progressIntoNextCell -= 1f;
                _cell += _step;

                if (!_board.InBounds(_cell))
                {
                    Finish();
                    return false;
                }

                _explosions.DamageCell(_cell, _damage);
            }

            if (_half != null)
            {
                var offset = (Vector3)(Vector2)_step * _progressIntoNextCell;
                _half.transform.position = _board.CellToWorld(_cell) + offset;
            }

            return true;
        }

        private void Finish()
        {
            if (_half == null)
            {
                return;
            }

            // The trail is left behind to burn out; only the rocket itself goes now.
            _half.Release();
            ObjectLifetime.Destroy(_half.gameObject);
        }
    }
}

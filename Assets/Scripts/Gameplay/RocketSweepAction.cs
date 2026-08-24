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
        private readonly Transform _visual;
        private readonly Vector2Int _step;
        private readonly float _cellsPerSecond;

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
            float cellsPerSecond = 16f)
        {
            _board = board;
            _explosions = explosions;
            _cellsPerSecond = cellsPerSecond;
            _cell = origin;

            var forward = axis == Rocket.Axis.Horizontal ? Vector2Int.right : Vector2Int.up;
            _step = positive ? forward : -forward;

            _visual = visuals != null
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

                _explosions.DamageCell(_cell, DamageInfo.FromExplosion());
            }

            if (_visual != null)
            {
                var offset = (Vector3)(Vector2)_step * _progressIntoNextCell;
                _visual.position = _board.CellToWorld(_cell) + offset;
            }

            return true;
        }

        private void Finish()
        {
            if (_visual != null)
            {
                ObjectLifetime.Destroy(_visual.gameObject);
            }
        }
    }
}

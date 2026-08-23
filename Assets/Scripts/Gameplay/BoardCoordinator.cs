using System;
using System.Collections.Generic;
using Blast.Effects;
using UnityEngine;

namespace Blast.Gameplay
{
    /// <summary>
    /// Resolves a player tap into a move.
    ///
    /// This is the only place that decides what a tap means, which keeps the rules out of the input
    /// layer and out of the items. It also owns the "board is busy" gate: while a move is resolving
    /// no further tap is accepted, so two blasts can never interleave.
    /// </summary>
    public sealed class BoardCoordinator
    {
        private readonly Board _board;
        private readonly GroupFinder _groupFinder;
        private readonly HintController _hintController;
        private readonly MoveCounter _moveCounter;
        private readonly ParticleEffectPool _effectPool;
        private readonly List<Vector2Int> _group = new();

        public BoardCoordinator(
            Board board,
            GroupFinder groupFinder,
            HintController hintController,
            MoveCounter moveCounter,
            ParticleEffectPool effectPool)
        {
            _board = board;
            _groupFinder = groupFinder;
            _hintController = hintController;
            _moveCounter = moveCounter;
            _effectPool = effectPool;
        }

        /// <summary>
        /// True while a move is being resolved. Currently a blast completes within the call, but the
        /// gate exists because falling, explosions and combos will each extend a move over several
        /// frames.
        /// </summary>
        public bool IsResolving { get; private set; }

        /// <summary>Raised after a successful blast, with the number of cubes cleared.</summary>
        public event Action<int> BlastResolved;

        /// <summary>Brings hints up to date, e.g. right after a level is built.</summary>
        public void RefreshHints()
        {
            _hintController.Refresh(_board);
        }

        /// <summary>Entry point for a tap, given in world space.</summary>
        public void HandleWorldTap(Vector3 worldPosition)
        {
            if (IsResolving || !_moveCounter.HasMovesLeft)
            {
                return;
            }

            var cell = _board.WorldToCell(worldPosition);
            if (!_board.InBounds(cell))
            {
                return;
            }

            TryBlastAt(cell);
        }

        private void TryBlastAt(Vector2Int cell)
        {
            var groupSize = _groupFinder.FindGroupOfCubes(_board, cell, _group);

            // Tapping a lone cube, an obstacle or an empty cell is simply not a move, so it must not
            // cost the player anything.
            if (!SpecialItemRules.IsBlastable(groupSize))
            {
                return;
            }

            IsResolving = true;

            _moveCounter.Spend();
            ClearGroup();
            _hintController.Refresh(_board);

            IsResolving = false;
            BlastResolved?.Invoke(groupSize);
        }

        private void ClearGroup()
        {
            foreach (var cell in _group)
            {
                var item = _board.GetItem(cell);
                if (item == null)
                {
                    continue;
                }

                // Detach from the model before destroying the view, so the board is never holding a
                // reference to something that is on its way out.
                _board.Remove(item);

                if (_effectPool != null)
                {
                    _effectPool.Play(item.ClearEffectPrefab, item.transform.position);
                }

                ObjectLifetime.Destroy(item.gameObject);
            }
        }
    }
}

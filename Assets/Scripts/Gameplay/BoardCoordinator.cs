using System;
using System.Collections.Generic;
using Blast.Effects;
using Blast.Motion;
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
        private readonly GravitySystem _gravitySystem;
        private readonly FallAnimator _fallAnimator;
        private readonly List<Vector2Int> _group = new();
        private readonly List<FallRequest> _fallRequests = new();

        private int _resolvingGroupSize;

        public BoardCoordinator(
            Board board,
            GroupFinder groupFinder,
            HintController hintController,
            MoveCounter moveCounter,
            ParticleEffectPool effectPool,
            GravitySystem gravitySystem,
            FallAnimator fallAnimator)
        {
            _board = board;
            _groupFinder = groupFinder;
            _hintController = hintController;
            _moveCounter = moveCounter;
            _effectPool = effectPool;
            _gravitySystem = gravitySystem;
            _fallAnimator = fallAnimator;
        }

        /// <summary>
        /// True from the moment a tap is accepted until the board has finished falling and refilling.
        /// Input is gated on this so a second tap can never interleave with a settle in progress.
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
            _resolvingGroupSize = groupSize;

            _moveCounter.Spend();
            ClearGroup();
            Settle();
        }

        /// <summary>
        /// Collapses the columns, tops them up, and hands the resulting movement to the animator. The
        /// turn only ends once everything has landed.
        /// </summary>
        private void Settle()
        {
            _fallRequests.Clear();
            _gravitySystem.Resolve(_board, _fallRequests);

            if (_fallAnimator == null)
            {
                // No animator available (unit tests, tooling): apply the result instantly.
                foreach (var request in _fallRequests)
                {
                    _board.SnapToCell(request.Item);
                }

                OnSettled();
                return;
            }

            _fallAnimator.Play(_fallRequests, OnSettled);
        }

        private void OnSettled()
        {
            // Hints depend on the settled layout, so they can only be correct once falling is done.
            _hintController.Refresh(_board);

            IsResolving = false;
            BlastResolved?.Invoke(_resolvingGroupSize);
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

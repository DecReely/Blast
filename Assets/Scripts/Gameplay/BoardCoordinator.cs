using System;
using System.Collections.Generic;
using Blast.Effects;
using Blast.Items;
using Blast.Motion;
using UnityEngine;

namespace Blast.Gameplay
{
    /// <summary>
    /// Resolves a player tap into a move.
    ///
    /// The only place that decides what a tap means, which keeps the rules out of the input layer
    /// and out of the items themselves. A turn runs as a small sequence — resolve the tap, let any
    /// explosions play out, settle the board, refresh hints — and the "busy" gate is held for the
    /// whole of it so taps cannot interleave.
    ///
    /// There are three kinds of tap:
    /// a special item detonates; a group of four or more collapses into a new special; a smaller
    /// group simply blasts. Anything else is not a move and costs nothing.
    /// </summary>
    public sealed class BoardCoordinator
    {
        private readonly Board _board;
        private readonly GroupFinder _groupFinder;
        private readonly HintController _hintController;
        private readonly MoveCounter _moveCounter;
        private readonly ParticleEffectPool _effectPool;
        private readonly GravitySystem _gravitySystem;
        private readonly ExplosionSystem _explosionSystem;
        private readonly BoardActionRunner _actionRunner;
        private readonly SpecialItemFactory _specialFactory;
        private readonly FallAnimator _fallAnimator;
        private readonly MergeAnimator _mergeAnimator;

        private readonly List<Vector2Int> _group = new();
        private readonly List<FallRequest> _fallRequests = new();
        private readonly List<Transform> _mergingVisuals = new();
        private readonly HashSet<GridItem> _blastNeighbours = new();

        private int _resolvingGroupSize;

        public BoardCoordinator(
            Board board,
            GroupFinder groupFinder,
            HintController hintController,
            MoveCounter moveCounter,
            ParticleEffectPool effectPool,
            GravitySystem gravitySystem,
            ExplosionSystem explosionSystem,
            BoardActionRunner actionRunner,
            SpecialItemFactory specialFactory,
            FallAnimator fallAnimator,
            MergeAnimator mergeAnimator)
        {
            _board = board;
            _groupFinder = groupFinder;
            _hintController = hintController;
            _moveCounter = moveCounter;
            _effectPool = effectPool;
            _gravitySystem = gravitySystem;
            _explosionSystem = explosionSystem;
            _actionRunner = actionRunner;
            _specialFactory = specialFactory;
            _fallAnimator = fallAnimator;
            _mergeAnimator = mergeAnimator;
        }

        /// <summary>
        /// True from the moment a tap is accepted until the board has finished exploding, falling and
        /// refilling. Input is gated on this.
        /// </summary>
        public bool IsResolving { get; private set; }

        /// <summary>Raised when a turn completes, with the number of cubes the tap cleared.</summary>
        public event Action<int> TurnResolved;

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

            if (_board.GetItem(cell) is SpecialItem special)
            {
                BeginTurn(0);
                DetonateTapped(special);
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

            BeginTurn(groupSize);

            // The blast damages whatever it is touching either way; only the fate of the cubes
            // themselves differs.
            DamageBlastNeighbours();

            if (SpecialItemRules.CreatesSpecial(groupSize))
            {
                BeginSpecialCreation(cell, groupSize);
                return;
            }

            ClearGroup();
            Settle();
        }

        /// <summary>
        /// Collapses a large group into a special item: the cubes leave the board immediately, their
        /// visuals fly to the tapped cell, and the special is created there once they arrive.
        /// </summary>
        private void BeginSpecialCreation(Vector2Int tappedCell, int groupSize)
        {
            DetachGroupVisuals();

            _mergeAnimator.Play(_mergingVisuals, _board.CellToWorld(tappedCell), () =>
            {
                foreach (var visual in _mergingVisuals)
                {
                    if (visual != null)
                    {
                        ObjectLifetime.Destroy(visual.gameObject);
                    }
                }

                _mergingVisuals.Clear();
                _specialFactory.Create(groupSize, _board, tappedCell);

                Settle();
            });
        }

        private void DetonateTapped(SpecialItem special)
        {
            _explosionSystem.Detonate(special);

            // Rocket sweeps and any chain they set off run over several frames.
            _actionRunner.NotifyWhenIdle(Settle);
        }

        private void BeginTurn(int groupSize)
        {
            IsResolving = true;
            _resolvingGroupSize = groupSize;
            _moveCounter.Spend();
        }

        /// <summary>
        /// Applies blast damage to the obstacles touching the group.
        ///
        /// Collected into a set first so each obstacle is hit exactly once however many of its
        /// neighbours went off — that is precisely the vase's "no more than one damage from a single
        /// blast" rule, enforced here rather than inside every obstacle.
        ///
        /// Special items are skipped on purpose: they detonate when tapped or when caught in another
        /// explosion, not merely because cubes blasted beside them.
        /// </summary>
        private void DamageBlastNeighbours()
        {
            _blastNeighbours.Clear();

            foreach (var cell in _group)
            {
                foreach (var offset in Board.Neighbours)
                {
                    var neighbour = _board.GetItem(cell + offset);

                    if (neighbour == null || neighbour is Cube || neighbour is SpecialItem)
                    {
                        continue;
                    }

                    _blastNeighbours.Add(neighbour);
                }
            }

            foreach (var item in _blastNeighbours)
            {
                if (item.TryTakeDamage(DamageInfo.FromBlast()))
                {
                    RemoveAndDestroy(item);
                }
            }
        }

        private void ClearGroup()
        {
            foreach (var cell in _group)
            {
                var item = _board.GetItem(cell);
                if (item != null)
                {
                    RemoveAndDestroy(item);
                }
            }
        }

        /// <summary>
        /// Takes the group off the board but keeps the visuals alive so they can be animated. Once
        /// detached they are inert: nothing can tap them and gravity cannot see them.
        /// </summary>
        private void DetachGroupVisuals()
        {
            _mergingVisuals.Clear();

            foreach (var cell in _group)
            {
                var item = _board.GetItem(cell);
                if (item == null)
                {
                    continue;
                }

                _board.Remove(item);
                _mergingVisuals.Add(item.transform);
            }
        }

        private void RemoveAndDestroy(GridItem item)
        {
            // Detach from the model before destroying the view, so the board never holds a reference
            // to something on its way out.
            _board.Remove(item);
            _effectPool.Play(item.ClearEffectPrefab, item.transform.position);
            ObjectLifetime.Destroy(item.gameObject);
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
            TurnResolved?.Invoke(_resolvingGroupSize);
        }
    }
}

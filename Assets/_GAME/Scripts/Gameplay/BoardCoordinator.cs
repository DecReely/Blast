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
    /// There are four kinds of tap: a run of adjacent special items merges into one combo; a lone
    /// special detonates; a group of four or more cubes collapses into a new special; a smaller group
    /// simply blasts. Anything else is not a move and costs nothing.
    /// </summary>
    public sealed class BoardCoordinator
    {
        private readonly Board _board;
        private readonly GroupFinder _groupFinder;
        private readonly ComboDetector _comboDetector;
        private readonly HintController _hintController;
        private readonly MoveCounter _moveCounter;
        private readonly ParticleEffectPool _effectPool;
        private readonly GravitySystem _gravitySystem;
        private readonly ExplosionSystem _explosionSystem;
        private readonly BoardActionRunner _actionRunner;
        private readonly SpecialItemFactory _specialFactory;
        private readonly FallAnimator _fallAnimator;
        private readonly MergeAnimator _mergeAnimator;
        private readonly SpecialItemVisuals _visuals;

        private readonly List<Vector2Int> _group = new();
        private readonly List<SpecialItem> _specialGroup = new();
        private readonly List<FallRequest> _fallRequests = new();
        private readonly List<Transform> _mergingVisuals = new();
        private readonly Dictionary<GridItem, int> _blastNeighbours = new();

        private int _resolvingGroupSize;

        public BoardCoordinator(
            Board board,
            GroupFinder groupFinder,
            ComboDetector comboDetector,
            HintController hintController,
            MoveCounter moveCounter,
            ParticleEffectPool effectPool,
            GravitySystem gravitySystem,
            ExplosionSystem explosionSystem,
            BoardActionRunner actionRunner,
            SpecialItemFactory specialFactory,
            FallAnimator fallAnimator,
            MergeAnimator mergeAnimator,
            SpecialItemVisuals visuals)
        {
            _board = board;
            _groupFinder = groupFinder;
            _comboDetector = comboDetector;
            _hintController = hintController;
            _moveCounter = moveCounter;
            _effectPool = effectPool;
            _gravitySystem = gravitySystem;
            _explosionSystem = explosionSystem;
            _actionRunner = actionRunner;
            _specialFactory = specialFactory;
            _fallAnimator = fallAnimator;
            _mergeAnimator = mergeAnimator;
            _visuals = visuals;
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
                TapSpecial(cell, special);
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

            var world = _board.CellToWorld(tappedCell);

            _mergeAnimator.Play(_mergingVisuals, world, () =>
            {
                DestroyMergingVisuals();
                _specialFactory.Create(groupSize, _board, tappedCell);

                // Punctuates the moment the arriving cubes turn into the special, which otherwise
                // simply appears.
                PlayEffect(_visuals != null ? _visuals.SpecialCreatedEffectPrefab : null, world);

                Settle();
            });
        }

        /// <summary>
        /// A tapped special either detonates alone or, if it touches other specials, merges them all
        /// into a single combo.
        /// </summary>
        private void TapSpecial(Vector2Int cell, SpecialItem special)
        {
            var groupSize = _comboDetector.FindGroup(_board, cell, _specialGroup);

            BeginTurn(0);

            if (ComboRules.IsCombo(groupSize))
            {
                BeginCombo(cell);
                return;
            }

            _explosionSystem.Detonate(special);

            // Rocket sweeps and any chain they set off run over several frames.
            _actionRunner.NotifyWhenIdle(Settle);
        }

        /// <summary>
        /// Merges a run of adjacent special items into one explosion at the tapped cell.
        ///
        /// Every member is taken off the board up front, before anything can explode. That is what
        /// makes "individual explosions of special items are ignored" true by construction: once
        /// detached they cannot be found, damaged or triggered, so no flag or suppression check is
        /// needed. Their visuals then fly to the tapped cell and one combo pattern fires there.
        /// </summary>
        private void BeginCombo(Vector2Int tappedCell)
        {
            // Resolved before the members are consumed, because it depends on their types.
            var pattern = ComboRules.SelectPattern(_specialGroup);

            _mergingVisuals.Clear();

            foreach (var member in _specialGroup)
            {
                _board.Remove(member);
                _mergingVisuals.Add(member.transform);
            }

            var world = _board.CellToWorld(tappedCell);

            _mergeAnimator.Play(_mergingVisuals, world, () =>
            {
                DestroyMergingVisuals();

                // The members were consumed silently so their individual explosions could be
                // discarded, which leaves the combo itself with nothing to show at its centre.
                PlayEffect(_visuals != null ? _visuals.ComboEffectPrefab : null, world);

                pattern.Detonate(tappedCell, _explosionSystem);
                _actionRunner.NotifyWhenIdle(Settle);
            });
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
        /// Each obstacle is hit exactly once, carrying a count of how many of the blasted cubes were
        /// adjacent to it. Obstacles then interpret that number for themselves: a vase ignores it and
        /// takes one, while a chalice box in its chalice phase collects one chalice per adjacent cube.
        /// Counting here and deciding there keeps each rule with the item it belongs to.
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

                    _blastNeighbours.TryGetValue(neighbour, out var adjacentCubes);
                    _blastNeighbours[neighbour] = adjacentCubes + 1;
                }
            }

            foreach (var (item, adjacentCubes) in _blastNeighbours)
            {
                switch (item.ApplyDamage(DamageInfo.FromBlast(adjacentCubes)))
                {
                    // Stone ignores blasts, so it must not react to one.
                    case DamageResult.Ignored:
                        break;

                    case DamageResult.Damaged:
                        PlayEffect(item.DamageEffectPrefab, item.transform.position);
                        break;

                    case DamageResult.Destroyed:
                        RemoveAndDestroy(item);
                        break;
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

        /// <summary>Disposes of the detached visuals once a merge animation has finished.</summary>
        private void DestroyMergingVisuals()
        {
            foreach (var visual in _mergingVisuals)
            {
                if (visual != null)
                {
                    ObjectLifetime.Destroy(visual.gameObject);
                }
            }

            _mergingVisuals.Clear();
        }

        private void RemoveAndDestroy(GridItem item)
        {
            // Detach from the model before destroying the view, so the board never holds a reference
            // to something on its way out.
            _board.Remove(item);
            PlayEffect(item.ClearEffectPrefab, item.transform.position);
            ObjectLifetime.Destroy(item.gameObject);
        }

        /// <summary>Pooled one-shot playback. A null prefab simply means "nothing to show".</summary>
        private void PlayEffect(ParticleSystem prefab, Vector3 position)
        {
            if (_effectPool != null)
            {
                _effectPool.Play(prefab, position);
            }
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

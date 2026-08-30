using System.Collections.Generic;
using Blast.Items;
using UnityEngine;

namespace Blast.Gameplay
{
    /// <summary>
    /// Keeps every cube's artwork showing which special item its group would create.
    ///
    /// Recomputed wholesale after the board settles rather than patched incrementally: a single
    /// blast can split or merge groups anywhere in a column, so working out exactly which cubes
    /// changed would be more code and easier to get wrong than simply rescanning. Each group is
    /// visited once, so a pass is linear in the number of cells.
    /// </summary>
    public sealed class HintController
    {
        private readonly GroupFinder _groupFinder;
        private readonly List<Vector2Int> _group = new();
        private readonly HashSet<Vector2Int> _alreadyHinted = new();

        public HintController(GroupFinder groupFinder)
        {
            _groupFinder = groupFinder;
        }

        public void Refresh(Board board)
        {
            _alreadyHinted.Clear();

            for (var y = 0; y < board.Height; y++)
            {
                for (var x = 0; x < board.Width; x++)
                {
                    var cell = new Vector2Int(x, y);

                    if (_alreadyHinted.Contains(cell) || board.GetItem(cell) is not Cube)
                    {
                        continue;
                    }

                    var size = _groupFinder.FindGroupOfCubes(board, cell, _group);
                    var hint = SpecialItemRules.HintFor(size);

                    foreach (var member in _group)
                    {
                        _alreadyHinted.Add(member);

                        if (board.GetItem(member) is Cube cube)
                        {
                            cube.ShowHint(hint);
                        }
                    }
                }
            }
        }
    }
}

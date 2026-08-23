using System.Collections.Generic;
using Blast.Items;
using UnityEngine;

namespace Blast.Gameplay
{
    /// <summary>
    /// Finds the connected group of same-coloured cubes containing a cell.
    ///
    /// Connectivity is orthogonal only, and only cubes participate: obstacles and special items
    /// break a group rather than joining it.
    ///
    /// The working buffers are instance fields that get reused, so finding a group allocates
    /// nothing. That matters because hints re-scan the whole board after every move.
    /// </summary>
    public sealed class GroupFinder
    {
        private readonly Queue<Vector2Int> _frontier = new();
        private readonly HashSet<Vector2Int> _visited = new();

        /// <summary>
        /// Fills <paramref name="result"/> with every cell in the group containing
        /// <paramref name="origin"/> and returns its size. Returns 0 when the cell does not hold a
        /// cube, which is also how "this tap cannot start a blast" is reported.
        /// </summary>
        public int FindGroupOfCubes(Board board, Vector2Int origin, List<Vector2Int> result)
        {
            result.Clear();

            if (board.GetItem(origin) is not Cube start)
            {
                return 0;
            }

            var color = start.Color;

            _frontier.Clear();
            _visited.Clear();

            _frontier.Enqueue(origin);
            _visited.Add(origin);

            while (_frontier.Count > 0)
            {
                var current = _frontier.Dequeue();
                result.Add(current);

                foreach (var offset in Board.Neighbours)
                {
                    var next = current + offset;

                    if (!_visited.Add(next))
                    {
                        continue;
                    }

                    if (board.GetItem(next) is Cube neighbour && neighbour.Color == color)
                    {
                        _frontier.Enqueue(next);
                    }
                }
            }

            return result.Count;
        }
    }
}

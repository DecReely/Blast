using System.Collections.Generic;
using Blast.Items;
using UnityEngine;

namespace Blast.Gameplay
{
    /// <summary>
    /// Finds the connected run of special items containing a cell.
    ///
    /// Mirrors <see cref="GroupFinder"/> but matches on "is a special item" rather than on colour, so
    /// a rocket touching a TNT touching another rocket is one group. Buffers are reused, so detection
    /// allocates nothing per tap.
    /// </summary>
    public sealed class ComboDetector
    {
        private readonly Queue<Vector2Int> _frontier = new();
        private readonly HashSet<Vector2Int> _visited = new();

        /// <summary>
        /// Fills <paramref name="result"/> with the special items connected to <paramref name="origin"/>
        /// and returns the count. Returns 0 when the cell holds no special item.
        /// </summary>
        public int FindGroup(Board board, Vector2Int origin, List<SpecialItem> result)
        {
            result.Clear();

            if (board.GetItem(origin) is not SpecialItem)
            {
                return 0;
            }

            _frontier.Clear();
            _visited.Clear();

            _frontier.Enqueue(origin);
            _visited.Add(origin);

            while (_frontier.Count > 0)
            {
                var current = _frontier.Dequeue();

                if (board.GetItem(current) is SpecialItem special)
                {
                    result.Add(special);
                }

                foreach (var offset in Board.Neighbours)
                {
                    var next = current + offset;

                    if (!_visited.Add(next))
                    {
                        continue;
                    }

                    if (board.GetItem(next) is SpecialItem)
                    {
                        _frontier.Enqueue(next);
                    }
                }
            }

            return result.Count;
        }
    }
}

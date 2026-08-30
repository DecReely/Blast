using Blast.Items;
using UnityEngine;

namespace Blast.Gameplay
{
    /// <summary>
    /// One square of the board.
    ///
    /// Owned exclusively by <see cref="Board"/>: the mutators are internal so that all writes go
    /// through the board's API and the model can never drift out of step with what is on screen.
    /// A multi-cell item such as the chalice box is referenced by every cell it covers, so a lookup
    /// at any of its four cells finds the same instance.
    /// </summary>
    public sealed class Cell
    {
        internal Cell(Vector2Int position)
        {
            Position = position;
        }

        public Vector2Int Position { get; }

        /// <summary>The item occupying this cell, or null when the cell is empty.</summary>
        public GridItem Item { get; private set; }

        public bool IsEmpty => Item == null;

        internal void SetItem(GridItem item)
        {
            Item = item;
        }

        internal void Clear()
        {
            Item = null;
        }
    }
}

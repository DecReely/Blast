using Blast.Items;
using UnityEngine;

namespace Blast.Gameplay
{
    /// <summary>
    /// One item that has already been moved in the model and whose transform now has to catch up.
    ///
    /// Gravity is resolved instantly on the grid and the movement is replayed afterwards as pure
    /// presentation, so this carries only what the animation needs.
    /// </summary>
    public readonly struct FallRequest
    {
        public FallRequest(GridItem item, Vector3 from, Vector3 to)
        {
            Item = item;
            From = from;
            To = to;
        }

        public readonly GridItem Item;

        /// <summary>Where the item is coming from. For a refilled cube this is above the board.</summary>
        public readonly Vector3 From;

        /// <summary>World centre of the cell the item now occupies in the model.</summary>
        public readonly Vector3 To;
    }
}

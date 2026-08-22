using UnityEngine;
using UnityEngine.Rendering;

namespace Blast.Items
{
    /// <summary>
    /// Base class for anything that occupies cells on the board: cubes, special items and obstacles.
    ///
    /// A grid item is a passive presentation object. It never inspects its surroundings, never asks
    /// what is next to it and never decides where it belongs — the board is the single source of
    /// truth and pushes state down into the item. That keeps every gameplay rule in one place and
    /// makes items trivial to reason about.
    /// </summary>
    [RequireComponent(typeof(SortingGroup))]
    public abstract class GridItem : MonoBehaviour
    {
        private SortingGroup _sortingGroup;

        /// <summary>
        /// Bottom-left cell this item occupies. Written by the board; mirrored here purely so the
        /// item can report where the board put it.
        /// </summary>
        public Vector2Int Origin { get; private set; }

        /// <summary>
        /// Footprint in cells. Everything is 1x1 except the chalice box, which is 2x2.
        /// </summary>
        public virtual Vector2Int Size => Vector2Int.one;

        /// <summary>
        /// Whether gravity applies. Cubes, special items and vases fall; stone and the chalice box
        /// are fixed and also block anything above them from falling past.
        /// </summary>
        public abstract bool CanFall { get; }

        protected virtual void Awake()
        {
            _sortingGroup = GetComponent<SortingGroup>();
        }

        /// <summary>
        /// Called by the board when the item is placed or moved. Not intended for use elsewhere:
        /// changing an item's origin without telling the board would desynchronise the two.
        /// </summary>
        internal void SetOrigin(Vector2Int origin)
        {
            Origin = origin;
        }

        /// <summary>
        /// Sprites are ordered so that higher rows draw in front of lower ones. The art has a 3D
        /// bevel that overhangs upwards into the cell above, and the row above must cover it —
        /// otherwise the bevel shows through as a dark sliver across the top of every item.
        /// A <see cref="SortingGroup"/> is used so multi-renderer items (the chalice box) sort as
        /// one unit while keeping their internal layering.
        /// </summary>
        internal void SetSortingOrder(int order)
        {
            if (_sortingGroup == null)
            {
                _sortingGroup = GetComponent<SortingGroup>();
            }

            _sortingGroup.sortingOrder = order;
        }
    }
}

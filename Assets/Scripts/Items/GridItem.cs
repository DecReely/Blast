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
        [Tooltip("Particle burst played where the item stood when it is cleared from the board.")]
        [SerializeField] private ParticleSystem _clearEffectPrefab;

        private SortingGroup _sortingGroup;

        /// <summary>
        /// The effect to play when this item is removed. Owned by the item rather than looked up by
        /// the board, so each type carries its own presentation and adding a new item type needs no
        /// change anywhere else.
        /// </summary>
        public ParticleSystem ClearEffectPrefab => _clearEffectPrefab;

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

        /// <summary>
        /// Applies one hit and reports whether the item is now finished and should be removed.
        ///
        /// Abstract rather than virtual on purpose: every item type has a genuinely different rule,
        /// and forcing each to state it prevents a new type from silently inheriting "immune".
        /// </summary>
        public abstract bool TryTakeDamage(DamageInfo damage);

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
        /// Sprites are ordered so that higher rows draw in front of lower ones.
        ///
        /// Board art is slightly taller than its cell (a cube is 140x160 px in a 150 px cell) and is
        /// centred, so vertically adjacent items overlap by a few pixels. Drawing the upper row in
        /// front puts its shadowed bottom edge over the lower item's highlight, which is what gives
        /// the rows their shadow separation; the reverse order lays a bright edge between rows instead.
        ///
        /// A <see cref="SortingGroup"/> is used so multi-renderer items (the chalice box) sort as one
        /// unit while keeping their internal layering.
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

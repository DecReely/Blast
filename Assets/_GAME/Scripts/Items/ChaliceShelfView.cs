using System.Collections.Generic;
using UnityEngine;

namespace Blast.Items
{
    /// <summary>
    /// Draws the chalices a <see cref="ChaliceBox"/> still holds, on the two shelves of its artwork.
    ///
    /// The box art is a cabinet with a middle shelf, so the count is split across two rows. Both the
    /// split and the spacing are computed rather than authored, because the box can be left holding
    /// any number from ten down to one and every one of those states has to look deliberate.
    ///
    /// Purely presentational: it is told how many to show and never asks the box anything.
    /// </summary>
    public sealed class ChaliceShelfView : MonoBehaviour
    {
        [Header("Artwork")]
        [SerializeField] private Sprite _chaliceSprite;

        [Tooltip("Most chalices the box can hold, and so the most renderers this will ever create.")]
        [SerializeField] private int _capacity = 10;

        [Header("Layout")]
        [Tooltip("Width the shelf spreads chalices across, in cells. The box itself is two wide.")]
        [SerializeField] private float _rowWidth = 1.45f;

        [Tooltip("Local Y the upper row stands on: the top face of the middle shelf.")]
        [SerializeField] private float _topRowY = 0.1f;

        [Tooltip("Local Y the lower row stands on: the cabinet floor.")]
        [SerializeField] private float _bottomRowY = -0.6f;

        [Header("Depth")]
        [Tooltip("Base size of a chalice. Below one so a full row of five overlaps a little.")]
        [SerializeField] private float _baseScale = 0.75f;

        [Tooltip("Extra size for the chalice in the middle of a row, which reads as nearest.")]
        [SerializeField] private float _centerScale = 1.12f;

        [Tooltip("Size at the ends of a row, which read as furthest back.")]
        [SerializeField] private float _edgeScale = 0.92f;

        [Tooltip("Sorting order of the outermost chalice, within the box's sorting group.")]
        [SerializeField] private int _sortingOrder = 10;

        [Tooltip("How many sorting steps the middle of a row is raised by, so it draws in front.")]
        [SerializeField] private int _depthSortingSteps = 4;

        private readonly List<SpriteRenderer> _renderers = new();

        /// <summary>
        /// How a total is shared between the two shelves.
        ///
        /// The rows are kept as even as possible and the remainder goes to the bottom, so nine reads
        /// as 4 above and 5 below rather than 3 and 6. Static and side-effect free so the rule can be
        /// checked on its own.
        /// </summary>
        public static void SplitRows(int total, out int topRow, out int bottomRow)
        {
            total = Mathf.Max(0, total);

            topRow = total / 2;
            bottomRow = total - topRow;
        }

        /// <summary>Lays out <paramref name="remaining"/> chalices, hiding any left over.</summary>
        public void Show(int remaining)
        {
            if (_chaliceSprite == null)
            {
                return;
            }

            var total = Mathf.Clamp(remaining, 0, Mathf.Max(0, _capacity));
            SplitRows(total, out var topRow, out var bottomRow);

            var used = 0;
            used = LayOutRow(bottomRow, _bottomRowY, used);
            used = LayOutRow(topRow, _topRowY, used);

            // Anything the previous, larger count needed is kept but parked out of sight, so the
            // renderers are reused rather than churned as the box empties.
            for (var i = used; i < _renderers.Count; i++)
            {
                _renderers[i].enabled = false;
            }
        }

        /// <summary>
        /// Places one shelf's worth of chalices and returns how many renderers have been used in
        /// total, so the next row carries on from there.
        /// </summary>
        private int LayOutRow(int count, float shelfY, int firstIndex)
        {
            var spriteHeight = _chaliceSprite.bounds.size.y;

            for (var i = 0; i < count; i++)
            {
                var renderer = RendererAt(firstIndex + i);

                // 0 in the middle of the row, 1 at either end.
                var offsetFromCenter = count > 1
                    ? Mathf.Abs(i - ((count - 1) * 0.5f)) / ((count - 1) * 0.5f)
                    : 0f;

                var scale = _baseScale * Mathf.Lerp(_centerScale, _edgeScale, offsetFromCenter);

                // Evenly spaced across the shelf and centred on it, whatever the count: each chalice
                // takes the middle of its own equal share of the width.
                var x = (((i + 0.5f) / count) - 0.5f) * _rowWidth;

                // Positioned by its foot rather than its middle, so a chalice stands on the shelf at
                // any scale instead of hovering over it.
                var y = shelfY + (spriteHeight * scale * 0.5f);

                renderer.transform.localPosition = new Vector3(x, y, 0f);
                renderer.transform.localScale = Vector3.one * scale;

                // The middle of the row draws in front of its neighbours, which together with the
                // larger scale reads as the front of a shelf rather than a flat line.
                renderer.sortingOrder = _sortingOrder +
                                        Mathf.RoundToInt((1f - offsetFromCenter) * _depthSortingSteps);

                renderer.enabled = true;
            }

            return firstIndex + count;
        }

        private SpriteRenderer RendererAt(int index)
        {
            while (_renderers.Count <= index)
            {
                _renderers.Add(CreateRenderer(_renderers.Count));
            }

            return _renderers[index];
        }

        /// <summary>
        /// Built on demand rather than authored into the prefab: the positions depend on how many are
        /// showing, so pre-placed children would be wrong for every count but one.
        /// </summary>
        private SpriteRenderer CreateRenderer(int index)
        {
            var child = new GameObject($"Chalice_{index}");
            child.transform.SetParent(transform, false);

            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = _chaliceSprite;

            // Clipped to the board like every other board sprite.
            renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

            return renderer;
        }
    }
}

using Blast.Items;
using UnityEngine;

namespace Blast.Gameplay
{
    /// <summary>
    /// The board: the single source of truth for the level's contents.
    ///
    /// Everything else in the game asks the board rather than working things out for itself. It owns
    /// the cell grid, it owns item placement, and it is the only thing that converts between cell
    /// coordinates and world space — that conversion is delegated to the attached Unity
    /// <see cref="Grid"/>, so cell size and origin are defined in exactly one place and no system
    /// ever multiplies by a cell size of its own.
    /// </summary>
    [RequireComponent(typeof(Grid))]
    public sealed class Board : MonoBehaviour
    {
        /// <summary>The four orthogonal neighbours; diagonals are never adjacent in this game.</summary>
        public static readonly Vector2Int[] Neighbours =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        [Tooltip("Parent for spawned item instances, purely to keep the runtime hierarchy readable.")]
        [SerializeField] private Transform _itemsRoot;

        private Grid _grid;
        private Cell[,] _cells;

        public int Width { get; private set; }

        public int Height { get; private set; }

        public Transform ItemsRoot => _itemsRoot != null ? _itemsRoot : transform;

        /// <summary>World-space centre of the whole board, used to place the frame and camera.</summary>
        public Vector3 CenterWorld => transform.position + new Vector3(Width * 0.5f, Height * 0.5f, 0f);

        private void Awake()
        {
            CacheGrid();
        }

        /// <summary>
        /// Allocates an empty grid and moves the board so its centre sits on
        /// <paramref name="worldCenter"/>. Cell size stays fixed, so a smaller level simply occupies
        /// less of the screen instead of being zoomed to fit.
        /// </summary>
        public void Initialize(int width, int height, Vector2 worldCenter)
        {
            CacheGrid();

            Width = width;
            Height = height;
            _cells = new Cell[width, height];

            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    _cells[x, y] = new Cell(new Vector2Int(x, y));
                }
            }

            // Cell (0,0)'s centre is half a cell in from this origin, so offsetting by half the
            // board size lands the board's centre exactly on the requested anchor.
            transform.position = new Vector3(
                worldCenter.x - (width * 0.5f),
                worldCenter.y - (height * 0.5f),
                0f);
        }

        public bool InBounds(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;
        }

        /// <summary>Returns the cell, or null when out of bounds.</summary>
        public Cell GetCell(Vector2Int cell)
        {
            return InBounds(cell) ? _cells[cell.x, cell.y] : null;
        }

        /// <summary>Returns the item occupying a cell, or null when empty or out of bounds.</summary>
        public GridItem GetItem(Vector2Int cell)
        {
            return GetCell(cell)?.Item;
        }

        /// <summary>
        /// Puts an item on the board and snaps its transform to the cell. Used when building a level,
        /// where items simply appear in place.
        ///
        /// This is also where an item picks up the board's clipping: every path that spawns one —
        /// level building, refill and special item creation — ends here, so it is the one place that
        /// can say "you are on a board now" without the prefabs having to assume it.
        /// </summary>
        public void Place(GridItem item, Vector2Int origin)
        {
            Occupy(item, origin);
            SnapToCell(item);
            item.ClipToBoard();
        }

        /// <summary>
        /// Moves an item to a new origin in the model <em>without</em> touching its transform.
        ///
        /// Falling is resolved in the model up front and only then animated, so for the duration of
        /// the animation the item's cell is already its destination while its transform is still
        /// catching up. That is deliberate: the board is never in an intermediate state, so a second
        /// query mid-fall still sees a consistent grid.
        /// </summary>
        public void MoveTo(GridItem item, Vector2Int origin)
        {
            Remove(item);
            Occupy(item, origin);
        }

        /// <summary>Snaps an item's transform to the cell the model says it occupies.</summary>
        public void SnapToCell(GridItem item)
        {
            item.transform.position = FootprintCenterWorld(item.Origin, item.Size);
        }

        private void Occupy(GridItem item, Vector2Int origin)
        {
            var size = item.Size;

            for (var dx = 0; dx < size.x; dx++)
            {
                for (var dy = 0; dy < size.y; dy++)
                {
                    var cell = GetCell(origin + new Vector2Int(dx, dy));
                    if (cell == null)
                    {
                        Debug.LogError($"[Board] {item.name} at {origin} does not fit inside {Width}x{Height}.", item);
                        return;
                    }

                    cell.SetItem(item);
                }
            }

            item.SetOrigin(origin);

            // Higher rows draw in front; see GridItem.SetSortingOrder for why.
            item.SetSortingOrder(origin.y);
        }

        /// <summary>Clears every cell an item occupies. Does not destroy the instance.</summary>
        public void Remove(GridItem item)
        {
            var size = item.Size;

            for (var dx = 0; dx < size.x; dx++)
            {
                for (var dy = 0; dy < size.y; dy++)
                {
                    var cell = GetCell(item.Origin + new Vector2Int(dx, dy));

                    // Guard against clobbering a cell that something else has already claimed.
                    if (cell != null && cell.Item == item)
                    {
                        cell.Clear();
                    }
                }
            }
        }

        /// <summary>World position of a single cell's centre.</summary>
        public Vector3 CellToWorld(Vector2Int cell)
        {
            return _grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
        }

        /// <summary>
        /// Cell under a world position. Combined with <see cref="InBounds"/> this is all input
        /// picking needs — because the board is a regular grid there is no need for colliders or
        /// raycasts to work out what was tapped.
        /// </summary>
        public Vector2Int WorldToCell(Vector3 world)
        {
            var cell = _grid.WorldToCell(world);
            return new Vector2Int(cell.x, cell.y);
        }

        /// <summary>
        /// Centre of a footprint. For a 1x1 item this is just the cell centre; for the 2x2 chalice
        /// box it is the point shared by its four cells, which is where its pivot is authored.
        /// </summary>
        public Vector3 FootprintCenterWorld(Vector2Int origin, Vector2Int size)
        {
            var offset = new Vector3((size.x - 1) * 0.5f, (size.y - 1) * 0.5f, 0f);
            return CellToWorld(origin) + offset;
        }

        private void CacheGrid()
        {
            if (_grid == null)
            {
                _grid = GetComponent<Grid>();
            }
        }
    }
}

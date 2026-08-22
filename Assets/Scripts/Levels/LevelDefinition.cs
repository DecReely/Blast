using System;
using UnityEngine;

namespace Blast.Levels
{
    /// <summary>
    /// Deserialised contents of a single <c>level_XX.json</c> file.
    ///
    /// Field names intentionally match the JSON keys so <see cref="JsonUtility"/> can map them
    /// directly; they are private and exposed through properties so the rest of the codebase never
    /// sees the snake_case wire format.
    /// </summary>
    [Serializable]
    public sealed class LevelDefinition
    {
        /// <summary>Smallest grid dimension the case study allows.</summary>
        public const int MinDimension = 6;

        /// <summary>Largest grid dimension the case study allows.</summary>
        public const int MaxDimension = 10;

#pragma warning disable CS0649 // Assigned by JsonUtility via reflection.
        [SerializeField] private int level_number;
        [SerializeField] private int grid_width;
        [SerializeField] private int grid_height;
        [SerializeField] private int move_count;
        [SerializeField] private string[] grid;
#pragma warning restore CS0649

        public int LevelNumber => level_number;
        public int Width => grid_width;
        public int Height => grid_height;
        public int MoveCount => move_count;

        /// <summary>
        /// Item code at a cell, using the level file's convention that index 0 is the
        /// <em>bottom-left</em> cell and the list then runs horizontally, row by row, upwards.
        /// </summary>
        public string CodeAt(int x, int y)
        {
            return grid[(y * grid_width) + x];
        }

        public string CodeAt(Vector2Int cell)
        {
            return CodeAt(cell.x, cell.y);
        }

        /// <summary>
        /// Guards against malformed level data before anything tries to build a board from it,
        /// so a bad file produces one clear message instead of an index-out-of-range mid-build.
        /// </summary>
        public bool TryValidate(out string error)
        {
            if (grid_width is < MinDimension or > MaxDimension)
            {
                error = $"grid_width {grid_width} is outside the allowed {MinDimension}-{MaxDimension} range.";
                return false;
            }

            if (grid_height is < MinDimension or > MaxDimension)
            {
                error = $"grid_height {grid_height} is outside the allowed {MinDimension}-{MaxDimension} range.";
                return false;
            }

            if (move_count <= 0)
            {
                error = $"move_count must be positive but was {move_count}.";
                return false;
            }

            var expected = grid_width * grid_height;
            if (grid == null || grid.Length != expected)
            {
                error = $"grid has {grid?.Length ?? 0} entries but {grid_width}x{grid_height} needs {expected}.";
                return false;
            }

            error = null;
            return true;
        }
    }
}

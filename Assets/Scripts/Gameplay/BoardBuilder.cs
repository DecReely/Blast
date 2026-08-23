using Blast.Items;
using Blast.Levels;
using UnityEngine;

namespace Blast.Gameplay
{
    /// <summary>
    /// Turns a <see cref="LevelDefinition"/> into item instances on a <see cref="Board"/>.
    ///
    /// A plain class rather than a component: it holds no state beyond the catalog, which keeps the
    /// level-format knowledge in one small, easily testable place and out of both the board and the
    /// items.
    /// </summary>
    public sealed class BoardBuilder
    {
        private readonly ItemCatalog _catalog;

        public BoardBuilder(ItemCatalog catalog)
        {
            _catalog = catalog;
        }

        /// <summary>
        /// Spawns every item described by the level. The board must already be initialised to the
        /// level's dimensions.
        /// </summary>
        public void Populate(Board board, LevelDefinition level)
        {
            // Bottom-to-top so the chalice box is always reached at its bottom-left corner first.
            for (var y = 0; y < level.Height; y++)
            {
                for (var x = 0; x < level.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    var code = level.CodeAt(cell);

                    // The box's other three corners are absorbed by the instance spawned from its
                    // bottom-left corner, so they must not spawn anything of their own.
                    if (LevelCodes.IsAbsorbedChaliceBoxCorner(code))
                    {
                        continue;
                    }

                    if (code == LevelCodes.ChaliceBoxBottomLeft)
                    {
                        WarnIfChaliceBoxMalformed(level, cell);
                    }

                    var prefab = ResolvePrefab(code);
                    if (prefab == null)
                    {
                        Debug.LogError($"[BoardBuilder] Level {level.LevelNumber}: unknown item code '{code}' at {cell}.");
                        continue;
                    }

                    var instance = Object.Instantiate(prefab, board.ItemsRoot);
                    instance.name = $"{prefab.name}_{x}_{y}";
                    board.Place(instance, cell);
                }
            }
        }

        /// <summary>Destroys all spawned items so a level can be rebuilt or replayed.</summary>
        public static void ClearItems(Board board)
        {
            var root = board.ItemsRoot;
            for (var i = root.childCount - 1; i >= 0; i--)
            {
                ObjectLifetime.Destroy(root.GetChild(i).gameObject);
            }
        }

        private GridItem ResolvePrefab(string code)
        {
            return code == LevelCodes.RandomCube
                ? _catalog.GetRandomCubePrefab()
                : _catalog.GetPrefab(code);
        }

        /// <summary>
        /// The four corner codes must agree, otherwise the level file is describing a box shape that
        /// cannot exist. Reported rather than thrown so a bad level still loads and can be inspected.
        /// </summary>
        private static void WarnIfChaliceBoxMalformed(LevelDefinition level, Vector2Int bottomLeft)
        {
            var expected = new (Vector2Int offset, string code)[]
            {
                (new Vector2Int(1, 0), LevelCodes.ChaliceBoxBottomRight),
                (new Vector2Int(0, 1), LevelCodes.ChaliceBoxTopLeft),
                (new Vector2Int(1, 1), LevelCodes.ChaliceBoxTopRight)
            };

            foreach (var (offset, code) in expected)
            {
                var cell = bottomLeft + offset;

                if (cell.x >= level.Width || cell.y >= level.Height)
                {
                    Debug.LogError($"[BoardBuilder] Level {level.LevelNumber}: chalice box at {bottomLeft} runs off the grid.");
                    return;
                }

                if (level.CodeAt(cell) != code)
                {
                    Debug.LogError(
                        $"[BoardBuilder] Level {level.LevelNumber}: chalice box at {bottomLeft} expected " +
                        $"'{code}' at {cell} but found '{level.CodeAt(cell)}'.");
                }
            }
        }
    }
}

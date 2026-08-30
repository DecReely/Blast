using Blast.Gameplay;
using Blast.Items;
using UnityEditor;
using UnityEngine;

namespace Blast.EditorTools
{
    /// <summary>
    /// Builds deterministic board layouts for verification tools.
    ///
    /// Random play almost never produces the situations worth checking — adjacent special items, or a
    /// specific explosion overlapping a chalice box — so they are planted instead.
    ///
    /// Filling the board with stone first makes an ideal measuring grid: stone never falls and is
    /// never spawned by refill, so any cell that held stone before an explosion and does not
    /// afterwards was destroyed by it. That survives gravity and refill running before the turn ends.
    /// </summary>
    internal static class BoardScratchpad
    {
        public const string StoneCode = "s";

        public static ItemCatalog LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ProjectPaths.ItemCatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[BoardScratchpad] Item catalog not found.");
            }

            return catalog;
        }

        /// <summary>Replaces every cell with stone.</summary>
        public static void FillWithStone(Board board, ItemCatalog catalog)
        {
            Clear(board);

            for (var y = 0; y < board.Height; y++)
            {
                for (var x = 0; x < board.Width; x++)
                {
                    Place(board, catalog, StoneCode, new Vector2Int(x, y));
                }
            }
        }

        public static void Clear(Board board)
        {
            for (var y = 0; y < board.Height; y++)
            {
                for (var x = 0; x < board.Width; x++)
                {
                    RemoveAt(board, new Vector2Int(x, y));
                }
            }
        }

        /// <summary>
        /// Places an item, first clearing every cell of its footprint. Clearing the whole footprint
        /// matters for the 2x2 chalice box: overwriting only the anchor would orphan the three other
        /// items, leaving them rendered but absent from the model.
        /// </summary>
        public static GridItem Place(Board board, ItemCatalog catalog, string code, Vector2Int origin)
        {
            var prefab = catalog.GetPrefab(code);
            if (prefab == null)
            {
                Debug.LogError($"[BoardScratchpad] No prefab for code '{code}'.");
                return null;
            }

            var size = prefab.Size;
            for (var dx = 0; dx < size.x; dx++)
            {
                for (var dy = 0; dy < size.y; dy++)
                {
                    RemoveAt(board, origin + new Vector2Int(dx, dy));
                }
            }

            var instance = Object.Instantiate(prefab, board.ItemsRoot);
            instance.name = $"{prefab.name}_{origin.x}_{origin.y}";
            board.Place(instance, origin);
            return instance;
        }

        /// <summary>Counts cells holding an item of a given type.</summary>
        public static int Count<T>(Board board) where T : GridItem
        {
            var count = 0;

            for (var y = 0; y < board.Height; y++)
            {
                for (var x = 0; x < board.Width; x++)
                {
                    if (board.GetItem(new Vector2Int(x, y)) is T)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        /// <summary>Finds the first item of a given type, or null.</summary>
        public static T Find<T>(Board board) where T : GridItem
        {
            for (var y = 0; y < board.Height; y++)
            {
                for (var x = 0; x < board.Width; x++)
                {
                    if (board.GetItem(new Vector2Int(x, y)) is T match)
                    {
                        return match;
                    }
                }
            }

            return null;
        }

        private static void RemoveAt(Board board, Vector2Int cell)
        {
            var item = board.GetItem(cell);
            if (item == null)
            {
                return;
            }

            board.Remove(item);
            Object.DestroyImmediate(item.gameObject);
        }
    }
}

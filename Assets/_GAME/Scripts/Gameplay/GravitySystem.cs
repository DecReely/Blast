using System.Collections.Generic;
using Blast.Items;
using UnityEngine;

namespace Blast.Gameplay
{
    /// <summary>
    /// Applies gravity to the board and tops the columns up with new cubes.
    ///
    /// The whole settle is resolved on the grid in one pass and the resulting movements are handed
    /// to the animator afterwards. Deciding the final layout up front — rather than stepping items
    /// down cell by cell as they animate — means the board is never in a half-fallen state, so
    /// nothing can query it mid-fall and get a contradictory answer.
    ///
    /// Movement is strictly vertical, so each column is independent and can be collapsed on its own.
    /// </summary>
    public sealed class GravitySystem
    {
        private readonly ItemCatalog _catalog;

        public GravitySystem(ItemCatalog catalog)
        {
            _catalog = catalog;
        }

        /// <summary>
        /// Settles every column and refills the gaps this opened at the top, appending the resulting
        /// movements to <paramref name="output"/>.
        /// </summary>
        public void Resolve(Board board, List<FallRequest> output)
        {
            for (var x = 0; x < board.Width; x++)
            {
                var firstFreeRow = CollapseColumn(board, x, output);
                RefillColumn(board, x, firstFreeRow, output);
            }
        }

        /// <summary>
        /// Drops everything in a column onto the highest thing beneath it and returns the lowest row
        /// that is still empty and reachable from above.
        ///
        /// Immovable items (stone, chalice box) act as floors: the scan resumes above them, which is
        /// also what leaves a sealed pocket underneath a stone permanently empty — correct, because
        /// items only ever move vertically and nothing could reach it.
        /// </summary>
        private static int CollapseColumn(Board board, int x, List<FallRequest> output)
        {
            var writeRow = 0;

            for (var y = 0; y < board.Height; y++)
            {
                var item = board.GetItem(new Vector2Int(x, y));

                if (item == null)
                {
                    continue;
                }

                if (!item.CanFall)
                {
                    // A blocker seals everything below it; carry on from just above.
                    writeRow = y + 1;
                    continue;
                }

                if (y != writeRow)
                {
                    var from = item.transform.position;
                    var targetCell = new Vector2Int(x, writeRow);
                    board.MoveTo(item, targetCell);
                    output.Add(new FallRequest(item, from, board.CellToWorld(targetCell)));
                }

                writeRow++;
            }

            return writeRow;
        }

        /// <summary>
        /// Fills the empty top of a column with new random cubes.
        ///
        /// They are registered in their destination cell immediately and start their fall from above
        /// the board, so the spawn stack arrives in order without needing per-item delays.
        /// </summary>
        private void RefillColumn(Board board, int x, int firstFreeRow, List<FallRequest> output)
        {
            var spawnIndex = 0;

            for (var y = firstFreeRow; y < board.Height; y++)
            {
                var cell = new Vector2Int(x, y);
                if (!board.GetCell(cell).IsEmpty)
                {
                    continue;
                }

                var prefab = _catalog.GetRandomCubePrefab();
                if (prefab == null)
                {
                    return;
                }

                var cube = Object.Instantiate(prefab, board.ItemsRoot);
                cube.name = $"{prefab.name}_{x}_{y}";

                board.Place(cube, cell);

                // Stack the spawns above the grid so the one destined for the lowest gap starts
                // lowest and therefore lands first.
                var spawnCell = new Vector2Int(x, board.Height + spawnIndex);
                var from = board.CellToWorld(spawnCell);
                cube.transform.position = from;
                spawnIndex++;

                output.Add(new FallRequest(cube, from, board.CellToWorld(cell)));
            }
        }
    }
}

using System.Collections.Generic;
using System.Text;
using Blast.Gameplay;
using Blast.Items;
using Blast.Levels;
using UnityEditor;
using UnityEngine;

namespace Blast.EditorTools
{
    /// <summary>
    /// Checks that each combo clears exactly the cells the case study describes.
    ///
    /// Random play almost never lines special items up next to each other, so the configurations are
    /// planted deliberately. The board is first filled entirely with stone, which makes an ideal
    /// probe: stone never falls and is never spawned by refill, so any cell that held stone before
    /// and does not afterwards was destroyed by the explosion. That gives an exact footprint even
    /// though gravity and refill run before the turn ends.
    /// </summary>
    public static class ComboVerificationTool
    {
        private sealed class ComboCase
        {
            public string Name;
            public string[] MemberCodes;

            /// <summary>Cells the pattern covers, counted from the case study's description.</summary>
            public int PatternCells;

            /// <summary>
            /// Only stone is counted, and the planted members replaced stone in their own cells, so
            /// those cells are not part of the measurement. Every configuration here places its
            /// members inside the pattern, so the difference is exactly the member count.
            /// </summary>
            public int ExpectedClearedStones => PatternCells - MemberCodes.Length;
        }

        [MenuItem("Dream Games/Debug/Verify Combos")]
        public static void Verify()
        {
            // Level 1 is 10x7. The centre tap and the expected counts below are derived from those
            // dimensions, with the combo centred on the tapped cell.
            var tappedCell = new Vector2Int(4, 3);

            var cases = new[]
            {
                new ComboCase
                {
                    Name = "Rocket-Rocket (one full row + one full column)",
                    MemberCodes = new[] { LevelCodes.HorizontalRocket, LevelCodes.VerticalRocket },

                    // Row of 10 plus column of 7, sharing the centre cell.
                    PatternCells = 10 + 7 - 1
                },
                new ComboCase
                {
                    Name = "TNT-TNT (7x7 area)",
                    MemberCodes = new[] { LevelCodes.Tnt, LevelCodes.Tnt },
                    PatternCells = 7 * 7
                },
                new ComboCase
                {
                    Name = "TNT-Rocket (3 rows + 3 columns)",
                    MemberCodes = new[] { LevelCodes.Tnt, LevelCodes.HorizontalRocket },

                    // Three rows of 10 plus three columns of 7, overlapping in a 3x3 block.
                    PatternCells = (3 * 10) + (3 * 7) - (3 * 3)
                }
            };

            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(GameAssetsBootstrapTool.ItemCatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[ComboVerification] Item catalog not found.");
                return;
            }

            var report = new StringBuilder();
            var failures = 0;

            LevelPreviewTool.RunHeadless(context =>
            {
                foreach (var comboCase in cases)
                {
                    // Reload so each case starts from a clean board, move count and coordinator.
                    context.Session.LoadLevel(1);

                    if (!RunCase(context, catalog, comboCase, tappedCell, report))
                    {
                        failures++;
                    }
                }
            });

            var summary = $"Combo verification: {cases.Length - failures}/{cases.Length} passed.\n{report}";

            if (failures == 0)
            {
                Debug.Log(summary);
            }
            else
            {
                Debug.LogError(summary);
            }
        }

        private static bool RunCase(
            LevelPreviewTool.Context context,
            ItemCatalog catalog,
            ComboCase comboCase,
            Vector2Int tappedCell,
            StringBuilder report)
        {
            var board = context.Board;

            FillWithStone(board, catalog);

            // Plant the group in a horizontal run starting at the tapped cell so they are adjacent.
            for (var i = 0; i < comboCase.MemberCodes.Length; i++)
            {
                PlaceAt(board, catalog, comboCase.MemberCodes[i], tappedCell + new Vector2Int(i, 0));
            }

            var stoneBefore = CollectStoneCells(board);

            context.Session.Coordinator.HandleWorldTap(board.CellToWorld(tappedCell));
            context.Ticker.RunUntilIdle(context.Session.Coordinator);

            var cleared = 0;
            foreach (var cell in stoneBefore)
            {
                if (board.GetItem(cell) is not Stone)
                {
                    cleared++;
                }
            }

            var passed = cleared == comboCase.ExpectedClearedStones;
            report.AppendLine(
                $"  {(passed ? "PASS" : "FAIL")} {comboCase.Name}: cleared {cleared} stones, " +
                $"expected {comboCase.ExpectedClearedStones} " +
                $"({comboCase.PatternCells} pattern cells minus {comboCase.MemberCodes.Length} members)");

            return passed;
        }

        /// <summary>
        /// Replaces the whole board with stone. Stone is immovable and never refilled, so the board
        /// stays a stable grid of probes for the duration of the explosion.
        /// </summary>
        private static void FillWithStone(Board board, ItemCatalog catalog)
        {
            for (var y = 0; y < board.Height; y++)
            {
                for (var x = 0; x < board.Width; x++)
                {
                    var existing = board.GetItem(new Vector2Int(x, y));
                    if (existing == null)
                    {
                        continue;
                    }

                    board.Remove(existing);
                    Object.DestroyImmediate(existing.gameObject);
                }
            }

            for (var y = 0; y < board.Height; y++)
            {
                for (var x = 0; x < board.Width; x++)
                {
                    PlaceAt(board, catalog, "s", new Vector2Int(x, y));
                }
            }
        }

        private static void PlaceAt(Board board, ItemCatalog catalog, string code, Vector2Int cell)
        {
            var existing = board.GetItem(cell);
            if (existing != null)
            {
                board.Remove(existing);
                Object.DestroyImmediate(existing.gameObject);
            }

            var prefab = catalog.GetPrefab(code);
            if (prefab == null)
            {
                Debug.LogError($"[ComboVerification] No prefab for code '{code}'.");
                return;
            }

            var instance = Object.Instantiate(prefab, board.ItemsRoot);
            instance.name = $"{prefab.name}_{cell.x}_{cell.y}";
            board.Place(instance, cell);
        }

        private static List<Vector2Int> CollectStoneCells(Board board)
        {
            var cells = new List<Vector2Int>();

            for (var y = 0; y < board.Height; y++)
            {
                for (var x = 0; x < board.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (board.GetItem(cell) is Stone)
                    {
                        cells.Add(cell);
                    }
                }
            }

            return cells;
        }
    }
}

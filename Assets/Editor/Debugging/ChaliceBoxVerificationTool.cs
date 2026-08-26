using System.Text;
using Blast.Gameplay;
using Blast.Items;
using Blast.Levels;
using UnityEditor;
using UnityEngine;

namespace Blast.EditorTools
{
    /// <summary>
    /// Checks the chalice box's two damage phases against the worked examples in the case study.
    ///
    /// Most cases drive the box directly rather than through a real explosion, because the rule being
    /// tested is precisely "how does the box interpret several hits from one source" and calling it
    /// with a shared <see cref="DamageInfo"/> expresses that exactly. One end-to-end case then
    /// confirms the explosion system really does share a source across the cells it covers.
    /// </summary>
    public static class ChaliceBoxVerificationTool
    {
        [MenuItem("Dream Games/Debug/Verify Chalice Box")]
        public static void Verify()
        {
            var catalog = BoardScratchpad.LoadCatalog();
            if (catalog == null)
            {
                return;
            }

            var report = new StringBuilder();
            var failures = 0;

            failures += CheckRuleCases(catalog, report);
            failures += CheckEndToEnd(catalog, report);

            var summary = $"Chalice box verification:\n{report}";

            if (failures == 0)
            {
                Debug.Log(summary);
            }
            else
            {
                Debug.LogError($"{summary}\n{failures} failure(s).");
            }
        }

        private static int CheckRuleCases(ItemCatalog catalog, StringBuilder report)
        {
            var failures = 0;

            // A TNT covering all four cells is still one source, so the doors take exactly one damage
            // and that source does not go on to collect chalices.
            failures += Expect(
                report,
                "Door phase: TNT over 4 cells deals 1 damage and collects nothing",
                actual: RunHits(catalog, DamageInfo.FromExplosion(), cells: 4, out var afterTnt) ? -1 : afterTnt,
                expected: 10);

            // "an adjacent blast with 8 neighbour cubes deals 8 damage" — but only once the doors are
            // open. While they hold, every source is worth exactly one.
            failures += Expect(
                report,
                "Door phase: blast with 8 adjacent cubes deals 1 damage",
                actual: RunHits(catalog, DamageInfo.FromBlast(8), cells: 1, out var afterBlast) ? -1 : afterBlast,
                expected: 10);

            // "a rocket deals 2 damage" — one source crossing two of the box's cells.
            failures += Expect(
                report,
                "Chalice phase: rocket crossing 2 cells collects 2",
                actual: RunOpenedBox(catalog, DamageInfo.FromExplosion(), cells: 2, out var afterRocket) ? -1 : afterRocket,
                expected: 8);

            failures += Expect(
                report,
                "Chalice phase: TNT covering 4 cells collects 4",
                actual: RunOpenedBox(catalog, DamageInfo.FromExplosion(), cells: 4, out var afterArea) ? -1 : afterArea,
                expected: 6);

            failures += Expect(
                report,
                "Chalice phase: blast with 8 adjacent cubes collects 8",
                actual: RunOpenedBox(catalog, DamageInfo.FromBlast(8), cells: 1, out var afterEight) ? -1 : afterEight,
                expected: 2);

            // Ten chalices empties the box, which should then report itself as finished.
            var destroyed = RunOpenedBox(catalog, DamageInfo.FromBlast(10), cells: 1, out var afterAll);
            failures += Expect(report, "Chalice phase: 10 chalices empties and clears the box",
                actual: destroyed ? afterAll : -1, expected: 0);

            return failures;
        }

        /// <summary>
        /// Applies one damage source to a fresh box, repeated for however many of its cells the source
        /// covered. Returns whether the box reported itself finished.
        /// </summary>
        private static bool RunHits(ItemCatalog catalog, DamageInfo damage, int cells, out int chalicesRemaining)
        {
            var box = CreateBox(catalog);
            var destroyed = false;

            for (var i = 0; i < cells; i++)
            {
                destroyed |= box.TryTakeDamage(damage);
            }

            chalicesRemaining = box.ChalicesRemaining;
            Object.DestroyImmediate(box.gameObject);
            return destroyed;
        }

        /// <summary>Same, but on a box whose doors have already been broken by an earlier source.</summary>
        private static bool RunOpenedBox(ItemCatalog catalog, DamageInfo damage, int cells, out int chalicesRemaining)
        {
            var box = CreateBox(catalog);

            // A separate, earlier source opens the doors.
            box.TryTakeDamage(DamageInfo.FromExplosion());

            var destroyed = false;
            for (var i = 0; i < cells; i++)
            {
                destroyed |= box.TryTakeDamage(damage);
            }

            chalicesRemaining = box.ChalicesRemaining;
            Object.DestroyImmediate(box.gameObject);
            return destroyed;
        }

        private static ChaliceBox CreateBox(ItemCatalog catalog)
        {
            var prefab = (ChaliceBox)catalog.GetPrefab(LevelCodes.ChaliceBoxBottomLeft);
            return Object.Instantiate(prefab);
        }

        /// <summary>
        /// Fires a real TNT whose area overlaps two of the box's cells, confirming the explosion
        /// system shares one damage source across every cell it covers.
        /// </summary>
        private static int CheckEndToEnd(ItemCatalog catalog, StringBuilder report)
        {
            var failures = 0;

            LevelPreviewTool.RunHeadless(context =>
            {
                context.Session.LoadLevel(1);

                var board = context.Board;
                BoardScratchpad.FillWithStone(board, catalog);

                // Box occupies (4,3)-(5,4). The TNT's 5x5 reaches y<=3, so it covers exactly the box's
                // bottom two cells.
                BoardScratchpad.Place(board, catalog, LevelCodes.ChaliceBoxBottomLeft, new Vector2Int(4, 3));
                var tntCell = new Vector2Int(4, 1);
                BoardScratchpad.Place(board, catalog, LevelCodes.Tnt, tntCell);

                context.Session.Coordinator.HandleWorldTap(board.CellToWorld(tntCell));
                context.Ticker.RunUntilIdle(context.Session.Coordinator);

                var box = BoardScratchpad.Find<ChaliceBox>(board);
                failures += Expect(
                    report,
                    "End to end: TNT covering 2 box cells breaks the doors only",
                    actual: box == null ? -1 : box.ChalicesRemaining,
                    expected: 10);
            });

            return failures;
        }

        private static int Expect(StringBuilder report, string name, int actual, int expected)
        {
            var passed = actual == expected;
            report.AppendLine($"  {(passed ? "PASS" : "FAIL")} {name} (chalices left {actual}, expected {expected})");
            return passed ? 0 : 1;
        }
    }
}

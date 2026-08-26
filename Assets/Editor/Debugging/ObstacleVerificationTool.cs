using System.Text;
using Blast.Gameplay;
using Blast.Items;
using Blast.Levels;
using UnityEditor;
using UnityEngine;

namespace Blast.EditorTools
{
    /// <summary>
    /// Checks the vase and stone damage rules, and that a level reports a win the moment its last
    /// obstacle is cleared.
    ///
    /// The damage rules are driven directly rather than through a real board. They are statements
    /// about how an item interprets a hit, and expressing them as such keeps them independent of any
    /// particular layout — an earlier layout-based attempt failed only because two TNTs placed side
    /// by side correctly merged into a combo, testing something other than what was intended.
    /// </summary>
    public static class ObstacleVerificationTool
    {
        [MenuItem("Dream Games/Debug/Verify Obstacles")]
        public static void Verify()
        {
            var catalog = BoardScratchpad.LoadCatalog();
            if (catalog == null)
            {
                return;
            }

            var report = new StringBuilder();
            var failures = 0;

            failures += CheckVase(catalog, report);
            failures += CheckStone(catalog, report);
            failures += CheckWinCondition(catalog, report);

            var summary = $"Obstacle verification:\n{report}";

            if (failures == 0)
            {
                Debug.Log(summary);
            }
            else
            {
                Debug.LogError($"{summary}\n{failures} failure(s).");
            }
        }

        private static int CheckVase(ItemCatalog catalog, StringBuilder report)
        {
            var failures = 0;

            var vase = Spawn<Vase>(catalog, "v");
            failures += Expect(report, "Vase survives one explosion hit",
                actual: vase.TryTakeDamage(DamageInfo.FromExplosion()), expected: false);
            failures += Expect(report, "Vase is cleared by the second hit",
                actual: vase.TryTakeDamage(DamageInfo.FromExplosion()), expected: true);
            Object.DestroyImmediate(vase.gameObject);

            // "It takes no more than one damage from a single blast", however many cubes touched it.
            vase = Spawn<Vase>(catalog, "v");
            failures += Expect(report, "Vase takes only one damage from a blast of 8 adjacent cubes",
                actual: vase.TryTakeDamage(DamageInfo.FromBlast(8)), expected: false);
            Object.DestroyImmediate(vase.gameObject);

            return failures;
        }

        private static int CheckStone(ItemCatalog catalog, StringBuilder report)
        {
            var failures = 0;

            var stone = Spawn<Stone>(catalog, BoardScratchpad.StoneCode);
            failures += Expect(report, "Stone ignores an adjacent blast",
                actual: stone.TryTakeDamage(DamageInfo.FromBlast(8)), expected: false);
            failures += Expect(report, "Stone is cleared by one explosion",
                actual: stone.TryTakeDamage(DamageInfo.FromExplosion()), expected: true);
            Object.DestroyImmediate(stone.gameObject);

            return failures;
        }

        /// <summary>
        /// Clearing the last obstacle must win the level. A stone is used because a single explosion
        /// clears it, keeping the setup to one TNT and one obstacle.
        /// </summary>
        private static int CheckWinCondition(ItemCatalog catalog, StringBuilder report)
        {
            var outcome = LevelOutcome.InProgress;
            var stonesLeft = -1;

            LevelPreviewTool.RunHeadless(context =>
            {
                context.Session.LoadLevel(1);

                var board = context.Board;
                BoardScratchpad.Clear(board);

                BoardScratchpad.Place(board, catalog, BoardScratchpad.StoneCode, new Vector2Int(0, 0));
                BoardScratchpad.Place(board, catalog, LevelCodes.Tnt, new Vector2Int(1, 0));

                context.Session.Goals.Initialize(board);

                context.Session.Coordinator.HandleWorldTap(board.CellToWorld(new Vector2Int(1, 0)));
                context.Ticker.RunUntilIdle(context.Session.Coordinator);

                outcome = context.Session.Outcome;
                stonesLeft = context.Session.Goals.StonesRemaining;
            });

            var passed = outcome == LevelOutcome.Won && stonesLeft == 0;
            report.AppendLine(
                $"  {(passed ? "PASS" : "FAIL")} Clearing the last obstacle wins the level " +
                $"(outcome {outcome}, stones left {stonesLeft})");

            return passed ? 0 : 1;
        }

        private static T Spawn<T>(ItemCatalog catalog, string code) where T : GridItem
        {
            return Object.Instantiate((T)catalog.GetPrefab(code));
        }

        private static int Expect(StringBuilder report, string name, bool actual, bool expected)
        {
            var passed = actual == expected;
            report.AppendLine($"  {(passed ? "PASS" : "FAIL")} {name} (cleared: {actual}, expected {expected})");
            return passed ? 0 : 1;
        }
    }
}

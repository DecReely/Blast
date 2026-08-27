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
            RunChecks().Log();
        }

        public static VerificationReport RunChecks()
        {
            var report = new VerificationReport("Obstacle verification");

            report.Absorb(CheckVaseRules());
            report.Absorb(CheckStoneRules());
            report.Absorb(CheckWinCondition());

            return report;
        }

        /// <summary>
        /// A vase survives its first hit and is cleared by the second, and a blast is worth exactly
        /// one hit however many cubes went off beside it.
        /// </summary>
        public static VerificationReport CheckVaseRules()
        {
            var report = new VerificationReport("Vase damage rules");

            var catalog = BoardScratchpad.LoadCatalog();
            if (catalog == null)
            {
                report.Check("Item catalog is available", false);
                return report;
            }

            var vase = Spawn<Vase>(catalog, "v");
            report.Expect("Vase is only cracked by one explosion hit",
                vase.ApplyDamage(DamageInfo.FromExplosion()), DamageResult.Damaged);
            report.Expect("Vase is cleared by the second hit",
                vase.ApplyDamage(DamageInfo.FromExplosion()), DamageResult.Destroyed);
            Object.DestroyImmediate(vase.gameObject);

            // "It takes no more than one damage from a single blast", however many cubes touched it.
            vase = Spawn<Vase>(catalog, "v");
            report.Expect("Vase takes only one damage from a blast of 8 adjacent cubes",
                vase.ApplyDamage(DamageInfo.FromBlast(8)), DamageResult.Damaged);
            Object.DestroyImmediate(vase.gameObject);

            return report;
        }

        /// <summary>Stone ignores blasts entirely and is cleared by any single explosion.</summary>
        public static VerificationReport CheckStoneRules()
        {
            var report = new VerificationReport("Stone damage rules");

            var catalog = BoardScratchpad.LoadCatalog();
            if (catalog == null)
            {
                report.Check("Item catalog is available", false);
                return report;
            }

            var stone = Spawn<Stone>(catalog, BoardScratchpad.StoneCode);
            report.Expect("Stone ignores an adjacent blast",
                stone.ApplyDamage(DamageInfo.FromBlast(8)), DamageResult.Ignored);
            report.Expect("Stone is cleared by one explosion",
                stone.ApplyDamage(DamageInfo.FromExplosion()), DamageResult.Destroyed);
            Object.DestroyImmediate(stone.gameObject);

            return report;
        }

        /// <summary>
        /// Clearing the last obstacle must win the level. A stone is used because a single explosion
        /// clears it, keeping the setup to one TNT and one obstacle.
        /// </summary>
        public static VerificationReport CheckWinCondition()
        {
            var report = new VerificationReport("Win condition");

            var catalog = BoardScratchpad.LoadCatalog();
            if (catalog == null)
            {
                report.Check("Item catalog is available", false);
                return report;
            }

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

            report.Check(
                "Clearing the last obstacle wins the level",
                outcome == LevelOutcome.Won && stonesLeft == 0,
                $"outcome {outcome}, stones left {stonesLeft}");

            return report;
        }

        private static T Spawn<T>(ItemCatalog catalog, string code) where T : GridItem
        {
            return Object.Instantiate((T)catalog.GetPrefab(code));
        }
    }
}

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
            RunChecks().Log();
        }

        public static VerificationReport RunChecks()
        {
            var report = new VerificationReport("Chalice box verification");

            report.Absorb(CheckDamagePhases());
            report.Absorb(CheckEndToEnd());

            return report;
        }

        /// <summary>
        /// The counting rules for both phases: one damage per source while the doors hold, then one
        /// chalice per covered cell or per adjacent cube once they are open.
        /// </summary>
        public static VerificationReport CheckDamagePhases()
        {
            var report = new VerificationReport("Chalice box damage phases");

            var catalog = BoardScratchpad.LoadCatalog();
            if (catalog == null)
            {
                report.Check("Item catalog is available", false);
                return report;
            }

            // A TNT covering all four cells is still one source, so the doors take exactly one damage
            // and that source does not go on to collect chalices.
            report.Expect(
                "Door phase: TNT over 4 cells deals 1 damage and collects nothing",
                RunHits(catalog, DamageInfo.FromExplosion(), cells: 4, opened: false),
                10);

            // "an adjacent blast with 8 neighbour cubes deals 8 damage" — but only once the doors are
            // open. While they hold, every source is worth exactly one.
            report.Expect(
                "Door phase: blast with 8 adjacent cubes deals 1 damage",
                RunHits(catalog, DamageInfo.FromBlast(8), cells: 1, opened: false),
                10);

            // "a rocket deals 2 damage" — one source crossing two of the box's cells.
            report.Expect(
                "Chalice phase: rocket crossing 2 cells collects 2",
                RunHits(catalog, DamageInfo.FromExplosion(), cells: 2, opened: true),
                8);

            report.Expect(
                "Chalice phase: TNT covering 4 cells collects 4",
                RunHits(catalog, DamageInfo.FromExplosion(), cells: 4, opened: true),
                6);

            report.Expect(
                "Chalice phase: blast with 8 adjacent cubes collects 8",
                RunHits(catalog, DamageInfo.FromBlast(8), cells: 1, opened: true),
                2);

            // Ten chalices empties the box, which should then report itself as finished.
            var box = CreateBox(catalog, opened: true);
            var result = box.ApplyDamage(DamageInfo.FromBlast(10));

            report.Expect("Chalice phase: 10 chalices empties and clears the box",
                result, DamageResult.Destroyed);
            report.Expect("  and it holds nothing afterwards", box.ChalicesRemaining, 0);

            Object.DestroyImmediate(box.gameObject);

            return report;
        }

        /// <summary>
        /// Fires a real TNT whose area overlaps two of the box's cells, confirming the explosion
        /// system shares one damage source across every cell it covers.
        /// </summary>
        public static VerificationReport CheckEndToEnd()
        {
            var report = new VerificationReport("Chalice box, end to end");

            var catalog = BoardScratchpad.LoadCatalog();
            if (catalog == null)
            {
                report.Check("Item catalog is available", false);
                return report;
            }

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

                report.Expect(
                    "TNT covering 2 box cells breaks the doors only",
                    box == null ? -1 : box.ChalicesRemaining,
                    10);
            });

            return report;
        }

        /// <summary>
        /// Applies one damage source to a fresh box, repeated for however many of its cells the source
        /// covered, and reports how many chalices are left.
        /// </summary>
        private static int RunHits(ItemCatalog catalog, DamageInfo damage, int cells, bool opened)
        {
            var box = CreateBox(catalog, opened);

            for (var i = 0; i < cells; i++)
            {
                box.ApplyDamage(damage);
            }

            var remaining = box.ChalicesRemaining;
            Object.DestroyImmediate(box.gameObject);
            return remaining;
        }

        /// <param name="opened">
        /// True to spend a separate, earlier source on the doors first, putting the box in its
        /// chalice phase.
        /// </param>
        private static ChaliceBox CreateBox(ItemCatalog catalog, bool opened)
        {
            var prefab = (ChaliceBox)catalog.GetPrefab(LevelCodes.ChaliceBoxBottomLeft);
            var box = Object.Instantiate(prefab);

            if (opened)
            {
                box.ApplyDamage(DamageInfo.FromExplosion());
            }

            return box;
        }
    }
}

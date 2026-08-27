using Blast.Gameplay;
using Blast.Items;
using Blast.Levels;
using UnityEditor;
using UnityEngine;

namespace Blast.EditorTools
{
    /// <summary>
    /// Checks the turn-flow rules that are easy to get subtly wrong and hard to notice while playing:
    /// input gating, taps that are not moves, and the exact boundary between winning and losing on
    /// the final move.
    ///
    /// These are all statements about <em>when</em> something happens rather than what it looks like,
    /// so they are driven through the real session and coordinator rather than by inspecting state
    /// directly. A test that reached past the coordinator would not prove the game behaves this way.
    /// </summary>
    public static class EdgeCaseVerificationTool
    {
        [MenuItem("Dream Games/Debug/Verify Edge Cases")]
        public static void Verify()
        {
            RunChecks().Log();
        }

        public static VerificationReport RunChecks()
        {
            var report = new VerificationReport("Edge case verification");

            var catalog = BoardScratchpad.LoadCatalog();
            if (catalog == null)
            {
                report.Check("Item catalog is available", false);
                return report;
            }

            LevelPreviewTool.RunHeadless(context =>
            {
                CheckTapDuringResolveIsIgnored(context, catalog, report);
                CheckNonMoveTapsCostNothing(context, catalog, report);
                CheckLossHappensOnlyAtZeroMoves(context, catalog, report);
                CheckWinOnTheLastMove(context, catalog, report);
                CheckMultiBoxChaliceGoal(context, report);
                CheckNoDeadlockAfterRefill(context, report);
            });

            return report;
        }

        /// <summary>
        /// A tap arriving while the board is still exploding or falling must be dropped entirely, not
        /// queued: it would otherwise spend a move against a board the player never saw.
        /// </summary>
        private static void CheckTapDuringResolveIsIgnored(
            LevelPreviewTool.Context context, ItemCatalog catalog, VerificationReport report)
        {
            context.Session.LoadLevel(1);

            var board = context.Board;
            var coordinator = context.Session.Coordinator;

            BoardScratchpad.Clear(board);

            // A rocket sweep runs over many frames, which gives a wide window to tap into.
            BoardScratchpad.Place(board, catalog, LevelCodes.HorizontalRocket, new Vector2Int(0, 0));

            // A blastable pair elsewhere, so the interrupting tap would be legal if it were accepted.
            BoardScratchpad.Place(board, catalog, "r", new Vector2Int(0, 5));
            BoardScratchpad.Place(board, catalog, "r", new Vector2Int(1, 5));

            coordinator.HandleWorldTap(board.CellToWorld(new Vector2Int(0, 0)));

            // Part way through the sweep, not at the end of it.
            context.Ticker.Advance(3);

            var resolving = coordinator.IsResolving;
            var movesMidTurn = context.Session.Moves.Remaining;

            coordinator.HandleWorldTap(board.CellToWorld(new Vector2Int(0, 5)));

            var movesAfterInterrupt = context.Session.Moves.Remaining;
            context.Ticker.RunUntilIdle(coordinator);

            report.Check(
                "A tap during a resolving turn is ignored",
                resolving && movesAfterInterrupt == movesMidTurn,
                $"resolving {resolving}, moves {movesMidTurn} -> {movesAfterInterrupt}");
        }

        /// <summary>
        /// Only a blastable group or a special is a move. Tapping anything else must leave the move
        /// count alone, which is the difference between a forgiving game and a frustrating one.
        /// </summary>
        private static void CheckNonMoveTapsCostNothing(
            LevelPreviewTool.Context context, ItemCatalog catalog, VerificationReport report)
        {
            context.Session.LoadLevel(1);

            var board = context.Board;
            BoardScratchpad.Clear(board);

            // A cube with no same-coloured neighbour, an obstacle, and an empty cell beside them.
            BoardScratchpad.Place(board, catalog, "r", new Vector2Int(0, 0));
            BoardScratchpad.Place(board, catalog, "b", new Vector2Int(1, 0));
            BoardScratchpad.Place(board, catalog, BoardScratchpad.StoneCode, new Vector2Int(2, 0));

            var before = context.Session.Moves.Remaining;

            ExpectNoMove(context, report, "a lone cube", new Vector2Int(0, 0), before);
            ExpectNoMove(context, report, "a stone", new Vector2Int(2, 0), before);
            ExpectNoMove(context, report, "an empty cell", new Vector2Int(5, 5), before);
            ExpectNoMove(context, report, "outside the board", new Vector2Int(-1, -1), before);
        }

        private static void ExpectNoMove(
            LevelPreviewTool.Context context,
            VerificationReport report,
            string what,
            Vector2Int cell,
            int expectedMoves)
        {
            var coordinator = context.Session.Coordinator;

            coordinator.HandleWorldTap(context.Board.CellToWorld(cell));

            var remaining = context.Session.Moves.Remaining;

            report.Check(
                $"Tapping {what} is not a move",
                remaining == expectedMoves && !coordinator.IsResolving,
                $"moves {remaining}, expected {expectedMoves}");
        }

        /// <summary>
        /// Spending the second-to-last move must not end the level, and spending the last one must.
        /// Both halves matter: an off-by-one either steals a move or hands out a free one.
        /// </summary>
        private static void CheckLossHappensOnlyAtZeroMoves(
            LevelPreviewTool.Context context, ItemCatalog catalog, VerificationReport report)
        {
            context.Session.LoadLevel(1);

            var board = context.Board;
            var moves = context.Session.Moves;

            // Wind the counter down without playing, so the level is untouched at two moves left.
            while (moves.Remaining > 2)
            {
                moves.Spend();
            }

            BoardScratchpad.Clear(board);

            // A stone keeps the goal unmet, so the level can only end by running out of moves.
            BoardScratchpad.Place(board, catalog, BoardScratchpad.StoneCode, new Vector2Int(0, 0));
            context.Session.Goals.Initialize(board);

            PlantAndBlastPair(context, catalog, new Vector2Int(0, 4));
            report.Expect("Second-to-last move does not end the level",
                context.Session.Outcome, LevelOutcome.InProgress);

            PlantAndBlastPair(context, catalog, new Vector2Int(0, 4));
            report.Expect("The level fails exactly when the last move is spent",
                context.Session.Outcome, LevelOutcome.Failed);
        }

        /// <summary>
        /// Clearing the final obstacle with the final move is a win, not a loss. The check has to run
        /// after the board settles, because a rocket still in flight can clear the last goal.
        /// </summary>
        private static void CheckWinOnTheLastMove(
            LevelPreviewTool.Context context, ItemCatalog catalog, VerificationReport report)
        {
            context.Session.LoadLevel(1);

            var board = context.Board;
            var moves = context.Session.Moves;

            while (moves.Remaining > 1)
            {
                moves.Spend();
            }

            BoardScratchpad.Clear(board);

            // One stone and one TNT: detonating the TNT clears the level's only obstacle.
            BoardScratchpad.Place(board, catalog, BoardScratchpad.StoneCode, new Vector2Int(0, 0));
            BoardScratchpad.Place(board, catalog, LevelCodes.Tnt, new Vector2Int(1, 0));
            context.Session.Goals.Initialize(board);

            context.Session.Coordinator.HandleWorldTap(board.CellToWorld(new Vector2Int(1, 0)));
            context.Ticker.RunUntilIdle(context.Session.Coordinator);

            report.Expect("Winning on the last move counts as a win",
                context.Session.Outcome, LevelOutcome.Won);

            report.Expect("  and the move counter really was at zero",
                moves.Remaining, 0);
        }

        /// <summary>
        /// Levels 3 and 9 place four boxes and nothing else. The goal has to scale with them, or three
        /// of the four would be decorative and the level would be winnable without touching them.
        /// </summary>
        private static void CheckMultiBoxChaliceGoal(
            LevelPreviewTool.Context context, VerificationReport report)
        {
            foreach (var levelNumber in new[] { 3, 9 })
            {
                context.Session.LoadLevel(levelNumber);

                var boxes = CountChaliceBoxes(context.Board);
                var goal = context.Session.Goals.ChaliceGoal;

                report.Expect(
                    $"Level {levelNumber}: every one of its {boxes} boxes counts towards the goal",
                    goal,
                    boxes * 10);
            }
        }

        private static int CountChaliceBoxes(Board board)
        {
            var boxes = 0;

            for (var y = 0; y < board.Height; y++)
            {
                for (var x = 0; x < board.Width; x++)
                {
                    var cell = new Vector2Int(x, y);

                    // A box covers four cells; count it only at its anchor.
                    if (board.GetItem(cell) is ChaliceBox box && box.Origin == cell)
                    {
                        boxes++;
                    }
                }
            }

            return boxes;
        }

        /// <summary>
        /// A settled board must always offer the player something to tap. If refill could ever leave a
        /// board with no blastable pair and no special, the level would be stuck without being lost —
        /// the one failure state the flow has no answer for.
        /// </summary>
        private static void CheckNoDeadlockAfterRefill(
            LevelPreviewTool.Context context, VerificationReport report)
        {
            const int turnsPerLevel = 12;

            var groupFinder = new GroupFinder();
            var comboDetector = new ComboDetector();
            var deadlocks = 0;
            var turns = 0;

            for (var levelNumber = 1; levelNumber <= 10; levelNumber++)
            {
                context.Session.LoadLevel(levelNumber);
                Random.InitState(levelNumber * 104729);

                var options = new System.Collections.Generic.List<BoardTapScanner.TapOption>();

                for (var turn = 0; turn < turnsPerLevel; turn++)
                {
                    if (context.Session.Outcome != LevelOutcome.InProgress)
                    {
                        break;
                    }

                    BoardTapScanner.Collect(context.Board, groupFinder, comboDetector, options);

                    if (options.Count == 0)
                    {
                        deadlocks++;
                        report.Note($"  level {levelNumber} turn {turn}: no legal tap available");
                        break;
                    }

                    var choice = options[Random.Range(0, options.Count)];
                    context.Session.Coordinator.HandleWorldTap(context.Board.CellToWorld(choice.Cell));
                    context.Ticker.RunUntilIdle(context.Session.Coordinator);
                    turns++;
                }
            }

            report.Check(
                "A settled board always offers a legal tap",
                deadlocks == 0,
                $"{turns} turns across 10 levels, {deadlocks} deadlocks");
        }

        /// <summary>Blasts a freshly planted pair, which is always a legal move and never a win.</summary>
        private static void PlantAndBlastPair(
            LevelPreviewTool.Context context, ItemCatalog catalog, Vector2Int at)
        {
            BoardScratchpad.Place(context.Board, catalog, "r", at);
            BoardScratchpad.Place(context.Board, catalog, "r", at + Vector2Int.right);

            context.Session.Coordinator.HandleWorldTap(context.Board.CellToWorld(at));
            context.Ticker.RunUntilIdle(context.Session.Coordinator);
        }
    }
}

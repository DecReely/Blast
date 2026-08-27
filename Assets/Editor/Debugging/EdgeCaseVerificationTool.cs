using System.Text;
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
            var catalog = BoardScratchpad.LoadCatalog();
            if (catalog == null)
            {
                return;
            }

            var report = new StringBuilder();
            var failures = 0;

            LevelPreviewTool.RunHeadless(context =>
            {
                failures += CheckTapDuringResolveIsIgnored(context, catalog, report);
                failures += CheckNonMoveTapsCostNothing(context, catalog, report);
                failures += CheckLossHappensOnlyAtZeroMoves(context, catalog, report);
                failures += CheckWinOnTheLastMove(context, catalog, report);
                failures += CheckMultiBoxChaliceGoal(context, report);
                failures += CheckNoDeadlockAfterRefill(context, report);
            });

            var summary = $"Edge case verification:\n{report}";

            if (failures == 0)
            {
                Debug.Log(summary);
            }
            else
            {
                Debug.LogError($"{summary}\n{failures} failure(s).");
            }
        }

        /// <summary>
        /// A tap arriving while the board is still exploding or falling must be dropped entirely, not
        /// queued: it would otherwise spend a move against a board the player never saw.
        /// </summary>
        private static int CheckTapDuringResolveIsIgnored(
            LevelPreviewTool.Context context, ItemCatalog catalog, StringBuilder report)
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

            var passed = resolving && movesAfterInterrupt == movesMidTurn;
            report.AppendLine(
                $"  {(passed ? "PASS" : "FAIL")} A tap during a resolving turn is ignored " +
                $"(resolving {resolving}, moves {movesMidTurn} -> {movesAfterInterrupt})");

            return passed ? 0 : 1;
        }

        /// <summary>
        /// Only a blastable group or a special is a move. Tapping anything else must leave the move
        /// count alone, which is the difference between a forgiving game and a frustrating one.
        /// </summary>
        private static int CheckNonMoveTapsCostNothing(
            LevelPreviewTool.Context context, ItemCatalog catalog, StringBuilder report)
        {
            context.Session.LoadLevel(1);

            var board = context.Board;
            BoardScratchpad.Clear(board);

            // A cube with no same-coloured neighbour, an obstacle, and an empty cell beside them.
            BoardScratchpad.Place(board, catalog, "r", new Vector2Int(0, 0));
            BoardScratchpad.Place(board, catalog, "b", new Vector2Int(1, 0));
            BoardScratchpad.Place(board, catalog, BoardScratchpad.StoneCode, new Vector2Int(2, 0));

            var before = context.Session.Moves.Remaining;
            var failures = 0;

            failures += ExpectNoMove(context, report, "a lone cube", new Vector2Int(0, 0), before);
            failures += ExpectNoMove(context, report, "a stone", new Vector2Int(2, 0), before);
            failures += ExpectNoMove(context, report, "an empty cell", new Vector2Int(5, 5), before);
            failures += ExpectNoMove(context, report, "outside the board", new Vector2Int(-1, -1), before);

            return failures;
        }

        private static int ExpectNoMove(
            LevelPreviewTool.Context context,
            StringBuilder report,
            string what,
            Vector2Int cell,
            int expectedMoves)
        {
            var coordinator = context.Session.Coordinator;

            coordinator.HandleWorldTap(context.Board.CellToWorld(cell));

            var remaining = context.Session.Moves.Remaining;
            var passed = remaining == expectedMoves && !coordinator.IsResolving;

            report.AppendLine(
                $"  {(passed ? "PASS" : "FAIL")} Tapping {what} is not a move " +
                $"(moves {remaining}, expected {expectedMoves})");

            return passed ? 0 : 1;
        }

        /// <summary>
        /// Spending the second-to-last move must not end the level, and spending the last one must.
        /// Both halves matter: an off-by-one either steals a move or hands out a free one.
        /// </summary>
        private static int CheckLossHappensOnlyAtZeroMoves(
            LevelPreviewTool.Context context, ItemCatalog catalog, StringBuilder report)
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

            var failures = 0;

            PlantAndBlastPair(context, catalog, new Vector2Int(0, 4));
            failures += Expect(report, "Second-to-last move does not end the level",
                context.Session.Outcome, LevelOutcome.InProgress);

            PlantAndBlastPair(context, catalog, new Vector2Int(0, 4));
            failures += Expect(report, "The level fails exactly when the last move is spent",
                context.Session.Outcome, LevelOutcome.Failed);

            return failures;
        }

        /// <summary>
        /// Clearing the final obstacle with the final move is a win, not a loss. The check has to run
        /// after the board settles, because a rocket still in flight can clear the last goal.
        /// </summary>
        private static int CheckWinOnTheLastMove(
            LevelPreviewTool.Context context, ItemCatalog catalog, StringBuilder report)
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

            var failures = Expect(report, "Winning on the last move counts as a win",
                context.Session.Outcome, LevelOutcome.Won);

            failures += Expect(report, "  and the move counter really was at zero",
                moves.Remaining, 0);

            return failures;
        }

        /// <summary>
        /// Levels 3 and 9 place four boxes and nothing else. The goal has to scale with them, or three
        /// of the four would be decorative and the level would be winnable without touching them.
        /// </summary>
        private static int CheckMultiBoxChaliceGoal(LevelPreviewTool.Context context, StringBuilder report)
        {
            var failures = 0;

            foreach (var levelNumber in new[] { 3, 9 })
            {
                context.Session.LoadLevel(levelNumber);

                var boxes = CountChaliceBoxes(context.Board);
                var goal = context.Session.Goals.ChaliceGoal;

                failures += Expect(
                    report,
                    $"Level {levelNumber}: every one of its {boxes} boxes counts towards the goal",
                    goal,
                    boxes * 10);
            }

            return failures;
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
        private static int CheckNoDeadlockAfterRefill(LevelPreviewTool.Context context, StringBuilder report)
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
                        report.AppendLine($"    level {levelNumber} turn {turn}: no legal tap available");
                        break;
                    }

                    var choice = options[Random.Range(0, options.Count)];
                    context.Session.Coordinator.HandleWorldTap(context.Board.CellToWorld(choice.Cell));
                    context.Ticker.RunUntilIdle(context.Session.Coordinator);
                    turns++;
                }
            }

            var passed = deadlocks == 0;
            report.AppendLine(
                $"  {(passed ? "PASS" : "FAIL")} A settled board always offers a legal tap " +
                $"({turns} turns across 10 levels, {deadlocks} deadlocks)");

            return passed ? 0 : 1;
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

        private static int Expect<T>(StringBuilder report, string name, T actual, T expected)
        {
            var passed = Equals(actual, expected);
            report.AppendLine($"  {(passed ? "PASS" : "FAIL")} {name} (was {actual}, expected {expected})");
            return passed ? 0 : 1;
        }
    }
}

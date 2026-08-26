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
    /// Checks the invariant a settled board must satisfy, and exercises it across every level.
    ///
    /// The check is written independently of <see cref="GravitySystem"/> rather than reusing its
    /// logic, so a bug in the collapse algorithm cannot hide behind a check that makes the same
    /// mistake.
    /// </summary>
    public static class BoardIntegrity
    {
        /// <summary>
        /// Once the board has settled, scanning a column upwards must never find a gap below an item
        /// that is able to fall, and the top of every column must be full.
        ///
        /// The one legal exception is a pocket sealed underneath an immovable item: because movement
        /// is strictly vertical, nothing can ever reach it.
        /// </summary>
        public static List<string> FindViolations(Board board)
        {
            var violations = new List<string>();

            for (var x = 0; x < board.Width; x++)
            {
                var lowestGap = -1;

                for (var y = 0; y < board.Height; y++)
                {
                    var item = board.GetItem(new Vector2Int(x, y));

                    if (item == null)
                    {
                        if (lowestGap < 0)
                        {
                            lowestGap = y;
                        }

                        continue;
                    }

                    if (!item.CanFall)
                    {
                        // Anything below is sealed off and legitimately stays empty.
                        lowestGap = -1;
                        continue;
                    }

                    if (lowestGap >= 0)
                    {
                        violations.Add($"column {x}: {item.name} floats at y={y} above an empty cell at y={lowestGap}");
                        lowestGap = -1;
                    }
                }

                if (lowestGap >= 0)
                {
                    violations.Add($"column {x}: not refilled from y={lowestGap} upwards");
                }
            }

            return violations;
        }

        public static string Describe(Board board)
        {
            var violations = FindViolations(board);
            return violations.Count == 0
                ? "Board integrity OK: no floating items and every reachable cell filled."
                : $"Board integrity FAILED ({violations.Count}):\n  " + string.Join("\n  ", violations);
        }

        /// <summary>
        /// Plays a long series of turns on every level, letting each one fully resolve, and reports
        /// any violation. This is the main regression test for blasting, explosions and refill.
        /// </summary>
        [MenuItem("Dream Games/Debug/Run Board Stress Test")]
        public static void RunStressTest()
        {
            const int turnsPerLevel = 15;
            var report = new StringBuilder();
            var totals = new Totals();

            LevelPreviewTool.RunHeadless(context =>
            {
                for (var levelNumber = 1; levelNumber <= 10; levelNumber++)
                {
                    context.Session.LoadLevel(levelNumber);

                    // Deterministic per level so a failure can be reproduced.
                    Random.InitState(levelNumber * 7919);

                    PlayLevel(context, levelNumber, turnsPerLevel, report, totals);
                }
            });

            var summary =
                $"Board stress test: {totals.Turns} turns " +
                $"({totals.Blasts} blasts, {totals.SpecialsCreated} specials created, " +
                $"{totals.Detonations} detonations), {totals.Violations} violations.\n{report}";

            if (totals.Violations == 0)
            {
                Debug.Log(summary);
            }
            else
            {
                Debug.LogError(summary);
            }
        }

        private sealed class Totals
        {
            public int Turns;
            public int Blasts;
            public int SpecialsCreated;
            public int Detonations;
            public int Violations;
        }

        private static void PlayLevel(
            LevelPreviewTool.Context context,
            int levelNumber,
            int turns,
            StringBuilder report,
            Totals totals)
        {
            var board = context.Board;
            var level = context.Session.CurrentLevel;
            var goals = context.Session.Goals;
            var played = 0;

            report.AppendLine(
                $"  level {levelNumber} ({level.Width}x{level.Height}, {level.MoveCount} moves): " +
                $"goals vases={goals.VasesRemaining} stones={goals.StonesRemaining} chalices={goals.ChaliceGoal}");

            for (var attempt = 0; attempt < turns * 25 && played < turns; attempt++)
            {
                // Obstacles are destructible now, so a level can genuinely end mid-run.
                if (context.Session.Outcome != LevelOutcome.InProgress)
                {
                    break;
                }

                var coordinator = context.Session.Coordinator;

                var cell = new Vector2Int(
                    Random.Range(0, level.Width),
                    Random.Range(0, level.Height));

                var wasSpecial = board.GetItem(cell) is SpecialItem;
                var specialsBefore = CountSpecials(board);
                var movesBefore = context.Session.Moves.Remaining;

                coordinator.HandleWorldTap(board.CellToWorld(cell));

                if (context.Session.Moves.Remaining == movesBefore && !coordinator.IsResolving)
                {
                    // Not a legal tap; costs nothing and changes nothing.
                    continue;
                }

                context.Ticker.RunUntilIdle(coordinator);

                played++;
                totals.Turns++;

                if (wasSpecial)
                {
                    totals.Detonations++;
                }
                else
                {
                    totals.Blasts++;
                }

                if (CountSpecials(board) > specialsBefore)
                {
                    totals.SpecialsCreated++;
                }

                var violations = FindViolations(board);
                if (violations.Count == 0)
                {
                    continue;
                }

                totals.Violations += violations.Count;
                report.AppendLine($"  level {levelNumber}, turn {played} at {cell}:");
                foreach (var violation in violations)
                {
                    report.AppendLine($"    {violation}");
                }
            }

            report.AppendLine(
                $"    played {played} turns, outcome {context.Session.Outcome}, " +
                $"chalices {goals.ChalicesCollected}/{goals.ChaliceGoal}, " +
                $"vases {goals.VasesRemaining}, stones {goals.StonesRemaining}");
        }

        private static int CountSpecials(Board board)
        {
            var count = 0;

            for (var y = 0; y < board.Height; y++)
            {
                for (var x = 0; x < board.Width; x++)
                {
                    if (board.GetItem(new Vector2Int(x, y)) is SpecialItem)
                    {
                        count++;
                    }
                }
            }

            return count;
        }
    }
}

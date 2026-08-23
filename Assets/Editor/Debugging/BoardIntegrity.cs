using System.Collections.Generic;
using System.Text;
using Blast.Gameplay;
using Blast.Motion;
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
        /// Plays a series of blasts on every level, settling each one, and reports any violation.
        /// This is the main regression test for gravity and refill.
        /// </summary>
        [MenuItem("Dream Games/Debug/Run Gravity Stress Test")]
        public static void RunStressTest()
        {
            const int blastsPerLevel = 12;
            var report = new StringBuilder();
            var totalBlasts = 0;
            var totalViolations = 0;

            LevelPreviewTool.RunHeadless((session, board, animator) =>
            {
                for (var levelNumber = 1; levelNumber <= 10; levelNumber++)
                {
                    session.LoadLevel(levelNumber);

                    // Recreated per level, so it has to be re-read after every load.
                    var coordinator = session.Coordinator;

                    // Deterministic per level so a failure can be reproduced.
                    Random.InitState(levelNumber * 7919);

                    var level = session.CurrentLevel;
                    var blasted = 0;

                    for (var attempt = 0; attempt < blastsPerLevel * 20 && blasted < blastsPerLevel; attempt++)
                    {
                        var cell = new Vector2Int(
                            Random.Range(0, level.Width),
                            Random.Range(0, level.Height));

                        var before = session.Moves.Remaining;
                        coordinator.HandleWorldTap(board.CellToWorld(cell));

                        if (session.Moves.Remaining == before)
                        {
                            // Not a blastable cell; costs nothing and changes nothing.
                            continue;
                        }

                        LevelPreviewTool.RunUntilSettled(animator, coordinator);
                        blasted++;
                        totalBlasts++;

                        var violations = FindViolations(board);
                        if (violations.Count == 0)
                        {
                            continue;
                        }

                        totalViolations += violations.Count;
                        report.AppendLine($"  level {levelNumber}, blast {blasted} at {cell}:");
                        foreach (var violation in violations)
                        {
                            report.AppendLine($"    {violation}");
                        }
                    }

                    report.AppendLine($"  level {levelNumber}: {blasted} blasts, {level.Width}x{level.Height}");
                }
            });

            var summary = $"Gravity stress test: {totalBlasts} blasts, {totalViolations} violations.\n{report}";

            if (totalViolations == 0)
            {
                Debug.Log(summary);
            }
            else
            {
                Debug.LogError(summary);
            }
        }
    }
}

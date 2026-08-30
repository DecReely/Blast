using System;
using Blast.Items;
using UnityEngine;

namespace Blast.Gameplay
{
    /// <summary>
    /// Tracks the level's goals: "clear all obstacles within the move count".
    ///
    /// Remaining counts are recomputed by scanning the board after each turn rather than by
    /// decrementing on destruction events. Scanning cannot drift out of step with the board, and at
    /// a hundred cells it costs nothing — whereas a missed event would silently make a level
    /// unwinnable.
    ///
    /// Chalices are the exception: they are collected progressively and a box vanishes once emptied,
    /// so the count has to be accumulated as it is reported.
    /// </summary>
    public sealed class GoalTracker
    {
        private int _chalicesCollected;

        /// <summary>Vases left on the board.</summary>
        public int VasesRemaining { get; private set; }

        /// <summary>Stones left on the board.</summary>
        public int StonesRemaining { get; private set; }

        /// <summary>
        /// Total chalices in the level, being ten per chalice box. Every box must be emptied — the
        /// case study's win condition is clearing all obstacles, and levels 3 and 9 place four boxes
        /// and nothing else, so a flat target would leave three of them purely decorative.
        /// </summary>
        public int ChaliceGoal { get; private set; }

        public int ChalicesCollected => Mathf.Min(_chalicesCollected, ChaliceGoal);

        public int ChalicesRemaining => Mathf.Max(0, ChaliceGoal - _chalicesCollected);

        public bool HasVaseGoal { get; private set; }

        public bool HasStoneGoal { get; private set; }

        public bool HasChaliceGoal => ChaliceGoal > 0;

        /// <summary>Every obstacle cleared.</summary>
        public bool IsComplete =>
            VasesRemaining == 0 && StonesRemaining == 0 && ChalicesRemaining == 0;

        /// <summary>Raised whenever any counter changes, for the goal display to react to.</summary>
        public event Action Changed;

        /// <summary>Reads the starting goals off a freshly built board.</summary>
        public void Initialize(Board board)
        {
            _chalicesCollected = 0;
            ChaliceGoal = 0;

            Count(board, out var vases, out var stones, out var boxes);

            HasVaseGoal = vases > 0;
            HasStoneGoal = stones > 0;

            VasesRemaining = vases;
            StonesRemaining = stones;

            foreach (var box in boxes)
            {
                ChaliceGoal += box.ChaliceCount;
            }

            Changed?.Invoke();
        }

        /// <summary>Recounts what is left. Call once the board has settled.</summary>
        public void Refresh(Board board)
        {
            Count(board, out var vases, out var stones, out _);

            if (vases == VasesRemaining && stones == StonesRemaining)
            {
                return;
            }

            VasesRemaining = vases;
            StonesRemaining = stones;
            Changed?.Invoke();
        }

        /// <summary>Adds chalices taken from a box.</summary>
        public void ReportChalices(int count)
        {
            if (count <= 0)
            {
                return;
            }

            _chalicesCollected += count;
            Changed?.Invoke();
        }

        private static void Count(Board board, out int vases, out int stones, out System.Collections.Generic.List<ChaliceBox> boxes)
        {
            vases = 0;
            stones = 0;
            boxes = new System.Collections.Generic.List<ChaliceBox>();

            for (var y = 0; y < board.Height; y++)
            {
                for (var x = 0; x < board.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    var item = board.GetItem(cell);

                    switch (item)
                    {
                        case Vase:
                            vases++;
                            break;

                        case Stone:
                            stones++;
                            break;

                        // A box covers four cells; count it only at its anchor.
                        case ChaliceBox box when box.Origin == cell:
                            boxes.Add(box);
                            break;
                    }
                }
            }
        }
    }
}

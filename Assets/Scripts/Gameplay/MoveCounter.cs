using System;
using UnityEngine;

namespace Blast.Gameplay
{
    /// <summary>
    /// Tracks the moves remaining in a level.
    ///
    /// A move is only ever spent by a valid tap, so tapping a lone cube or an empty cell costs
    /// nothing. Exposes plain C# events rather than reaching into the UI, so the counter has no
    /// dependency on how it is displayed.
    /// </summary>
    public sealed class MoveCounter
    {
        public MoveCounter(int totalMoves)
        {
            Total = Mathf.Max(0, totalMoves);
            Remaining = Total;
        }

        /// <summary>Moves the level started with.</summary>
        public int Total { get; }

        public int Remaining { get; private set; }

        public bool HasMovesLeft => Remaining > 0;

        /// <summary>Raised with the new remaining count whenever a move is spent.</summary>
        public event Action<int> RemainingChanged;

        /// <summary>Raised once when the last move is used up.</summary>
        public event Action Exhausted;

        public void Spend()
        {
            if (Remaining == 0)
            {
                return;
            }

            Remaining--;
            RemainingChanged?.Invoke(Remaining);

            if (Remaining == 0)
            {
                Exhausted?.Invoke();
            }
        }
    }
}

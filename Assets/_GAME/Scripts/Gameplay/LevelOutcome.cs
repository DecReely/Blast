namespace Blast.Gameplay
{
    /// <summary>How a level attempt has ended, if it has.</summary>
    public enum LevelOutcome
    {
        InProgress,

        /// <summary>Every obstacle cleared within the move count.</summary>
        Won,

        /// <summary>Moves ran out with obstacles still standing.</summary>
        Failed
    }
}

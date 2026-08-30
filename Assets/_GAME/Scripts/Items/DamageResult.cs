namespace Blast.Items
{
    /// <summary>
    /// What one hit did to an item.
    ///
    /// A plain "was it destroyed" answer cannot tell a survivor apart from something that was never
    /// hurt in the first place — stone shrugging off a blast and a vase cracking would both report
    /// "still here". Presentation needs that distinction: only the vase should show a hit.
    /// </summary>
    public enum DamageResult
    {
        /// <summary>The item is immune to this hit and nothing about it changed.</summary>
        Ignored,

        /// <summary>The item took the hit and survived it, so it should react visibly.</summary>
        Damaged,

        /// <summary>The item is finished and must be removed from the board.</summary>
        Destroyed
    }
}

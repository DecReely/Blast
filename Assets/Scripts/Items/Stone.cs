namespace Blast.Items
{
    /// <summary>
    /// Stone. Cleared by a single point of damage, but only a special item explosion can hurt it —
    /// an adjacent cube blast does nothing.
    /// </summary>
    public sealed class Stone : Obstacle
    {
        /// <summary>
        /// Stone never falls, so it also acts as a floor: anything above it stops there instead of
        /// falling past, and the cells beneath it can only be refilled if they are reachable from
        /// another column.
        /// </summary>
        public override bool CanFall => false;
    }
}

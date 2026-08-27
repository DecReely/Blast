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

        /// <summary>
        /// Immune to blasts however many cubes go off beside it; a single explosion clears it.
        /// There is no middle state, so a blast is reported as ignored rather than as damage that
        /// happened to leave it standing.
        /// </summary>
        public override DamageResult ApplyDamage(DamageInfo damage)
        {
            return damage.Source == DamageSource.Explosion
                ? DamageResult.Destroyed
                : DamageResult.Ignored;
        }
    }
}

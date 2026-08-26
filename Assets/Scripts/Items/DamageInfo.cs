namespace Blast.Items
{
    /// <summary>What caused a point of damage. Items react differently to each.</summary>
    public enum DamageSource
    {
        /// <summary>Cubes blasted next to the item. Stone ignores this entirely.</summary>
        Blast,

        /// <summary>A rocket sweep or TNT area. Hurts everything that can be hurt.</summary>
        Explosion
    }

    /// <summary>
    /// One hit delivered to an item.
    ///
    /// Carrying the source rather than a pre-resolved amount is what lets each item own its own rule:
    /// stone ignores blasts, a vase takes at most one hit per blast no matter how many of its
    /// neighbours went off, and the chalice box counts sources or cells depending on its phase.
    ///
    /// Each instance also carries an identity. A multi-cell item is hit once per cell an explosion
    /// covers, so without it the chalice box could not distinguish "one rocket crossing two of my
    /// cells" from "two separate rockets" — a distinction its two phases depend on. Reuse the same
    /// instance for every cell one source touches, and create a new one for a genuinely separate hit.
    /// </summary>
    public readonly struct DamageInfo
    {
        private static int _sourceCounter;

        private DamageInfo(DamageSource source, int amount, int sourceId)
        {
            Source = source;
            Amount = amount;
            SourceId = sourceId;
        }

        public readonly DamageSource Source;

        /// <summary>
        /// Points this hit is worth. One for a single covered cell; a blast passes the number of its
        /// cubes that touch the item, which the chalice box uses and the vase deliberately ignores.
        /// </summary>
        public readonly int Amount;

        /// <summary>Identifies the blast or explosion this hit came from.</summary>
        public readonly int SourceId;

        public static DamageInfo FromBlast(int amount = 1)
        {
            return new DamageInfo(DamageSource.Blast, amount, ++_sourceCounter);
        }

        public static DamageInfo FromExplosion(int amount = 1)
        {
            return new DamageInfo(DamageSource.Explosion, amount, ++_sourceCounter);
        }
    }
}

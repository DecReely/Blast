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
    /// Carrying the source rather than a pre-resolved amount is what lets each item own its own
    /// rule: stone ignores blasts, a vase takes at most one hit per blast no matter how many of its
    /// neighbours went off, and the chalice box counts sources or cells depending on its phase.
    /// </summary>
    public readonly struct DamageInfo
    {
        private DamageInfo(DamageSource source, int amount)
        {
            Source = source;
            Amount = amount;
        }

        public readonly DamageSource Source;

        /// <summary>
        /// Points of damage this hit is worth. Usually one; multi-cell items can receive more when a
        /// single explosion covers several of their cells.
        /// </summary>
        public readonly int Amount;

        public static DamageInfo FromBlast(int amount = 1)
        {
            return new DamageInfo(DamageSource.Blast, amount);
        }

        public static DamageInfo FromExplosion(int amount = 1)
        {
            return new DamageInfo(DamageSource.Explosion, amount);
        }
    }
}

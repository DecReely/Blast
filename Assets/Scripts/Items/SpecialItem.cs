namespace Blast.Items
{
    /// <summary>
    /// Base class for rockets and TNTs.
    ///
    /// Special items are created by blasting a group of four or more cubes, can be triggered by a
    /// tap or by another explosion, and combine into a single combo when tapped while adjacent to
    /// other special items. The explosion patterns themselves live outside the item so that a combo
    /// can replace them wholesale — see the explosion pattern strategies.
    /// </summary>
    public abstract class SpecialItem : GridItem
    {
        /// <summary>Special items obey gravity just like cubes.</summary>
        public override bool CanFall => true;

        /// <summary>
        /// Never reached in practice: the explosion system detects a special in a damaged cell and
        /// detonates it instead of damaging it, which is how a chain reaction propagates. Reported
        /// as "not destroyed" so that a caller which bypassed that path cannot silently delete a
        /// special without triggering it.
        /// </summary>
        public override bool TryTakeDamage(DamageInfo damage)
        {
            return false;
        }
    }
}

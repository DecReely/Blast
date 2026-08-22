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
    }
}

namespace Blast.Items
{
    /// <summary>
    /// Base class for the three obstacle types, which together form a level's goals: a level is won
    /// when every obstacle has been cleared within the move limit.
    ///
    /// The types differ in what can hurt them and in how damage is counted, so each subclass owns
    /// its own damage rule rather than the board special-casing them:
    /// a vase takes at most one damage per blast, stone ignores blasts entirely and only reacts to
    /// special item explosions, and the chalice box counts damage sources in its door phase but
    /// affected cells in its chalice phase.
    /// </summary>
    public abstract class Obstacle : GridItem
    {
    }
}

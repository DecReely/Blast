namespace Blast.Levels
{
    /// <summary>
    /// The item codes used by the level files.
    ///
    /// Only the codes that need <em>special</em> handling during parsing are named here; every other
    /// code is looked up verbatim in the item catalog, so adding a new item type means adding a
    /// catalog row and no changes to this file.
    /// </summary>
    public static class LevelCodes
    {
        /// <summary>Placeholder for "any cube colour", resolved at build time.</summary>
        public const string RandomCube = "rand";

        /// <summary>
        /// The chalice box is the only multi-cell item: it is authored as four corner codes and
        /// collapses into a single 2x2 item anchored at its bottom-left corner.
        /// </summary>
        public const string ChaliceBoxBottomLeft = "cbBL";

        public const string ChaliceBoxBottomRight = "cbBR";
        public const string ChaliceBoxTopLeft = "cbTL";
        public const string ChaliceBoxTopRight = "cbTR";

        /// <summary>
        /// True for the three corner codes that are absorbed by the box placed from its
        /// bottom-left corner, and which therefore must not spawn anything themselves.
        /// </summary>
        public static bool IsAbsorbedChaliceBoxCorner(string code)
        {
            return code is ChaliceBoxBottomRight or ChaliceBoxTopLeft or ChaliceBoxTopRight;
        }
    }
}

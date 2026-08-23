using Blast.Items;

namespace Blast.Gameplay
{
    /// <summary>
    /// The group-size thresholds that drive blasting and special item creation.
    ///
    /// Kept in one place because the same numbers decide three separate things: whether a tap is a
    /// legal move, which special item a blast produces, and which hint the cubes display. If they
    /// were duplicated the hint could advertise a special the blast would not actually create.
    /// </summary>
    public static class SpecialItemRules
    {
        /// <summary>Smallest group that can be blasted at all.</summary>
        public const int MinimumBlastSize = 2;

        /// <summary>From this size a blast produces a rocket.</summary>
        public const int RocketThreshold = 4;

        /// <summary>From this size a blast produces a TNT instead.</summary>
        public const int TntThreshold = 6;

        public static bool IsBlastable(int groupSize)
        {
            return groupSize >= MinimumBlastSize;
        }

        /// <summary>The hint cubes in a group of this size should display.</summary>
        public static Cube.Hint HintFor(int groupSize)
        {
            if (groupSize >= TntThreshold)
            {
                return Cube.Hint.Tnt;
            }

            return groupSize >= RocketThreshold ? Cube.Hint.Rocket : Cube.Hint.None;
        }
    }
}

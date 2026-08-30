using Blast.Items;
using Blast.Levels;
using UnityEngine;

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

        /// <summary>
        /// Whether a group of this size collapses into a special item.
        /// Separate from <see cref="SpecialCodeFor"/> because that picks a rocket orientation at
        /// random, so it must be called exactly once per blast.
        /// </summary>
        public static bool CreatesSpecial(int groupSize)
        {
            return groupSize >= RocketThreshold;
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

        /// <summary>
        /// Item code for the special a blast of this size creates, or null when the group is too
        /// small to earn one. A rocket's orientation is chosen at random, as the case study specifies.
        ///
        /// Shares its thresholds with <see cref="HintFor"/>, so what the cubes advertised is
        /// necessarily what appears.
        /// </summary>
        public static string SpecialCodeFor(int groupSize)
        {
            if (groupSize >= TntThreshold)
            {
                return LevelCodes.Tnt;
            }

            if (groupSize < RocketThreshold)
            {
                return null;
            }

            return Random.value < 0.5f ? LevelCodes.HorizontalRocket : LevelCodes.VerticalRocket;
        }
    }
}

using System.Collections.Generic;
using Blast.Items;

namespace Blast.Gameplay
{
    /// <summary>
    /// Chooses which explosion a merged group of special items produces.
    ///
    /// The three combos are distinguished purely by how many TNTs are involved, so the rules read
    /// straight off the case study. Each resolves to an existing pattern with different parameters
    /// rather than a bespoke class.
    /// </summary>
    public static class ComboRules
    {
        /// <summary>The TNT-TNT combo's 7x7 area.</summary>
        public const int TntTntRadius = 3;

        /// <summary>The Rocket-Rocket combo: one row and one column.</summary>
        public const int RocketRocketThickness = 1;

        /// <summary>The TNT-Rocket combo: three rows and three columns.</summary>
        public const int TntRocketThickness = 3;

        /// <summary>Smallest group that counts as a combo rather than a single detonation.</summary>
        public const int MinimumComboSize = 2;

        public static bool IsCombo(int groupSize)
        {
            return groupSize >= MinimumComboSize;
        }

        /// <summary>
        /// Resolves the combo for a group of adjacent special items.
        ///
        /// Order matters: two or more TNTs always win, then a single TNT paired with any rocket, and
        /// otherwise the group must be rockets only.
        /// </summary>
        public static IExplosionPattern SelectPattern(IReadOnlyList<SpecialItem> group)
        {
            var tntCount = 0;

            foreach (var special in group)
            {
                if (special is Tnt)
                {
                    tntCount++;
                }
            }

            if (tntCount >= 2)
            {
                return new AreaPattern(TntTntRadius);
            }

            return tntCount == 1
                ? new CrossRocketPattern(TntRocketThickness)
                : new CrossRocketPattern(RocketRocketThickness);
        }
    }
}

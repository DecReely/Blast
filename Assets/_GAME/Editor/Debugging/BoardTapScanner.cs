using System.Collections.Generic;
using Blast.Gameplay;
using Blast.Items;
using UnityEngine;

namespace Blast.EditorTools
{
    /// <summary>
    /// Enumerates every tap the board would currently accept.
    ///
    /// Shared by the tools that need to know what a player could do next: the playthrough bot picks
    /// the best option, while the edge case checks only need to know whether any option exists at
    /// all. Both ask the same question, so they ask it in one place.
    ///
    /// The rules are not duplicated here — a tap counts as legal exactly when
    /// <see cref="SpecialItemRules.IsBlastable"/> says so, or when the cell holds a special item,
    /// which is the same test <see cref="BoardCoordinator"/> applies.
    /// </summary>
    internal static class BoardTapScanner
    {
        internal readonly struct TapOption
        {
            public TapOption(
                Vector2Int cell, int groupSize, bool isSpecial, bool isCombo, int obstacleValue, int tntCount)
            {
                Cell = cell;
                GroupSize = groupSize;
                IsSpecial = isSpecial;
                IsCombo = isCombo;
                ObstacleValue = obstacleValue;
                TntCount = tntCount;
            }

            public Vector2Int Cell { get; }

            /// <summary>Cubes in the group, or the number of merged specials for a combo.</summary>
            public int GroupSize { get; }

            public bool IsSpecial { get; }

            /// <summary>The tapped special touches at least one other, so it would merge.</summary>
            public bool IsCombo { get; }

            /// <summary>
            /// How much progress towards the goals this tap is worth: estimated blast damage for a
            /// cube group, or obstacle cells in range for a special.
            /// </summary>
            public int ObstacleValue { get; }

            /// <summary>TNTs among the specials that would merge, which decides the combo's shape.</summary>
            public int TntCount { get; }
        }

        /// <summary>A lone TNT's blast radius, used to judge whether it is worth detonating yet.</summary>
        private const int TntRadius = 2;

        /// <summary>
        /// Collects one option per distinct group, rather than one per cell, so a group of eight does
        /// not drown out every other choice in the list.
        /// </summary>
        public static void Collect(
            Board board,
            GroupFinder groupFinder,
            ComboDetector comboDetector,
            List<TapOption> into)
        {
            into.Clear();

            var visited = new HashSet<Vector2Int>();
            var group = new List<Vector2Int>();
            var specialGroup = new List<SpecialItem>();

            for (var y = 0; y < board.Height; y++)
            {
                for (var x = 0; x < board.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!visited.Add(cell))
                    {
                        continue;
                    }

                    var item = board.GetItem(cell);

                    if (item is SpecialItem special)
                    {
                        var merged = comboDetector.FindGroup(board, cell, specialGroup);

                        into.Add(new TapOption(
                            cell,
                            merged,
                            isSpecial: true,
                            isCombo: ComboRules.IsCombo(merged),
                            obstacleValue: EstimateExplosionValue(board, cell, special),
                            tntCount: CountTnts(specialGroup)));

                        continue;
                    }

                    if (item is not Cube)
                    {
                        continue;
                    }

                    var size = groupFinder.FindGroupOfCubes(board, cell, group);

                    // Mark the whole group so its other members are not offered again.
                    foreach (var member in group)
                    {
                        visited.Add(member);
                    }

                    if (!SpecialItemRules.IsBlastable(size))
                    {
                        continue;
                    }

                    into.Add(new TapOption(
                        cell,
                        size,
                        isSpecial: false,
                        isCombo: false,
                        obstacleValue: EstimateBlastDamage(board, group),
                        tntCount: 0));
                }
            }
        }

        private static int CountTnts(List<SpecialItem> specials)
        {
            var tnts = 0;

            foreach (var special in specials)
            {
                if (special is Tnt)
                {
                    tnts++;
                }
            }

            return tnts;
        }

        /// <summary>Whether the player has anything at all to tap. A settled board must never say no.</summary>
        public static bool HasLegalTap(Board board, GroupFinder groupFinder, ComboDetector comboDetector)
        {
            var options = new List<TapOption>();
            Collect(board, groupFinder, comboDetector, options);
            return options.Count > 0;
        }

        /// <summary>
        /// What a blast on this group would actually be worth against the obstacles it touches.
        ///
        /// Counting distinct obstacles is not good enough, because the three types respond to a blast
        /// in completely different ways: a vase takes one damage however many cubes touched it, a
        /// stone takes none at all, and a chalice box collects one chalice per adjacent cube. Only the
        /// last of those rewards a large group hugging one obstacle, and it is exactly the play the
        /// chalice levels are built around.
        /// </summary>
        private static int EstimateBlastDamage(Board board, List<Vector2Int> group)
        {
            var contacts = new Dictionary<GridItem, int>();

            foreach (var cell in group)
            {
                foreach (var offset in Board.Neighbours)
                {
                    if (board.GetItem(cell + offset) is not Obstacle obstacle)
                    {
                        continue;
                    }

                    contacts.TryGetValue(obstacle, out var touching);
                    contacts[obstacle] = touching + 1;
                }
            }

            var damage = 0;

            foreach (var (obstacle, touching) in contacts)
            {
                damage += obstacle switch
                {
                    ChaliceBox => touching,
                    Vase => 1,
                    _ => 0
                };
            }

            return damage;
        }

        /// <summary>
        /// Obstacle cells the special would actually cover if detonated where it stands.
        ///
        /// Modelled on the real explosion shapes rather than a blanket radius, because the two differ
        /// enormously in practice: a rocket lying along a row of stone is worth firing immediately,
        /// while the same rocket a cell away from it is worth nothing. Cells are counted rather than
        /// obstacles, since an explosion damages a chalice box once per cell it covers.
        /// </summary>
        private static int EstimateExplosionValue(Board board, Vector2Int centre, SpecialItem special)
        {
            return special switch
            {
                Tnt => CountObstacleCellsInArea(board, centre, TntRadius),
                Rocket rocket => CountObstacleCellsInLine(board, centre, rocket.Orientation),
                _ => 0
            };
        }

        private static int CountObstacleCellsInArea(Board board, Vector2Int centre, int radius)
        {
            var cells = 0;

            for (var dy = -radius; dy <= radius; dy++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    if (board.GetItem(centre + new Vector2Int(dx, dy)) is Obstacle)
                    {
                        cells++;
                    }
                }
            }

            return cells;
        }

        private static int CountObstacleCellsInLine(Board board, Vector2Int centre, Rocket.Axis axis)
        {
            var cells = 0;
            var length = axis == Rocket.Axis.Horizontal ? board.Width : board.Height;

            for (var i = 0; i < length; i++)
            {
                var cell = axis == Rocket.Axis.Horizontal
                    ? new Vector2Int(i, centre.y)
                    : new Vector2Int(centre.x, i);

                if (board.GetItem(cell) is Obstacle)
                {
                    cells++;
                }
            }

            return cells;
        }
    }
}

using Blast.Gameplay;
using Blast.Items;
using Blast.Levels;
using NUnit.Framework;

namespace Blast.Tests
{
    /// <summary>
    /// Rules that are pure functions, tested without a scene, a board or a prefab.
    ///
    /// These run in milliseconds and pin down the arithmetic the rest of the game is built on, so a
    /// threshold or layout regression is reported precisely instead of surfacing as a puzzling
    /// failure somewhere in the integration suites.
    /// </summary>
    [TestFixture]
    public sealed class GameRuleTests
    {
        [TestCase(0, false)]
        [TestCase(1, false)]
        [TestCase(2, true)]
        [TestCase(9, true)]
        public void OnlyGroupsOfTwoOrMoreAreBlastable(int groupSize, bool expected)
        {
            Assert.That(SpecialItemRules.IsBlastable(groupSize), Is.EqualTo(expected));
        }

        [TestCase(3, Cube.Hint.None)]
        [TestCase(4, Cube.Hint.Rocket)]
        [TestCase(5, Cube.Hint.Rocket)]
        [TestCase(6, Cube.Hint.Tnt)]
        [TestCase(12, Cube.Hint.Tnt)]
        public void HintMatchesTheThresholds(int groupSize, Cube.Hint expected)
        {
            Assert.That(SpecialItemRules.HintFor(groupSize), Is.EqualTo(expected));
        }

        /// <summary>
        /// The hint is a promise about what the blast will produce, so the two must never disagree —
        /// a cube advertising a TNT and delivering a rocket is the exact bug this rules out.
        /// </summary>
        [Test]
        public void WhatTheCubesAdvertiseIsWhatTheBlastCreates()
        {
            for (var groupSize = 0; groupSize <= 20; groupSize++)
            {
                var hint = SpecialItemRules.HintFor(groupSize);
                var code = SpecialItemRules.SpecialCodeFor(groupSize);

                switch (hint)
                {
                    case Cube.Hint.None:
                        Assert.That(code, Is.Null, $"group of {groupSize} advertised nothing");
                        Assert.That(SpecialItemRules.CreatesSpecial(groupSize), Is.False);
                        break;

                    case Cube.Hint.Rocket:
                        Assert.That(
                            code,
                            Is.EqualTo(LevelCodes.HorizontalRocket).Or.EqualTo(LevelCodes.VerticalRocket),
                            $"group of {groupSize} advertised a rocket");
                        break;

                    case Cube.Hint.Tnt:
                        Assert.That(code, Is.EqualTo(LevelCodes.Tnt), $"group of {groupSize} advertised a TNT");
                        break;
                }
            }
        }

        /// <summary>
        /// The two shelves stay as even as they can, with the remainder on the bottom: ten reads as
        /// 5 and 5, nine as 4 above and 5 below rather than 3 and 6.
        /// </summary>
        [TestCase(10, 5, 5)]
        [TestCase(9, 4, 5)]
        [TestCase(8, 4, 4)]
        [TestCase(7, 3, 4)]
        [TestCase(1, 0, 1)]
        [TestCase(0, 0, 0)]
        public void ChalicesAreSharedEvenlyBetweenTheShelves(int total, int expectedTop, int expectedBottom)
        {
            ChaliceShelfView.SplitRows(total, out var top, out var bottom);

            Assert.That(top, Is.EqualTo(expectedTop), "top shelf");
            Assert.That(bottom, Is.EqualTo(expectedBottom), "bottom shelf");
            Assert.That(top + bottom, Is.EqualTo(total), "every chalice is shown once");
            Assert.That(bottom - top, Is.InRange(0, 1), "the shelves never differ by more than one");
        }
    }
}

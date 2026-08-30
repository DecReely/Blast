using Blast.EditorTools;
using NUnit.Framework;

namespace Blast.Tests
{
    /// <summary>
    /// The scene-driven suites, run as tests.
    ///
    /// These are integration tests: each one opens <c>LevelScene</c>, plants a board and plays real
    /// turns through the real coordinator, stepping the gameplay systems at a fixed 1/60 by hand
    /// because edit mode has no update loop. Driving the actual session is the point — a check that
    /// reached past the coordinator would prove something about a test harness rather than about
    /// the game.
    ///
    /// The suites double as the Dream Games ▸ Debug menu items, which log the same reports for when
    /// a failure needs reading rather than just detecting.
    /// </summary>
    [TestFixture]
    public sealed class GameplayVerificationTests
    {
        [Test]
        [Description("A settled board never floats an item or leaves a reachable cell unfilled, " +
                     "across long random play on every level.")]
        public void BoardStaysConsistentUnderRandomPlay()
        {
            VerificationAssert.Passed(BoardIntegrity.RunChecks());
        }

        [Test]
        [Description("Each combo clears exactly the footprint the case study describes.")]
        public void CombosClearTheirDocumentedFootprint()
        {
            VerificationAssert.Passed(ComboVerificationTool.RunChecks());
        }

        [Test]
        public void VaseFollowsItsDamageRule()
        {
            VerificationAssert.Passed(ObstacleVerificationTool.CheckVaseRules());
        }

        [Test]
        public void StoneFollowsItsDamageRule()
        {
            VerificationAssert.Passed(ObstacleVerificationTool.CheckStoneRules());
        }

        [Test]
        public void ClearingTheLastObstacleWinsTheLevel()
        {
            VerificationAssert.Passed(ObstacleVerificationTool.CheckWinCondition());
        }

        [Test]
        [Description("Doors count damage sources; chalices count covered cells and adjacent cubes.")]
        public void ChaliceBoxCountsBothPhasesCorrectly()
        {
            VerificationAssert.Passed(ChaliceBoxVerificationTool.CheckDamagePhases());
        }

        [Test]
        [Description("A real explosion shares one damage source across every cell it covers.")]
        public void ChaliceBoxSeesOneExplosionAsOneSource()
        {
            VerificationAssert.Passed(ChaliceBoxVerificationTool.CheckEndToEnd());
        }

        [Test]
        [Description("Input gating, taps that are not moves, the win/lose boundary on the final " +
                     "move, multi-box goals, and that a settled board always offers a legal tap.")]
        public void TurnFlowEdgeCasesHold()
        {
            VerificationAssert.Passed(EdgeCaseVerificationTool.RunChecks());
        }

        /// <summary>
        /// Explicit because it plays fifty full levels and takes far longer than the rest of the
        /// suite put together. It is a playability observation rather than a regression guard, so it
        /// is opt-in from the Test Runner or the menu item.
        /// </summary>
        [Test]
        [Explicit("Long running: plays every level five times with a heuristic bot.")]
        public void EveryLevelReachesAnOutcome()
        {
            VerificationAssert.Passed(LevelPlaythroughTool.RunChecks());
        }
    }
}

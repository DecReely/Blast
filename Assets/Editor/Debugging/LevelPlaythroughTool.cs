using System.Collections.Generic;
using System.Text;
using Blast.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Blast.EditorTools
{
    /// <summary>
    /// Plays every level from its opening board to a win or a loss, choosing moves with a simple
    /// heuristic.
    ///
    /// The stress test proves the board never corrupts itself; this proves the levels are actually
    /// playable — that a reasonable player can clear the obstacles inside the move budget, and that
    /// the session reports the right outcome when they do. It is the automated stand-in for sitting
    /// down and playing all ten.
    ///
    /// The bot is deliberately simple. It is not trying to play optimally, only well enough that a
    /// level failing to complete is worth a second look rather than being noise.
    /// </summary>
    public static class LevelPlaythroughTool
    {
        private const int LevelCount = 10;

        /// <summary>
        /// Each level is attempted several times with different seeds. Refill is random, so a single
        /// attempt says as much about luck as about the level.
        /// </summary>
        private const int AttemptsPerLevel = 5;

        [MenuItem("Dream Games/Debug/Play All Levels")]
        public static void PlayAll()
        {
            var report = new StringBuilder();
            var levelsEverWon = 0;
            var totalWins = 0;
            var totalAttempts = 0;
            var stuck = 0;

            LevelPreviewTool.RunHeadless(context =>
            {
                for (var levelNumber = 1; levelNumber <= LevelCount; levelNumber++)
                {
                    var wins = 0;
                    var bestMovesLeft = -1;
                    var closest = int.MaxValue;

                    for (var attempt = 0; attempt < AttemptsPerLevel; attempt++)
                    {
                        // Deterministic per attempt, so any failure can be reproduced exactly.
                        Random.InitState((levelNumber * 7919) + (attempt * 104729));

                        var result = PlayOnce(context, levelNumber);
                        totalAttempts++;

                        if (!result.Finished)
                        {
                            stuck++;
                            report.AppendLine(
                                $"    level {levelNumber} attempt {attempt}: ended still in progress");
                        }

                        if (result.Won)
                        {
                            wins++;
                            bestMovesLeft = Mathf.Max(bestMovesLeft, result.MovesLeft);
                        }
                        else
                        {
                            closest = Mathf.Min(closest, result.ObstaclesLeft);
                        }
                    }

                    totalWins += wins;

                    if (wins > 0)
                    {
                        levelsEverWon++;
                    }

                    report.AppendLine(
                        $"  level {levelNumber:00}: won {wins}/{AttemptsPerLevel}" +
                        (wins > 0
                            ? $", best finish with {bestMovesLeft} move(s) to spare"
                            : $", closest attempt left {closest} obstacle(s)"));
                }
            });

            var summary =
                $"Level playthrough: {levelsEverWon}/{LevelCount} levels completed by the bot, " +
                $"{totalWins}/{totalAttempts} attempts won.\n{report}";

            // A level the bot cannot finish is a difficulty observation, not a defect — it plays
            // greedily and never looks ahead. A level that ends with no outcome at all is a defect,
            // because it means the player would be left with a board that can neither be won nor lost.
            if (stuck == 0)
            {
                Debug.Log(summary);
            }
            else
            {
                Debug.LogError($"{summary}\n{stuck} attempt(s) ended without an outcome.");
            }
        }

        private readonly struct Result
        {
            public Result(bool won, bool finished, int movesLeft, int obstaclesLeft)
            {
                Won = won;
                Finished = finished;
                MovesLeft = movesLeft;
                ObstaclesLeft = obstaclesLeft;
            }

            public bool Won { get; }

            /// <summary>The level reached a definite outcome rather than running out of options.</summary>
            public bool Finished { get; }

            public int MovesLeft { get; }

            public int ObstaclesLeft { get; }
        }

        private static Result PlayOnce(LevelPreviewTool.Context context, int levelNumber)
        {
            context.Session.LoadLevel(levelNumber);

            var session = context.Session;
            var groupFinder = new GroupFinder();
            var comboDetector = new ComboDetector();
            var options = new List<BoardTapScanner.TapOption>();

            // Generous ceiling: the move counter ends the level long before this, so it only exists
            // to stop a bug from hanging the editor.
            var safety = session.Moves.Total * 4;

            while (session.Outcome == LevelOutcome.InProgress && safety-- > 0)
            {
                BoardTapScanner.Collect(context.Board, groupFinder, comboDetector, options);
                if (options.Count == 0)
                {
                    break;
                }

                var choice = SelectBest(options);
                session.Coordinator.HandleWorldTap(context.Board.CellToWorld(choice.Cell));
                context.Ticker.RunUntilIdle(session.Coordinator);
            }

            var goals = session.Goals;
            var obstaclesLeft = goals.VasesRemaining + goals.StonesRemaining + goals.ChalicesRemaining;

            return new Result(
                won: session.Outcome == LevelOutcome.Won,
                finished: session.Outcome != LevelOutcome.InProgress,
                movesLeft: session.Moves.Remaining,
                obstaclesLeft: obstaclesLeft);
        }

        /// <summary>
        /// Scores every available tap and takes the best.
        ///
        /// The weights encode what actually clears a level: damage delivered now dominates, a special
        /// is worth detonating in proportion to what its own explosion shape would cover, and among
        /// plain blasts the ones that either damage an obstacle or build a special come first. Stones
        /// can only be broken by an explosion, so building specials has to outrank clearing cubes.
        /// </summary>
        private static BoardTapScanner.TapOption SelectBest(List<BoardTapScanner.TapOption> options)
        {
            var best = options[0];
            var bestScore = int.MinValue;

            foreach (var option in options)
            {
                var score = Score(option);
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                best = option;
            }

            return best;
        }

        private static int Score(BoardTapScanner.TapOption option)
        {
            if (option.IsCombo)
            {
                return ScoreCombo(option);
            }

            if (option.IsSpecial)
            {
                // A special sitting in a barren corner is worth saving; one next to obstacles is not.
                return 150 + (option.ObstacleValue * 250);
            }

            // Damage delivered now is what actually finishes a level, so it dominates.
            var score = (option.ObstacleValue * 200) + (option.GroupSize * 4);

            // Building a special is an investment that pays for the obstacles a blast cannot touch,
            // but only worth making when this blast is not already doing real damage.
            score += option.GroupSize >= SpecialItemRules.TntThreshold ? 500
                : SpecialItemRules.CreatesSpecial(option.GroupSize) ? 250
                : 0;

            return score;
        }

        /// <summary>
        /// A combo is worth what its shape covers, and that shape depends only on how many TNTs are
        /// in the group — never on how many specials merged.
        ///
        /// That makes a long run of rockets a trap: merging ten produces the same plus shape as
        /// merging two, so the other eight are spent for nothing. Level 10 is built around exactly
        /// that, opening with a row of ten rockets meant to be set off as a chain reaction by a
        /// separate explosion rather than tapped directly. The score therefore falls away as a
        /// rockets-only group grows, until a large one is worth actively avoiding.
        /// </summary>
        private static int ScoreCombo(BoardTapScanner.TapOption option)
        {
            // 7x7, the largest explosion in the game.
            if (option.TntCount >= 2)
            {
                return 2600 + (option.ObstacleValue * 50);
            }

            // Rockets on three rows and three columns.
            if (option.TntCount == 1)
            {
                return 2400 + (option.ObstacleValue * 50);
            }

            const int rocketsSpentUsefully = 2;
            return 1200 - ((option.GroupSize - rocketsSpentUsefully) * 250);
        }
    }
}

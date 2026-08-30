using Blast.Gameplay;
using Blast.Motion;
using UnityEngine;

namespace Blast.EditorTools
{
    /// <summary>
    /// Drives every time-based gameplay system by hand.
    ///
    /// Editor tooling gets no <c>Update</c> loop, so a turn would never finish on its own. Each of
    /// these systems deliberately exposes a public <c>Tick</c> taking a delta, which is what makes
    /// them runnable outside play mode and, incidentally, testable.
    /// </summary>
    internal sealed class SceneTicker
    {
        /// <summary>Fixed step, so a run is deterministic and reproducible.</summary>
        public const float FrameStep = 1f / 60f;

        private readonly BoardActionRunner _actionRunner;
        private readonly MergeAnimator _mergeAnimator;
        private readonly FallAnimator _fallAnimator;

        private SceneTicker(BoardActionRunner actionRunner, MergeAnimator mergeAnimator, FallAnimator fallAnimator)
        {
            _actionRunner = actionRunner;
            _mergeAnimator = mergeAnimator;
            _fallAnimator = fallAnimator;
        }

        /// <summary>Collects the ticked systems from the open scene, or null if any is missing.</summary>
        public static SceneTicker FromOpenScene()
        {
            var actionRunner = Object.FindFirstObjectByType<BoardActionRunner>();
            var mergeAnimator = Object.FindFirstObjectByType<MergeAnimator>();
            var fallAnimator = Object.FindFirstObjectByType<FallAnimator>();

            if (actionRunner == null || mergeAnimator == null || fallAnimator == null)
            {
                Debug.LogError("[SceneTicker] LevelScene is missing one of the ticked gameplay systems.");
                return null;
            }

            return new SceneTicker(actionRunner, mergeAnimator, fallAnimator);
        }

        /// <summary>
        /// Advances one frame. Explosions first, then merges, then falls, matching the order a turn
        /// naturally progresses through.
        /// </summary>
        public void Step()
        {
            _actionRunner.Tick(FrameStep);
            _mergeAnimator.Tick(FrameStep);
            _fallAnimator.Tick(FrameStep);
        }

        public void Advance(int frames)
        {
            for (var i = 0; i < frames; i++)
            {
                Step();
            }
        }

        /// <summary>
        /// Runs a turn to completion. Capped so a stuck turn reports an error instead of hanging the
        /// editor.
        /// </summary>
        public int RunUntilIdle(BoardCoordinator coordinator)
        {
            const int maxFrames = 1200;
            var frames = 0;

            while (coordinator.IsResolving && frames < maxFrames)
            {
                Step();
                frames++;
            }

            if (coordinator.IsResolving)
            {
                Debug.LogError($"[SceneTicker] Turn never finished after {maxFrames} frames.");
            }

            return frames;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Blast.Motion
{
    /// <summary>
    /// Draws a set of loose visuals together onto a single point, shrinking as they go.
    ///
    /// Used when a blast of four or more cubes collapses into a special item: the case study asks for
    /// the cubes to "animate to the clicked cell" before the special appears. The same motion serves
    /// the special item combo, where several specials converge on the tapped cell.
    ///
    /// It only moves transforms — the board has already been updated and these are detached visuals,
    /// so nothing here can affect gameplay state.
    /// </summary>
    public sealed class MergeAnimator : MonoBehaviour
    {
        private struct Mover
        {
            public Transform Transform;
            public Vector3 From;
            public Vector3 FromScale;
        }

        [SerializeField] private float _duration = 0.2f;

        [Tooltip("Scale the visuals shrink to as they arrive.")]
        [SerializeField] private float _endScale = 0.3f;

        [Tooltip("Eased so the visuals accelerate inwards rather than drifting at a constant speed.")]
        [SerializeField]
        private AnimationCurve _ease = new(
            new Keyframe(0f, 0f),
            new Keyframe(1f, 1f));

        private readonly List<Mover> _movers = new();
        private Vector3 _target;
        private float _elapsed;
        private Action _onComplete;

        public bool IsAnimating => _movers.Count > 0;

        /// <summary>
        /// Starts the convergence. Invokes <paramref name="onComplete"/> immediately when there is
        /// nothing to move, so callers need no special case for an empty set.
        /// </summary>
        public void Play(IReadOnlyList<Transform> visuals, Vector3 target, Action onComplete)
        {
            _movers.Clear();
            _target = target;
            _elapsed = 0f;

            foreach (var visual in visuals)
            {
                if (visual == null)
                {
                    continue;
                }

                _movers.Add(new Mover
                {
                    Transform = visual,
                    From = visual.position,
                    FromScale = visual.localScale
                });
            }

            if (_movers.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            _onComplete = onComplete;
        }

        /// <summary>
        /// Advances the animation. Public and time-step driven so editor tooling can run it to
        /// completion without entering play mode.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_movers.Count == 0)
            {
                return;
            }

            _elapsed += deltaTime;
            var progress = _duration > 0f ? Mathf.Clamp01(_elapsed / _duration) : 1f;
            var eased = _ease.Evaluate(progress);

            foreach (var mover in _movers)
            {
                if (mover.Transform == null)
                {
                    continue;
                }

                mover.Transform.position = Vector3.LerpUnclamped(mover.From, _target, eased);
                mover.Transform.localScale = Vector3.LerpUnclamped(
                    mover.FromScale, mover.FromScale * _endScale, eased);
            }

            if (progress < 1f)
            {
                return;
            }

            _movers.Clear();

            // Cleared before invoking so a callback that starts another merge is safe.
            var callback = _onComplete;
            _onComplete = null;
            callback?.Invoke();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }
    }
}

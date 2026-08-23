using System;
using System.Collections.Generic;
using Blast.Gameplay;
using Blast.Items;
using UnityEngine;

namespace Blast.Motion
{
    /// <summary>
    /// Animates items into the cells gravity has already assigned them.
    ///
    /// The motion is integrated by hand — constant downward acceleration up to a terminal speed,
    /// then a short squash on landing — rather than driven by Physics or an animation clip, as the
    /// case study requires. Because acceleration is integrated rather than interpolated over a fixed
    /// duration, an item falling six rows genuinely moves faster than one falling a single row, and
    /// a column of falling cubes stays evenly spaced instead of stretching apart.
    /// </summary>
    public sealed class FallAnimator : MonoBehaviour
    {
        private enum Phase
        {
            Falling,
            Landing
        }

        private struct Mover
        {
            public GridItem Item;
            public float TargetY;
            public float Velocity;
            public Phase Phase;
            public float LandElapsed;
        }

        [Header("Fall")]
        [Tooltip("Downward acceleration in cells per second squared.")]
        [SerializeField] private float _gravity = 60f;

        [Tooltip("Terminal speed in cells per second, so long drops stay readable.")]
        [SerializeField] private float _maxFallSpeed = 26f;

        [Header("Landing")]
        [SerializeField] private float _landDuration = 0.16f;

        [Tooltip("Peak vertical squash, as a fraction of the item's height.")]
        [SerializeField] private float _landSquash = 0.17f;

        [Tooltip("How much the item widens relative to how much it squashes.")]
        [SerializeField] private float _landStretch = 0.6f;

        [Tooltip("Half the item's visual height, used to keep its base planted while it squashes.")]
        [SerializeField] private float _visualHalfHeight = 0.53f;

        [Tooltip("Squash amount over the landing, rising sharply then easing back to none.")]
        [SerializeField]
        private AnimationCurve _landCurve = new(
            new Keyframe(0f, 0f),
            new Keyframe(0.35f, 1f),
            new Keyframe(1f, 0f));

        private readonly List<Mover> _movers = new();
        private Action _onComplete;

        public bool IsAnimating => _movers.Count > 0;

        /// <summary>
        /// Starts animating a batch of falls and invokes <paramref name="onComplete"/> once every one
        /// has landed. Invokes it immediately when there is nothing to animate, so callers can treat
        /// "settled instantly" and "settled after a fall" identically.
        /// </summary>
        public void Play(List<FallRequest> requests, Action onComplete)
        {
            foreach (var request in requests)
            {
                if (request.Item == null)
                {
                    continue;
                }

                request.Item.transform.position = request.From;

                _movers.Add(new Mover
                {
                    Item = request.Item,
                    TargetY = request.To.y,
                    Velocity = 0f,
                    Phase = Phase.Falling,
                    LandElapsed = 0f
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
        /// Advances the animation. Public and time-step driven so editor tooling can run a settle to
        /// completion without entering play mode.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_movers.Count == 0)
            {
                return;
            }

            for (var i = _movers.Count - 1; i >= 0; i--)
            {
                var mover = _movers[i];

                if (mover.Item == null)
                {
                    _movers.RemoveAt(i);
                    continue;
                }

                var finished = mover.Phase == Phase.Falling
                    ? TickFall(ref mover, deltaTime)
                    : TickLanding(ref mover, deltaTime);

                if (finished)
                {
                    _movers.RemoveAt(i);
                }
                else
                {
                    _movers[i] = mover;
                }
            }

            if (_movers.Count == 0)
            {
                // Cleared before invoking so a callback that starts another settle is safe.
                var callback = _onComplete;
                _onComplete = null;
                callback?.Invoke();
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private bool TickFall(ref Mover mover, float deltaTime)
        {
            mover.Velocity = Mathf.Min(mover.Velocity + (_gravity * deltaTime), _maxFallSpeed);

            var position = mover.Item.transform.position;
            position.y -= mover.Velocity * deltaTime;

            if (position.y <= mover.TargetY)
            {
                position.y = mover.TargetY;
                mover.Phase = Phase.Landing;
                mover.LandElapsed = 0f;
            }

            mover.Item.transform.position = position;
            return false;
        }

        private bool TickLanding(ref Mover mover, float deltaTime)
        {
            mover.LandElapsed += deltaTime;
            var progress = _landDuration > 0f ? Mathf.Clamp01(mover.LandElapsed / _landDuration) : 1f;

            if (progress >= 1f)
            {
                mover.Item.transform.localScale = Vector3.one;
                mover.Item.transform.position = WithY(mover.Item.transform.position, mover.TargetY);
                return true;
            }

            ApplySquash(mover.Item, mover.TargetY, _landCurve.Evaluate(progress) * _landSquash);
            return false;
        }

        /// <summary>
        /// Squashes vertically and widens horizontally. Sprites are pivoted at their centre, so the
        /// item is also nudged down by half the height it loses; otherwise its base would visibly
        /// lift off the cell as it compresses.
        /// </summary>
        private void ApplySquash(GridItem item, float targetY, float amount)
        {
            item.transform.localScale = new Vector3(1f + (amount * _landStretch), 1f - amount, 1f);
            item.transform.position = WithY(item.transform.position, targetY - (amount * _visualHalfHeight));
        }

        private static Vector3 WithY(Vector3 position, float y)
        {
            position.y = y;
            return position;
        }
    }
}

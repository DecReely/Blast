using System;
using System.Collections.Generic;
using UnityEngine;

namespace Blast.Gameplay
{
    /// <summary>
    /// Ticks the board's in-flight actions and reports when they have all finished.
    ///
    /// Actions may add more actions while running — a rocket sweeping into a TNT detonates it, which
    /// spawns further sweeps — so "done" means the list has drained, not that the original batch
    /// completed. The turn ends only then.
    /// </summary>
    public sealed class BoardActionRunner : MonoBehaviour
    {
        private readonly List<IBoardAction> _actions = new();
        private Action _onIdle;

        public bool IsRunning => _actions.Count > 0;

        public void Add(IBoardAction action)
        {
            if (action != null)
            {
                _actions.Add(action);
            }
        }

        /// <summary>
        /// Invokes <paramref name="callback"/> once nothing is running, immediately if the board is
        /// already idle. Mirrors the fall animator so the coordinator can treat both the same way.
        /// </summary>
        public void NotifyWhenIdle(Action callback)
        {
            if (_actions.Count == 0)
            {
                callback?.Invoke();
                return;
            }

            _onIdle = callback;
        }

        /// <summary>
        /// Advances every action. Public and time-step driven so editor tooling can run a chain
        /// reaction to completion without entering play mode.
        /// </summary>
        public void Tick(float deltaTime)
        {
            // Backwards so finished actions can be removed in place. Actions added during the loop
            // land past the current index and are picked up on the next tick.
            for (var i = _actions.Count - 1; i >= 0; i--)
            {
                if (!_actions[i].Tick(deltaTime))
                {
                    _actions.RemoveAt(i);
                }
            }

            if (_actions.Count != 0 || _onIdle == null)
            {
                return;
            }

            // Cleared before invoking so a callback that queues more work is safe.
            var callback = _onIdle;
            _onIdle = null;
            callback.Invoke();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }
    }
}

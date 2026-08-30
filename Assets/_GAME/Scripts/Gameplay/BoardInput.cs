using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Blast.Gameplay
{
    /// <summary>
    /// Turns pointer presses into world positions.
    ///
    /// Deliberately knows nothing about the board or about cells: it reports where the player
    /// touched and lets the board — the single source of truth for the grid — decide which cell
    /// that is.
    ///
    /// The bindings come from the shared input asset rather than being declared in code, so which
    /// devices count as a tap is data an input asset can re-author without touching this class.
    /// </summary>
    public sealed class BoardInput : MonoBehaviour
    {
        [SerializeField] private Camera _camera;

        [Header("Actions")]
        [Tooltip("Button action for a tap. Bound to mouse, pen and touch in the shared input asset.")]
        [SerializeField] private InputActionReference _pressAction;

        [Tooltip("Vector2 action carrying the pointer's screen position.")]
        [SerializeField] private InputActionReference _pointerPosition;

        /// <summary>Raised on press with the tapped point on the board plane (z = 0).</summary>
        public event Action<Vector3> WorldTapped;

        /// <summary>
        /// Gate used to lock the player out while the board is resolving a move, so a second tap
        /// cannot interleave with an in-progress blast.
        /// </summary>
        public bool AcceptsInput { get; set; } = true;

        private InputAction PressAction => _pressAction != null ? _pressAction.action : null;

        private InputAction PointerPosition => _pointerPosition != null ? _pointerPosition.action : null;

        private void Awake()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_pressAction == null || _pointerPosition == null)
            {
                Debug.LogError("[BoardInput] Both input action references must be assigned.", this);
            }
        }

        private void OnEnable()
        {
            PressAction?.Enable();
            PointerPosition?.Enable();
        }

        private void OnDisable()
        {
            PressAction?.Disable();
            PointerPosition?.Disable();
        }

        /// <summary>
        /// Polled rather than driven by the action's performed callback.
        ///
        /// The UI check below is the reason: Unity does not support calling
        /// <see cref="EventSystem.IsPointerOverGameObject"/> from inside an input callback, because
        /// UI state has not settled at that point in the input update. Subscribing would mean
        /// stashing the press and validating it here anyway, which is polling with extra steps.
        /// </summary>
        private void Update()
        {
            var press = PressAction;
            var position = PointerPosition;

            if (!AcceptsInput || _camera == null || press == null || position == null)
            {
                return;
            }

            // Responding on press rather than release keeps the blast feeling immediate.
            if (!press.WasPressedThisFrame() || IsPointerOverUi())
            {
                return;
            }

            var world = _camera.ScreenToWorldPoint(position.ReadValue<Vector2>());
            world.z = 0f;

            WorldTapped?.Invoke(world);
        }

        /// <summary>Stops taps that land on the fail popup or other UI from reaching the board.</summary>
        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}

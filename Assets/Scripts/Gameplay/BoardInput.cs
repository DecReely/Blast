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
    /// that is. The bindings are declared inline rather than through an input asset so the component
    /// is self-contained and cannot break if the shared asset is re-authored.
    /// </summary>
    public sealed class BoardInput : MonoBehaviour
    {
        [SerializeField] private Camera _camera;

        [SerializeField]
        private InputAction _pressAction = new(
            name: "BoardPress",
            type: InputActionType.Button,
            binding: "<Pointer>/press");

        [SerializeField]
        private InputAction _pointerPosition = new(
            name: "BoardPointerPosition",
            type: InputActionType.Value,
            binding: "<Pointer>/position");

        /// <summary>Raised on press with the tapped point on the board plane (z = 0).</summary>
        public event Action<Vector3> WorldTapped;

        /// <summary>
        /// Gate used to lock the player out while the board is resolving a move, so a second tap
        /// cannot interleave with an in-progress blast.
        /// </summary>
        public bool AcceptsInput { get; set; } = true;

        private void Awake()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }
        }

        private void OnEnable()
        {
            _pressAction.Enable();
            _pointerPosition.Enable();
        }

        private void OnDisable()
        {
            _pressAction.Disable();
            _pointerPosition.Disable();
        }

        private void Update()
        {
            if (!AcceptsInput || _camera == null)
            {
                return;
            }

            // Responding on press rather than release keeps the blast feeling immediate.
            if (!_pressAction.WasPressedThisFrame() || IsPointerOverUi())
            {
                return;
            }

            var screenPosition = _pointerPosition.ReadValue<Vector2>();
            var world = _camera.ScreenToWorldPoint(screenPosition);
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

using Blast.Items;
using UnityEngine;

namespace Blast.Gameplay
{
    /// <summary>
    /// One half of a splitting rocket, as a self-contained visual.
    ///
    /// The four direction sprites live here, on the prefab, rather than on whatever spawns it. That
    /// puts every piece of a rocket half's appearance — artwork, trail, sorting — in one asset an
    /// artist can open and change, and means the spawner needs to know nothing about it beyond which
    /// way it is travelling.
    ///
    /// A half is not a board item: it occupies no cell, cannot be tapped and carries no damage rule.
    /// The sweep that owns it applies the damage; this only draws it.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class RocketHalfView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;

        [Header("Direction artwork")]
        [SerializeField] private Sprite _horizontalLeft;
        [SerializeField] private Sprite _horizontalRight;
        [SerializeField] private Sprite _verticalDown;
        [SerializeField] private Sprite _verticalUp;

        [Header("Trail")]
        [Tooltip("Optional exhaust left along the sweep. Simulated in world space so it stays put " +
                 "as the half moves on.")]
        [SerializeField] private ParticleSystem _trail;

        [Tooltip("How long a released trail is given to burn out before it is disposed of.")]
        [SerializeField] private float _trailLingerSeconds = 1f;

        /// <summary>Points the half in the direction it is about to travel.</summary>
        /// <param name="positive">True for the half heading right or up.</param>
        public void Show(Rocket.Axis axis, bool positive)
        {
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }

            _spriteRenderer.sprite = SpriteFor(axis, positive);

            // Clipped to the board so the half vanishes at the edge rather than flying off it. Set
            // here rather than on the prefab for the same reason board items are: a masked renderer
            // draws nothing without a SpriteMask present, which would leave the prefab with a blank
            // asset preview. See GridItem.ClipToBoard.
            _spriteRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        }

        /// <summary>
        /// Hands the trail over before the half is destroyed.
        ///
        /// The exhaust is laid down in world space along the whole sweep, so destroying it with the
        /// half would delete smoke that is still sitting in the middle of the board. Detaching it and
        /// letting it burn out is the difference between a rocket that fades and one that blinks out.
        /// </summary>
        public void Release()
        {
            if (_trail == null)
            {
                return;
            }

            _trail.transform.SetParent(null, worldPositionStays: true);
            _trail.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            ObjectLifetime.Destroy(_trail.gameObject, _trailLingerSeconds);
            _trail = null;
        }

        private Sprite SpriteFor(Rocket.Axis axis, bool positive)
        {
            if (axis == Rocket.Axis.Horizontal)
            {
                return positive ? _horizontalRight : _horizontalLeft;
            }

            return positive ? _verticalUp : _verticalDown;
        }
    }
}

using System;
using UnityEngine;

namespace Blast.Items
{
    /// <summary>
    /// Chalice box. The only multi-cell item: a static 2x2 obstacle authored in the level files as
    /// four corner codes.
    ///
    /// It clears in two phases, and the difference between them is entirely about how damage is
    /// counted:
    /// while the doors hold, every damage source deals exactly one damage no matter how many of the
    /// box's four cells it covered. Once the doors are gone each point of damage collects a chalice,
    /// and the count now scales with coverage — an explosion contributes one per cell it covers (so a
    /// rocket crossing two cells gives two), and an adjacent blast contributes one per adjacent cube.
    ///
    /// That distinction is why <see cref="DamageInfo"/> carries a source identity: an explosion
    /// arrives here as several separate calls, one per covered cell.
    /// </summary>
    public sealed class ChaliceBox : Obstacle
    {
        /// <summary>Which of the two clear stages the box is currently in.</summary>
        public enum Phase
        {
            Doors,
            Chalices
        }

        /// <summary>Sentinel for "not started yet"; see <see cref="Vase"/> for why this is lazy.</summary>
        private const int Uninitialised = -1;

        [Header("Renderers")]
        [SerializeField] private SpriteRenderer _boxRenderer;

        [Tooltip("Hidden once the doors are destroyed.")]
        [SerializeField] private SpriteRenderer _doorsRenderer;

        [Tooltip("Draws the chalices the box still holds, revealed when the doors break.")]
        [SerializeField] private ChaliceShelfView _shelfView;

        [Header("Health")]
        [Tooltip("Damage sources needed to destroy the doors.")]
        [SerializeField] private int _doorHitPoints = 1;

        [Tooltip("Chalices held by the box once its doors are open.")]
        [SerializeField] private int _chaliceCount = 10;

        [Header("Effects")]
        [Tooltip("Played when the doors are destroyed.")]
        [SerializeField] private ParticleSystem _doorBreakEffectPrefab;

        [Tooltip("Played at the box each time chalices are taken from it.")]
        [SerializeField] private ParticleSystem _chaliceCollectEffectPrefab;

        private int _doorHitPointsRemaining = Uninitialised;
        private int _chalicesRemaining;

        /// <summary>Source of the hit currently being applied, so repeat cells can be recognised.</summary>
        private int _currentSourceId = int.MinValue;

        /// <summary>True once the current source has had its full effect and must not apply again.</summary>
        private bool _currentSourceSpent;

        /// <summary>Raised with the number of chalices collected and where they came from.</summary>
        public event Action<int, Vector3> ChalicesCollected;

        /// <summary>
        /// Raised when the box wants a one-shot effect played at a world position.
        ///
        /// Exposed rather than played here so effects stay pooled in one place instead of each item
        /// instantiating its own. One event rather than one per occasion, so another moment worth a
        /// burst needs no new wiring.
        /// </summary>
        public event Action<ParticleSystem, Vector3> EffectRequested;

        /// <summary>Occupies a 2x2 block anchored at its bottom-left cell.</summary>
        public override Vector2Int Size => new(2, 2);

        /// <summary>The box is fixed in place and blocks falls, like stone.</summary>
        public override bool CanFall => false;

        /// <summary>Chalices this box still holds, counting the door phase as none collected yet.</summary>
        public int ChalicesRemaining =>
            _doorHitPointsRemaining == Uninitialised ? _chaliceCount : _chalicesRemaining;

        /// <summary>Total chalices this box contributes to the level goal.</summary>
        public int ChaliceCount => _chaliceCount;

        public ParticleSystem DoorBreakEffectPrefab => _doorBreakEffectPrefab;

        public override DamageResult ApplyDamage(DamageInfo damage)
        {
            EnsureInitialised();

            if (damage.SourceId != _currentSourceId)
            {
                _currentSourceId = damage.SourceId;
                _currentSourceSpent = false;
            }
            else if (_currentSourceSpent)
            {
                return DamageResult.Ignored;
            }

            return _doorHitPointsRemaining > 0
                ? DamageDoors()
                : CollectChalices(damage);
        }

        /// <summary>Shows or hides the doors to reflect the current phase.</summary>
        public void ShowPhase(Phase phase)
        {
            if (_doorsRenderer != null)
            {
                _doorsRenderer.enabled = phase == Phase.Doors;
            }

            RefreshShelves();
        }

        protected override void Awake()
        {
            base.Awake();

            // Play mode only. The editor verification tools never reach Awake, which is why the
            // same call is repeated lazily on the damage path.
            EnsureInitialised();
        }

        /// <summary>
        /// One damage per source, whatever it covered. A source that breaks the doors is spent on
        /// them and does not go on to collect chalices in the same hit.
        /// </summary>
        private DamageResult DamageDoors()
        {
            _currentSourceSpent = true;
            _doorHitPointsRemaining--;

            if (_doorHitPointsRemaining > 0)
            {
                return DamageResult.Damaged;
            }

            ShowPhase(Phase.Chalices);
            EffectRequested?.Invoke(_doorBreakEffectPrefab, transform.position);

            return DamageResult.Damaged;
        }

        /// <summary>
        /// Each hit collects chalices. Explosions arrive one call per covered cell so they accumulate
        /// naturally; a blast arrives once carrying its adjacent cube count.
        /// </summary>
        private DamageResult CollectChalices(DamageInfo damage)
        {
            var collected = Mathf.Clamp(damage.Amount, 1, _chalicesRemaining);
            if (collected <= 0)
            {
                return DamageResult.Destroyed;
            }

            _chalicesRemaining -= collected;

            ChalicesCollected?.Invoke(collected, transform.position);
            EffectRequested?.Invoke(_chaliceCollectEffectPrefab, transform.position);

            if (_chalicesRemaining <= 0)
            {
                return DamageResult.Destroyed;
            }

            RefreshShelves();
            return DamageResult.Damaged;
        }

        /// <summary>Brings the shelf artwork in line with how much the box still holds.</summary>
        private void RefreshShelves()
        {
            if (_shelfView != null)
            {
                _shelfView.Show(ChalicesRemaining);
            }
        }

        private void EnsureInitialised()
        {
            if (_doorHitPointsRemaining != Uninitialised)
            {
                return;
            }

            _doorHitPointsRemaining = Mathf.Max(1, _doorHitPoints);
            _chalicesRemaining = Mathf.Max(1, _chaliceCount);

            RefreshShelves();
        }
    }
}

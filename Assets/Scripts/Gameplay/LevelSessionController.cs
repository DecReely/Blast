using Blast.Effects;
using Blast.Items;
using Blast.Levels;
using Blast.Motion;
using UnityEngine;

namespace Blast.Gameplay
{
    /// <summary>
    /// Entry point for <c>LevelScene</c>: decides which level to play, builds the board and places
    /// the frame around it.
    ///
    /// Deliberately the only thing that wires the pieces together, so the board, the builder and the
    /// camera stay independent of each other and of how a level was chosen.
    /// </summary>
    public sealed class LevelSessionController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private LevelDatabase _levelDatabase;
        [SerializeField] private ItemCatalog _itemCatalog;

        [Header("Scene references")]
        [SerializeField] private Board _board;
        [SerializeField] private BoardFrame _boardFrame;
        [SerializeField] private BoardMask _boardMask;
        [SerializeField] private BoardInput _boardInput;
        [SerializeField] private ParticleEffectPool _effectPool;
        [SerializeField] private FallAnimator _fallAnimator;
        [SerializeField] private MergeAnimator _mergeAnimator;
        [SerializeField] private BoardActionRunner _actionRunner;
        [SerializeField] private SpecialItemVisuals _specialItemVisuals;

        [Header("Layout")]
        [Tooltip("World position the board's centre is pinned to. Constant for every level, so " +
                 "boards of different sizes stay put instead of being re-fitted to the screen.")]
        [SerializeField] private Vector2 _boardCenter = new(0f, -1.7f);

        [Header("Editor testing")]
        [Tooltip("Force a specific level number when playing LevelScene directly. 0 uses saved progress.")]
        [SerializeField] private int _levelNumberOverride;

        /// <summary>The level currently loaded, or null if loading failed.</summary>
        public LevelDefinition CurrentLevel { get; private set; }

        /// <summary>Moves remaining in the current level. Recreated on every load.</summary>
        public MoveCounter Moves { get; private set; }

        /// <summary>Resolves taps for the current level. Recreated on every load.</summary>
        public BoardCoordinator Coordinator { get; private set; }

        private void Start()
        {
            LoadLevel(ResolveLevelNumber());
        }

        private void OnDisable()
        {
            if (_boardInput != null)
            {
                _boardInput.WorldTapped -= OnWorldTapped;
            }
        }

        /// <summary>Rebuilds the scene for a given 1-based level number.</summary>
        public void LoadLevel(int levelNumber)
        {
            if (_levelDatabase == null || _itemCatalog == null || _board == null)
            {
                Debug.LogError("[LevelSessionController] Missing a required reference.", this);
                return;
            }

            var level = _levelDatabase.Load(levelNumber);
            if (level == null)
            {
                return;
            }

            CurrentLevel = level;

            BoardBuilder.ClearItems(_board);
            _board.Initialize(level.Width, level.Height, _boardCenter);

            new BoardBuilder(_itemCatalog).Populate(_board, level);

            if (_boardFrame != null)
            {
                _boardFrame.Fit(_board);
            }

            if (_boardMask != null)
            {
                _boardMask.Fit(_board);
            }

            StartSession(level);
        }

        /// <summary>
        /// Builds the per-level gameplay objects. They are recreated rather than reset so replaying a
        /// level cannot inherit state from the previous attempt.
        /// </summary>
        private void StartSession(LevelDefinition level)
        {
            var groupFinder = new GroupFinder();

            Moves = new MoveCounter(level.MoveCount);
            Coordinator = new BoardCoordinator(
                _board,
                groupFinder,
                new HintController(groupFinder),
                Moves,
                _effectPool,
                new GravitySystem(_itemCatalog),
                new ExplosionSystem(_board, _actionRunner, _effectPool, _specialItemVisuals),
                _actionRunner,
                new SpecialItemFactory(_itemCatalog),
                _fallAnimator,
                _mergeAnimator);

            Coordinator.RefreshHints();

            if (_boardInput == null)
            {
                return;
            }

            // Re-subscribing on every load, so guard against stacking duplicate handlers.
            _boardInput.WorldTapped -= OnWorldTapped;
            _boardInput.WorldTapped += OnWorldTapped;
            _boardInput.AcceptsInput = true;
        }

        private void OnWorldTapped(Vector3 worldPosition)
        {
            Coordinator?.HandleWorldTap(worldPosition);
        }

        /// <summary>
        /// Prefers the editor override so a level can be opened directly, then falls back to saved
        /// progress. Progress can legitimately exceed the level count once every level is finished,
        /// in which case the last level is replayed rather than failing to load.
        /// </summary>
        private int ResolveLevelNumber()
        {
            if (_levelNumberOverride > 0)
            {
                return _levelNumberOverride;
            }

            var current = LevelProgress.CurrentLevel;
            return _levelDatabase != null && current > _levelDatabase.LevelCount
                ? _levelDatabase.LevelCount
                : current;
        }
    }
}

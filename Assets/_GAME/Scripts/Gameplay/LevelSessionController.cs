using System;
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

        /// <summary>Obstacle goals for the current level. Recreated on every load.</summary>
        public GoalTracker Goals { get; private set; }

        /// <summary>How the current attempt stands.</summary>
        public LevelOutcome Outcome { get; private set; } = LevelOutcome.InProgress;

        /// <summary>Raised once, when the level is won or lost.</summary>
        public event Action<LevelOutcome> Finished;

        /// <summary>
        /// Raised after a level has been built and its counters exist. UI subscribes to this rather
        /// than reading the session in its own Start, which would depend on script execution order.
        /// </summary>
        public event Action LevelLoaded;

        /// <summary>Raised when a chalice box gives up chalices, with where they came from.</summary>
        public event Action<int, Vector3> ChalicesCollectedAt;

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
                new ComboDetector(),
                new HintController(groupFinder),
                Moves,
                _effectPool,
                new GravitySystem(_itemCatalog),
                new ExplosionSystem(_board, _actionRunner, _effectPool, _specialItemVisuals),
                _actionRunner,
                new SpecialItemFactory(_itemCatalog),
                _fallAnimator,
                _mergeAnimator,
                _specialItemVisuals);

            Goals = new GoalTracker();
            Goals.Initialize(_board);
            SubscribeToChaliceBoxes();

            Outcome = LevelOutcome.InProgress;
            Coordinator.TurnResolved += OnTurnResolved;
            Coordinator.RefreshHints();

            if (_boardInput != null)
            {
                // Re-subscribing on every load, so guard against stacking duplicate handlers.
                _boardInput.WorldTapped -= OnWorldTapped;
                _boardInput.WorldTapped += OnWorldTapped;
                _boardInput.AcceptsInput = true;
            }

            LevelLoaded?.Invoke();
        }

        private void OnWorldTapped(Vector3 worldPosition)
        {
            Coordinator?.HandleWorldTap(worldPosition);
        }

        /// <summary>
        /// Boxes report their own progress, since a box is destroyed the moment it is emptied and its
        /// contribution would otherwise be lost.
        /// </summary>
        private void SubscribeToChaliceBoxes()
        {
            for (var y = 0; y < _board.Height; y++)
            {
                for (var x = 0; x < _board.Width; x++)
                {
                    var cell = new Vector2Int(x, y);

                    // A box covers four cells; subscribe only at its anchor.
                    if (_board.GetItem(cell) is not ChaliceBox box || box.Origin != cell)
                    {
                        continue;
                    }

                    box.ChalicesCollected += OnChalicesCollected;
                    box.EffectRequested += OnBoxEffectRequested;
                }
            }
        }

        private void OnChalicesCollected(int count, Vector3 worldPosition)
        {
            Goals.ReportChalices(count);
            ChalicesCollectedAt?.Invoke(count, worldPosition);
        }

        /// <summary>
        /// Plays an effect a box asked for. Routed through the pool here rather than by the box
        /// itself, so items never instantiate their own particle systems.
        /// </summary>
        private void OnBoxEffectRequested(ParticleSystem effect, Vector3 worldPosition)
        {
            if (_effectPool != null)
            {
                _effectPool.Play(effect, worldPosition);
            }
        }

        /// <summary>
        /// Decides the level's fate once the board has settled.
        ///
        /// Checked after the turn fully resolves rather than the moment the last obstacle breaks,
        /// because a rocket still in flight can clear the final goal, and because spending the last
        /// move on a winning turn should still be a win.
        /// </summary>
        private void OnTurnResolved(int clearedCubes)
        {
            if (Outcome != LevelOutcome.InProgress)
            {
                return;
            }

            Goals.Refresh(_board);

            if (Goals.IsComplete)
            {
                Finish(LevelOutcome.Won);
                return;
            }

            if (!Moves.HasMovesLeft)
            {
                Finish(LevelOutcome.Failed);
            }
        }

        private void Finish(LevelOutcome outcome)
        {
            Outcome = outcome;

            if (_boardInput != null)
            {
                _boardInput.AcceptsInput = false;
            }

            Finished?.Invoke(outcome);
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

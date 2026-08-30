using Blast.Gameplay;
using Blast.Levels;
using UnityEngine;

namespace Blast.UI
{
    /// <summary>
    /// Joins the gameplay session to the level's UI and decides what happens when it ends.
    ///
    /// The session knows only whether the level was won or lost; everything about celebrating,
    /// persisting progress and moving between scenes lives here, so neither side has to know about
    /// the other.
    /// </summary>
    public sealed class LevelFlowController : MonoBehaviour
    {
        [SerializeField] private LevelSessionController _session;

        [Header("UI")]
        [SerializeField] private TopBarView _topBar;
        [SerializeField] private FailPopup _failPopup;
        [SerializeField] private WinCelebration _winCelebration;
        [SerializeField] private ChaliceFlightView _chaliceFlight;
        [SerializeField] private Camera _boardCamera;

        private void Awake()
        {
            if (_session == null)
            {
                Debug.LogError("[LevelFlowController] No level session assigned.", this);
                return;
            }

            // Subscribed in Awake so the level's own Start can safely raise these.
            _session.LevelLoaded += OnLevelLoaded;
            _session.Finished += OnLevelFinished;
            _session.ChalicesCollectedAt += OnChalicesCollected;

            if (_chaliceFlight != null)
            {
                _chaliceFlight.Initialize(_boardCamera);
            }
        }

        private void OnDestroy()
        {
            if (_session == null)
            {
                return;
            }

            _session.LevelLoaded -= OnLevelLoaded;
            _session.Finished -= OnLevelFinished;
            _session.ChalicesCollectedAt -= OnChalicesCollected;
        }

        private void OnLevelLoaded()
        {
            if (_topBar != null)
            {
                _topBar.Bind(_session.Moves, _session.Goals);
            }

            _failPopup?.Hide();
            _winCelebration?.Hide();
        }

        private void OnChalicesCollected(int count, Vector3 worldPosition)
        {
            if (_chaliceFlight == null || _topBar == null)
            {
                return;
            }

            _chaliceFlight.Fly(count, worldPosition, _topBar.ChalicePanelWorldPosition);
        }

        private void OnLevelFinished(LevelOutcome outcome)
        {
            if (outcome == LevelOutcome.Won)
            {
                AdvanceProgress();
                PlayCelebration();
                return;
            }

            _failPopup?.Show();
        }

        /// <summary>
        /// Records the win before the celebration plays, so progress survives even if the player
        /// closes the game during it.
        /// </summary>
        private void AdvanceProgress()
        {
            var completed = _session.CurrentLevel?.LevelNumber ?? LevelProgress.CurrentLevel;

            // Only move forward: replaying an earlier level must not roll progress back.
            if (completed >= LevelProgress.CurrentLevel)
            {
                LevelProgress.AdvanceTo(completed + 1);
            }
        }

        private void PlayCelebration()
        {
            if (_winCelebration == null)
            {
                SceneLoader.GoToMainMenu();
                return;
            }

            _winCelebration.Play(SceneLoader.GoToMainMenu);
        }
    }
}

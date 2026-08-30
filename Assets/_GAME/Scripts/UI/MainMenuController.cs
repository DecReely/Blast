using Blast.Levels;
using UnityEngine;
using UnityEngine.UI;

namespace Blast.UI
{
    /// <summary>
    /// Drives the main menu's single button.
    ///
    /// It shows the level the player is up to, or "Finished" once every level has been cleared, and
    /// the label is read from persisted progress on every enable so returning from a win shows the
    /// new number immediately.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private LevelDatabase _levelDatabase;
        [SerializeField] private Button _levelButton;
        [SerializeField] private Text _levelLabel;

        [Tooltip("Shown once the player has cleared every level.")]
        [SerializeField] private string _finishedText = "Finished";

        private void OnEnable()
        {
            Refresh();
        }

        private void Awake()
        {
            if (_levelButton != null)
            {
                _levelButton.onClick.AddListener(OnLevelButtonClicked);
            }
        }

        private void OnDestroy()
        {
            if (_levelButton != null)
            {
                _levelButton.onClick.RemoveListener(OnLevelButtonClicked);
            }
        }

        /// <summary>Re-reads persisted progress and updates the button. Called on every enable.</summary>
        public void Refresh()
        {
            var finished = LevelProgress.HasFinishedAllLevels(_levelDatabase);

            if (_levelLabel != null)
            {
                _levelLabel.text = finished ? _finishedText : $"Level {LevelProgress.CurrentLevel}";
            }

            // Nothing left to play, so the button stops responding rather than reloading the last level.
            if (_levelButton != null)
            {
                _levelButton.interactable = !finished;
            }
        }

        private void OnLevelButtonClicked()
        {
            if (LevelProgress.HasFinishedAllLevels(_levelDatabase))
            {
                return;
            }

            SceneLoader.GoToLevel();
        }
    }
}

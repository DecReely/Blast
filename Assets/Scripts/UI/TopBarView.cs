using Blast.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Blast.UI
{
    /// <summary>
    /// Shows the moves left and the level's obstacle goals.
    ///
    /// A pure view: it subscribes to the move counter and goal tracker and reflects them. It never
    /// decides anything, so the rules stay in the gameplay layer and the bar can be redesigned
    /// without touching them.
    /// </summary>
    public sealed class TopBarView : MonoBehaviour
    {
        [SerializeField] private Text _moveLabel;

        [Header("Goals")]
        [SerializeField] private GridLayoutGroup _goalLayout;
        [SerializeField] private GoalItemView _vaseGoal;
        [SerializeField] private GoalItemView _stoneGoal;
        [SerializeField] private GoalItemView _chaliceGoal;

        private GoalTracker _goals;
        private MoveCounter _moves;

        /// <summary>Where a collected chalice should fly to, so the flight has somewhere to land.</summary>
        public Vector3 ChalicePanelWorldPosition =>
            _chaliceGoal != null ? _chaliceGoal.IconWorldPosition : transform.position;

        /// <summary>Points the bar at a level's counters and shows only the goals it actually has.</summary>
        public void Bind(MoveCounter moves, GoalTracker goals)
        {
            Unbind();

            _moves = moves;
            _goals = goals;

            if (_moves != null)
            {
                _moves.RemainingChanged += OnMovesChanged;
            }

            if (_goals != null)
            {
                _goals.Changed += OnGoalsChanged;
            }

            ShowRelevantGoals();
            OnMovesChanged(_moves?.Remaining ?? 0);
            OnGoalsChanged();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void Unbind()
        {
            if (_moves != null)
            {
                _moves.RemainingChanged -= OnMovesChanged;
                _moves = null;
            }

            if (_goals != null)
            {
                _goals.Changed -= OnGoalsChanged;
                _goals = null;
            }
        }

        /// <summary>
        /// Only the obstacle types present in the level get a slot, so a stones-only level does not
        /// display empty vase and chalice entries.
        /// </summary>
        private void ShowRelevantGoals()
        {
            if (_goals == null)
            {
                return;
            }

            var shown = SetVisible(_vaseGoal, _goals.HasVaseGoal)
                        + SetVisible(_stoneGoal, _goals.HasStoneGoal)
                        + SetVisible(_chaliceGoal, _goals.HasChaliceGoal);

            ResizeGoalSlots(shown);
        }

        private static int SetVisible(GoalItemView view, bool visible)
        {
            if (view != null)
            {
                view.SetVisible(visible);
            }

            return visible ? 1 : 0;
        }

        /// <summary>
        /// The panel in the artwork is a fixed square, so the slots are scaled to fill it: a single
        /// goal gets the whole panel rather than sitting small in the middle of it.
        /// </summary>
        private void ResizeGoalSlots(int shown)
        {
            if (_goalLayout == null)
            {
                return;
            }

            _goalLayout.constraintCount = shown <= 1 ? 1 : 2;

            var cell = shown switch
            {
                <= 1 => 190f,
                2 => 114f,
                _ => 106f
            };

            _goalLayout.cellSize = new Vector2(cell, cell);
        }

        private void OnMovesChanged(int remaining)
        {
            if (_moveLabel != null)
            {
                _moveLabel.text = remaining.ToString();
            }
        }

        private void OnGoalsChanged()
        {
            if (_goals == null)
            {
                return;
            }

            if (_vaseGoal != null && _goals.HasVaseGoal)
            {
                _vaseGoal.SetRemaining(_goals.VasesRemaining);
            }

            if (_stoneGoal != null && _goals.HasStoneGoal)
            {
                _stoneGoal.SetRemaining(_goals.StonesRemaining);
            }

            if (_chaliceGoal != null && _goals.HasChaliceGoal)
            {
                _chaliceGoal.SetRemaining(_goals.ChalicesRemaining);
            }
        }
    }
}

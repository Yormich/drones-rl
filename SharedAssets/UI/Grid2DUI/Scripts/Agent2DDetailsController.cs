using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using GridWorld.Metrics;
using Agents;

namespace GridWorld.UI
{
    [DefaultExecutionOrder(-40)]
    public class Agent2DDetailsController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private VisualTreeAsset historyItemTemplate;

        // UI Element References
        private Label _episodeLabel;
        private Label _stepLabel;
        private Label _cumulativeRewardLabel;

        // Environment Params
        private Label _gridSizeLabel;
        private Label _numObstaclesLabel;

        // Observations (Directional)
        private Label _northObsLabel;
        private Label _southObsLabel;
        private Label _westObsLabel;
        private Label _eastObsLabel;

        // Distances
        private Label _distanceXLabel;
        private Label _distanceYLabel;

        // History List
        private ListView _actionHistoryList;

        // Logic State
        private Grid2DAgent _currentAgent;
        private Agent2DMetricCollector _currentCollector;
        private List<ActionHistoryEntry> _currentHistory = new List<ActionHistoryEntry>();

        private void OnEnable()
        {
            var root = uiDocument.rootVisualElement;
            QueryVisualElements(root);
            SetupListView();

            AgentListController.OnNewAgentSelected += OnAgentSelected;
        }

        private void OnDisable()
        {
            AgentListController.OnNewAgentSelected -= OnAgentSelected;
            UnhookAgentEvents();
        }

        private void QueryVisualElements(VisualElement root)
        {
            // Main Stats
            _episodeLabel = root.Q<Label>("EpisodeLabel");
            _stepLabel = root.Q<Label>("StepLabel");
            _cumulativeRewardLabel = root.Q<Label>("CumulativeRewardLabel");

            // Environment
            _gridSizeLabel = root.Q<Label>("GridSizeLabel");
            _numObstaclesLabel = root.Q<Label>("NumObstaclesLabel");

            // Observations
            _northObsLabel = root.Q<Label>("NorthObservationLabel");
            _southObsLabel = root.Q<Label>("SouthObservationLabel");
            _westObsLabel = root.Q<Label>("WestDirectionLabel");
            _eastObsLabel = root.Q<Label>("EastDirectionLabel");


            _distanceXLabel = root.Q<Label>("NormalizedXDistanceLabel");
            _distanceYLabel = root.Q<Label>("NormalizedYDistanceLabel");

            // List View
            _actionHistoryList = root.Q<ListView>("ActionHistoryList");
        }

        private void SetupListView()
        {
            if (_actionHistoryList == null) return;

            _actionHistoryList.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
            _actionHistoryList.fixedItemHeight = 70;

            // 2. Make Item (Instantiate the template)
            _actionHistoryList.makeItem = () =>
            {
                if (historyItemTemplate == null) return new Label("No Template Assigned");
                return historyItemTemplate.Instantiate();
            };

            // 3. Bind Item (Fill data)
            _actionHistoryList.bindItem = BindListItem;
        }

        private void BindListItem(VisualElement element, int index)
        {
            if (index < 0 || index >= _currentHistory.Count) return;

            var entry = _currentHistory[index];

            var stepLbl = element.Q<Label>("StepLabel");
            var actionLbl = element.Q<Label>("ActionLabel");
            var fromLbl = element.Q<Label>("FromLabel");
            var toLbl = element.Q<Label>("ToLabel");
            var rewardLbl = element.Q<Label>("StepRewardLabel");

            if (stepLbl != null) stepLbl.text = $"Step: {entry.StepIndex}";
            if (actionLbl != null) actionLbl.text = $"Performed action \"{entry.ActionLabel}\"";
            if (fromLbl != null) fromLbl.text = $"Moved from {entry.FromPos}";
            if (toLbl != null) toLbl.text = $"to {entry.ToPos}";
            if (rewardLbl != null) rewardLbl.text = $"with reward {entry.StepReward:F4}";
        }

        // --- Event Handlers ---

        private void OnAgentSelected(IAgent newAgent)
        {
            Grid2DAgent newAgentAs2D = newAgent as Grid2DAgent;
            if (_currentAgent != null && _currentAgent.AgentId == newAgent.AgentId || newAgentAs2D == null) return;

            UnhookAgentEvents();

            _currentAgent = newAgentAs2D;

            if (_currentAgent != null)
            {
                _currentCollector = _currentAgent.GetComponent<Agent2DMetricCollector>();
                if (_currentCollector != null)
                {
                    _currentCollector.OnDataUpdated += UpdateUI;

                    // Trigger an immediate manual update if data exists, 
                    // otherwise we wait for the next step
                    _currentCollector.UpdateStats(0);
                }
            }
            else
            {
                ClearUI();
            }
        }

        private void UnhookAgentEvents()
        {
            if (_currentCollector != null)
            {
                _currentCollector.OnDataUpdated -= UpdateUI;
                _currentCollector = null;
            }
        }

        // --- UI Updating ---

        private void UpdateUI(AgentUIData data)
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;

            if (_episodeLabel != null) _episodeLabel.text = $"Episode: {data.EpisodeNumber}";
            if (_stepLabel != null) _stepLabel.text = $"Current Step: {data.StepCount}";
            if (_cumulativeRewardLabel != null) _cumulativeRewardLabel.text = $"Cumulative Reward: {data.CumulativeReward:F4}";

            if (_gridSizeLabel != null) _gridSizeLabel.text = $"Grid Size: {data.GridSize} units";
            if (_numObstaclesLabel != null) _numObstaclesLabel.text = $"Num Obstacles: {data.NumObstacles}";

            // Observations
            if (_northObsLabel != null) _northObsLabel.text = $"North: {data.ObsNorth}";
            if (_southObsLabel != null) _southObsLabel.text = $"South: {data.ObsSouth}";
            if (_westObsLabel != null) _westObsLabel.text = $"West: {data.ObsWest}";
            if (_eastObsLabel != null) _eastObsLabel.text = $"East: {data.ObsEast}";

            // Distances
            if (_distanceXLabel != null) _distanceXLabel.text = $"Distance X: {data.NormalizedDistanceX:F3}";
            if (_distanceYLabel != null) _distanceYLabel.text = $"Distance Y: {data.NormalizedDistanceY:F3}";

            _currentHistory = data.ActionHistory;

            _actionHistoryList.itemsSource = _currentHistory;
            _actionHistoryList.Rebuild();
        }

        private void ClearUI()
        {
            if (_episodeLabel != null) _episodeLabel.text = "Episode: -";

            _currentHistory.Clear();
            _actionHistoryList.Rebuild();
        }
    }
}
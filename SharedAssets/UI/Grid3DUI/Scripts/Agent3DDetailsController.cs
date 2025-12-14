using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using GridWorld.Metrics;
using Agents;

namespace GridWorld.UI
{
    [DefaultExecutionOrder(-40)]
    public class Agent3DDetailsController : MonoBehaviour
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
        private Label _densityLevel;
        private Label _generationMethodLabel;

        // Raycast Observations (Replacing Cardinal Directions)
        private Label _rayFrontLabel;
        private Label _rayBackLabel;
        private Label _rayLeftLabel;
        private Label _rayRightLabel;
        private Label _rayUpLabel;
        private Label _rayDownLabel;

        // Distances (Includes Z)
        private Label _distanceXLabel;
        private Label _distanceYLabel;
        private Label _distanceZLabel;

        // History List
        private ListView _actionHistoryList;

        // Logic State
        private Grid3DAgent _currentAgent;
        private Agent3DMetricCollector _currentCollector;
        private List<ActionHistoryEntry3D> _currentHistory = new List<ActionHistoryEntry3D>();

        private void OnEnable()
        {
            var root = uiDocument.rootVisualElement;

            if (root != null)
            {
                QueryVisualElements(root);
                SetupListView();
            }

            AgentListController.OnNewAgentSelected += OnAgentSelected;

            if (AgentListController.Instance != null && AgentListController.Instance.CurrentSelectedAgent != null)
            {
                OnAgentSelected(AgentListController.Instance.CurrentSelectedAgent);
            }
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
            _densityLevel = root.Q<Label>("DensityLevelLabel");
            _generationMethodLabel = root.Q<Label>("GenerationMethodLabel");

            // Raycast Observations 
            _rayFrontLabel = root.Q<Label>("RayFrontLabel");
            _rayBackLabel = root.Q<Label>("RayBackLabel");
            _rayLeftLabel = root.Q<Label>("RayLeftLabel");
            _rayRightLabel = root.Q<Label>("RayRightLabel");
            _rayUpLabel = root.Q<Label>("RayUpLabel");
            _rayDownLabel = root.Q<Label>("RayDownLabel");

            // Distances
            _distanceXLabel = root.Q<Label>("NormalizedXDistanceLabel");
            _distanceYLabel = root.Q<Label>("NormalizedYDistanceLabel");
            _distanceZLabel = root.Q<Label>("NormalizedZDistanceLabel");

            // List View
            _actionHistoryList = root.Q<ListView>("ActionHistoryList");
        }

        private void SetupListView()
        {
            if (_actionHistoryList == null) return;

            _actionHistoryList.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;

            _actionHistoryList.makeItem = () =>
            {
                if (historyItemTemplate == null) return new Label("No Template Assigned");
                return historyItemTemplate.Instantiate();
            };

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
            if (actionLbl != null) actionLbl.text = $"Action: {entry.ActionLabel}";
            // Formatting Vector3Int nicely
            if (fromLbl != null) fromLbl.text = $"From: ({entry.FromPos.x}, {entry.FromPos.y}, {entry.FromPos.z})";
            if (toLbl != null) toLbl.text = $"To: ({entry.ToPos.x}, {entry.ToPos.y}, {entry.ToPos.z})";
            if (rewardLbl != null) rewardLbl.text = $"Reward: {entry.StepReward:F4}";
        }

        // --- Event Handlers ---

        private void OnAgentSelected(IAgent newAgent)
        {
            Grid3DAgent newAgentAs3D = newAgent as Grid3DAgent;

            UnhookAgentEvents();

            if (newAgentAs3D == null)
            {
                _currentAgent = null;
                ClearUI();
                return;
            }

            _currentAgent = newAgentAs3D;

            _currentCollector = _currentAgent.GetComponent<Agent3DMetricCollector>();
            if (_currentCollector != null)
            {
                _currentCollector.OnDataUpdated += UpdateUI;

                _currentCollector.UpdateStats(0);
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

        private void UpdateUI(Agent3DUiData data)
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;

            // Stats
            SetStatsData(data);

            // Ray Observations (Strings come directly from Collector logic)
            SetRayObservations(data);

            // Distances
            SetDistanceObservations(data);

            // History
            RebuildHistory(data);
        }

        private void ClearUI()
        {
            if (_episodeLabel != null) _episodeLabel.text = "Episode: -";
            _currentHistory.Clear();
            _actionHistoryList.Rebuild();
        }

        private void SetStatsData(Agent3DUiData data)
        {
            if (_episodeLabel != null) _episodeLabel.text = $"Episode: {data.EpisodeNumber}";
            if (_stepLabel != null) _stepLabel.text = $"Step: {data.StepCount}";
            if (_cumulativeRewardLabel != null) _cumulativeRewardLabel.text = $"Reward: {data.CumulativeReward:F4}";
            if (_gridSizeLabel != null) _gridSizeLabel.text = $"Size (width, height, length): {data.GridSize}";
            if (_densityLevel != null) _densityLevel.text = $"Density: {System.Math.Round(data.DensityLevel * 100)}s%";
            if (_generationMethodLabel != null) _generationMethodLabel.text = $"Generation: {data.GenerationType}";
        }

        private void SetRayObservations(Agent3DUiData data)
        {
            if (_rayFrontLabel != null) _rayFrontLabel.text = $"Forward: {data.RayFront}";
            if (_rayBackLabel != null) _rayBackLabel.text = $"Back: {data.RayBack}";
            if (_rayLeftLabel != null) _rayLeftLabel.text = $"Left: {data.RayLeft}";
            if (_rayRightLabel != null) _rayRightLabel.text = $"Right: {data.RayRight}";
            if (_rayUpLabel != null) _rayUpLabel.text = $"Above: {data.RayUp}";
            if (_rayDownLabel != null) _rayDownLabel.text = $"Below: {data.RayDown}";
        }

        private void SetDistanceObservations(Agent3DUiData data)
        {
            if (_distanceXLabel != null) _distanceXLabel.text = $"Distance X: {data.NormalizedDistanceX:F2}";
            if (_distanceYLabel != null) _distanceYLabel.text = $"Distance Y: {data.NormalizedDistanceY:F2}";
            if (_distanceZLabel != null) _distanceZLabel.text = $"Distance Z: {data.NormalizedDistanceZ:F2}";
        }

        private void RebuildHistory(Agent3DUiData data)
        {
            _currentHistory = data.ActionHistory;
            _actionHistoryList.itemsSource = _currentHistory;
            _actionHistoryList.Rebuild();
        }
    }
}
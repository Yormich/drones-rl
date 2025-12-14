using Agents;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;


namespace GridWorld.UI
{
    [DefaultExecutionOrder(-50)]
    public class AgentListController : MonoBehaviour
    {
        public static AgentListController Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private VisualTreeAsset itemTemplate;

        private ListView _listView;
        private readonly List<IAgent> _activeAgents = new();

        private class ItemUserData
        {
            public Label PositionLabel;
            public IAgent AgentRef;
        }

        private IAgent currentSelectedAgent;

        public IAgent CurrentSelectedAgent => currentSelectedAgent;

        public static event Action<IAgent> OnNewAgentSelected;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            // Subscribe to Agent events
            AgentEvents.OnAgentRegistered += HandleAgentRegistered;
            AgentEvents.OnAgentUnregistered += HandleAgentUnregistered;

            // Setup UI
            var root = uiDocument.rootVisualElement;
            _listView = root.Q<ListView>("AgentsList");
            
            SetupListView();
        }

        private void SetupListView()
        {
            _listView.makeItem = () =>
            {
                var newItem = itemTemplate.Instantiate();
                var userData = new ItemUserData
                {
                    PositionLabel = newItem.Q<Label>("AgentEnvironmentLocation")
                };
                newItem.userData = userData;
                return newItem;
            };

            _listView.bindItem = (element, index) =>
            {
                var agent = _activeAgents[index];
                var userData = (ItemUserData)element.userData;

                element.Q<Label>("AgentName").text = agent.GetAgentName();
                userData.AgentRef = agent;
                userData.PositionLabel.text = FormatPosition(agent.GetEnvironmentPosition());
            };

            _listView.itemsSource = _activeAgents;
            _listView.selectionType = SelectionType.Single;
            _listView.selectionChanged += OnAgentSelected;
        }

        private void OnAgentSelected(IEnumerable<object> selectedItems)
        {
            if (selectedItems.FirstOrDefault() is IAgent agent && agent.AgentId != currentSelectedAgent.AgentId)
            {
                currentSelectedAgent = agent;
                OnNewAgentSelected(currentSelectedAgent);
            }
        }

        private void OnDisable()
        {
            AgentEvents.OnAgentRegistered -= HandleAgentRegistered;
            AgentEvents.OnAgentUnregistered -= HandleAgentUnregistered;
        }

        private void HandleAgentRegistered(IAgent agent)
        {
            if (!_activeAgents.Contains(agent))
            {
                _activeAgents.Add(agent);
                RefreshList();

                if (currentSelectedAgent == null)
                {
                    currentSelectedAgent = agent;
                    OnNewAgentSelected?.Invoke(currentSelectedAgent);
                }
            }
        }

        private void HandleAgentUnregistered(IAgent agent)
        {
            if (_activeAgents.Contains(agent))
            {
                _activeAgents.Remove(agent);
                RefreshList();
            }
        }

        private void RefreshList()
        {
            // Notify UI Toolkit that the list size changed
            _listView.Rebuild();
        }

        private static string FormatPosition(Vector3 pos)
        {
            return $"Environment Location - (X:{pos.x:F1}, Y:{pos.y:F1}, Z:{pos.z:F1})";
        }
    }
}
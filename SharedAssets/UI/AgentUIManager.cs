using Agents;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridWorld.UI
{
    [DefaultExecutionOrder(-40)]
    public class AgentUIManager : MonoBehaviour
    {
        [Header("UI Query Settings")]
        [Tooltip("The Class Name or Element Name to search for in the UI Document to turn into windows.")]
        [SerializeField] private string modalContainerName = "AgentObservationContainer";

        [Header("Theming")]
        [SerializeField] private WindowThemeSO[] windowThemes;

        // The bridge: The currently focused agent
        private IAgent _currentSelectedAgent;

        // Managed window instances
        private readonly List<AgentWindowController> _windowControllers = new List<AgentWindowController>();

        private UIDocument _uiDocument;

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            InitializeWindows();

            AgentListController.OnNewAgentSelected += HandleAgentChanged;
        }

        private void OnDisable()
        {
            AgentListController.OnNewAgentSelected -= HandleAgentChanged;

            DisposeWindows();
        }

        private void InitializeWindows()
        {
            if (_uiDocument == null || windowThemes.Length == 0) return;

            var root = _uiDocument.rootVisualElement;

            // Find all elements matching the container name
            // We use Query to find ALL instances, allowing for multiple windows (Stats, Observations, etc.)
            var containers = root.Query(modalContainerName).ToList();

            for (int i = 0; i < containers.Count; i++)
            {
                VisualElement container = containers[i];
                WindowThemeSO windowTheme = windowThemes[i % windowThemes.Length];
                
                var controller = new AgentWindowController(
                    container,
                    windowTheme,
                    () => _currentSelectedAgent != null ? _currentSelectedAgent.GetAgentName() : "No Agent"
                );

                _windowControllers.Add(controller);
            }
        }

        private void HandleAgentChanged(IAgent newAgent)
        {
            _currentSelectedAgent = newAgent;

            foreach (var window in _windowControllers)
            {
                window.Refresh();
            }
        }

        private void DisposeWindows()
        {
            foreach (var window in _windowControllers)
            {
                window.Dispose();
            }
            _windowControllers.Clear();
        }
    }
}
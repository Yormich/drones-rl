using Agents;
using GridWorld.UI;
using GridWorld;
using SideChannels;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContinuousWorld.UI
{
    [DefaultExecutionOrder(-40)]
    [RequireComponent(typeof(UIDocument))]
    public class ContinuousVisualObservationController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private UIDocument uiDocument;

        private VisualElement _root;
        private VisualElement _activeAgentTrailingView;

        private void OnEnable()
        {
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            _root = uiDocument.rootVisualElement;

            if (_root == null)
            {
                Debug.LogError("ContinuousVisualObservationController: No Root VisualElement found.");
                return;
            }

            _activeAgentTrailingView = _root.Q<VisualElement>("AgentTrailingViewObservation");

            AgentEvents.OnAgentRegistered += HandleAgentUpdate;
            AgentEvents.OnAgentEnvironmentReady += HandleEnvironmentReady;
            AgentListController.OnNewAgentSelected += HandleAgentUpdate;


            // Initial check in case we start late
            if (AgentListController.Instance != null && AgentListController.Instance.CurrentSelectedAgent != null)
            {
                HandleAgentUpdate(AgentListController.Instance.CurrentSelectedAgent);
            }
        }

        private void OnDisable()
        {
            AgentEvents.OnAgentRegistered -= HandleAgentUpdate;
            AgentEvents.OnAgentEnvironmentReady -= HandleEnvironmentReady;
            AgentListController.OnNewAgentSelected -= HandleAgentUpdate;
        }

        private void HandleEnvironmentReady(IAgent agent)
        {
            // Only update if the agent reporting "Ready" is the one currently displayed
            if (AgentListController.Instance.CurrentSelectedAgent is ContinuousDroneAgent &&
                AgentListController.Instance.CurrentSelectedAgent.AgentId == agent.AgentId)
            {
                HandleAgentUpdate(agent);
            }
        }

        private void HandleAgentUpdate(IAgent agent)
        {
            if (agent is ContinuousDroneAgent agentCon)
            {
                UpdateFeeds(agentCon);
            }
            else
            {
                ClearFeeds();
            }
        }

        private void UpdateFeeds(ContinuousDroneAgent agent)
        {
            if (agent == null || agent.Area == null) return;

            ContinuousArea area = agent.Area;

            SetBackground(_activeAgentTrailingView, area.GetTrailingCameraTexture());
        }

        private void ClearFeeds()
        {
            SetBackground(_activeAgentTrailingView, null);
        }

        private static void SetBackground(VisualElement element, RenderTexture rt)
        {
            if (element == null) return;

            if (rt != null)
            {
                element.style.backgroundImage = Background.FromRenderTexture(rt);

                element.style.opacity = 1f;
            }
            else
            {
                element.style.backgroundImage = null;

                element.style.opacity = 0.1f;
            }
        }
    }
}
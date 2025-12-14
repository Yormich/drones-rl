using UnityEngine;
using UnityEngine.UIElements;
using SideChannels;
using Agents;

namespace GridWorld.UI
{
    [DefaultExecutionOrder(-40)]
    [RequireComponent(typeof(UIDocument))]
    public class AgentVisualObservationUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private UIDocument uiDocument;

        // Visual Elements mapped to your UXML names
        private VisualElement _root;
        private VisualElement _godView;
        private VisualElement _forwardView;
        private VisualElement _backView;
        private VisualElement _leftView;
        private VisualElement _rightView;
        private VisualElement _aboveView;
        private VisualElement _underView;

        private void OnEnable()
        {
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            _root = uiDocument.rootVisualElement;

            if (_root == null)
            {
                Debug.LogError("AgentVisualObservationUI: No Root VisualElement found.");
                return;
            }

            _godView = _root.Q<VisualElement>("GodViewObservation");
            _forwardView = _root.Q<VisualElement>("ForwardViewObservation");
            _backView = _root.Q<VisualElement>("BackViewObservation");
            _leftView = _root.Q<VisualElement>("LeftViewObservation");
            _rightView = _root.Q<VisualElement>("RightViewObservation");
            _aboveView = _root.Q<VisualElement>("AboveViewObservation");
            _underView = _root.Q<VisualElement>("UnderViewObservation");


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
            if (AgentListController.Instance.CurrentSelectedAgent != null &&
                AgentListController.Instance.CurrentSelectedAgent.AgentId == agent.AgentId)
            {
                HandleAgentUpdate(agent);
            }
        }

        private void HandleAgentUpdate(IAgent agent)
        {
            // We only show visuals for 3D Agents
            if (agent is Grid3DAgent agent3D)
            {
                UpdateFeeds(agent3D);
            }
            else
            {
                ClearFeeds();
            }
        }

        private void UpdateFeeds(Grid3DAgent agent)
        {
            if (agent == null || agent.Area == null) return;
            GridArea3D area = agent.Area;

            // Apply RenderTextures to Backgrounds
            SetBackground(_godView, area.GetCameraTexture(Grid3DViewType.GodView));
            SetBackground(_forwardView, area.GetCameraTexture(Grid3DViewType.Front));
            SetBackground(_backView, area.GetCameraTexture(Grid3DViewType.Back));
            SetBackground(_leftView, area.GetCameraTexture(Grid3DViewType.Left));
            SetBackground(_rightView, area.GetCameraTexture(Grid3DViewType.Right));
            SetBackground(_aboveView, area.GetCameraTexture(Grid3DViewType.Up));
            SetBackground(_underView, area.GetCameraTexture(Grid3DViewType.Down));
        }

        private void ClearFeeds()
        {
            SetBackground(_godView, null);
            SetBackground(_forwardView, null);
            SetBackground(_backView, null);
            SetBackground(_leftView, null);
            SetBackground(_rightView, null);
            SetBackground(_aboveView, null);
            SetBackground(_underView, null);
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
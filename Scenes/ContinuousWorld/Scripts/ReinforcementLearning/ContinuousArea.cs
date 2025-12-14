using UnityEngine;
using System.Collections;
using Unity.MLAgents.Sensors;
using DroneMovement;
using System.Linq;
using NUnit.Framework;
using System.Collections.Generic;
using GridWorld.UI;
using SideChannels;
using Agents;
using GridWorld.Visuals;
using ContinuousWorld.Visuals;

namespace ContinuousWorld
{
    public class ContinuousArea : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject goal;

        [Header("Visual Observations")]
        [SerializeField] private GameObject droneCameraPrefab;
        [SerializeField] private Vector2Int cameraResolution = new Vector2Int(256, 256);

        private GameObject _camObject;
        private CameraCapture _camCapture;
        private DroneFollowCamera _followScript;

        [Header("Agent Configuration")]
        [SerializeField] private float agentSafetyHeight = 10f;
        [SerializeField] private float goalSafetyHeight = 1.0f;

        [Tooltip("Prefab for the boundary visualizer (simple cube with wireframe shader and colliders on all sides)")]
        [SerializeField] private GameObject wireframePrefab;

        private TerrainGenerator terrainGenerator;
        private DronePhysics dronePhysics;
        private ContinuousDroneAgent agent;
        private GameObject _activeWireframe;

        private float _visionRadius;
        private float _currentWorldExtent;
        private ColorFlashFeedback[] colorFlashFeedbacks;
        private Vector3 _orchestratorOrigin;
        private bool _initializedOrigin = false;

        private void Start()
        {
            if (!_initializedOrigin)
            {
                _orchestratorOrigin = transform.position;
                _initializedOrigin = true;
            }

            this.agent = GetComponentInChildren<ContinuousDroneAgent>();
            this.dronePhysics = agent.GetComponent<DronePhysics>();
            this.colorFlashFeedbacks = GetComponentsInChildren<ColorFlashFeedback>();
            this.terrainGenerator = GetComponentInChildren<TerrainGenerator>();

            var lastLod = terrainGenerator.DetailLevels[^1];
            _visionRadius = lastLod.visibleDstThreshold;

            float defaultRayLength = 20f;
            var raySensors = agent.GetComponentsInChildren<RayPerceptionSensorComponent3D>();
            float raySensorLength = raySensors.Length > 0 ? raySensors.Max(s => s.RayLength) : defaultRayLength;

            terrainGenerator.SetColliderGenerationDistanceThreshold(raySensorLength);
            terrainGenerator.SetActiveViewer(agent.transform);

            if (wireframePrefab != null)
            {
                _activeWireframe = Instantiate(wireframePrefab, transform);
                _activeWireframe.tag = "Boundary";
                _activeWireframe.name = "WorldBoundary";
            }

            InitializeVisualCamera();
        }

        private void InitializeVisualCamera()
        {
            if (droneCameraPrefab == null) return;

            if (_camObject == null)
            {
                _camObject = Instantiate(droneCameraPrefab, transform);
                _camObject.name = "DroneTrailingCam";

                _camCapture = _camObject.AddComponent<CameraCapture>();
                _camCapture.Initialize(cameraResolution.x, cameraResolution.y);

                _followScript = _camObject.AddComponent<DroneFollowCamera>();
            }

            if (agent != null && _followScript != null)
            {
                _followScript.SetTarget(agent.transform);
            }
        }

        public void ResetArea()
        {
            StopAllCoroutines();

            ContinuousWorldSettings.Instance.UpdateActiveSeed();
            UpdateWorldBounds();

            dronePhysics.SetRelevantAerodynamicDrag(ContinuousWorldSettings.Instance.GetActiveDragOverride());
            terrainGenerator.ResetTerrain();

            if (_followScript != null)
            {
                _followScript.SetTarget(agent.transform);
            }

            StartCoroutine(SpawnLogic());
        }

        public float WorldExtent => _currentWorldExtent;

        public Vector3 GoalPosition => goal.transform.position;

        private void UpdateWorldBounds()
        {
            // Calculate the physical edge of the allowed area
            // Radius 0 = 0.5 * Size (Just center chunk)
            // Radius 1 = 1.5 * Size (3x3 chunks)
            int chunkRadius = ContinuousWorldSettings.Instance.GetActiveChunkRadius();
            float meshSize = ContinuousWorldSettings.Instance.MeshSettings.MeshWorldSize;

            _currentWorldExtent = (chunkRadius + 0.5f) * meshSize;

            // The Orchestrator placed us at the Top-Left corner (OrchestratorOrigin).
            // The Terrain generates around (0,0,0) local.
            // We shift +X and -Z so that our (0,0,0) center sits in the middle of the Orchestrator's grid cell.
            Vector3 centerOffset = new Vector3(_currentWorldExtent, 0f, -_currentWorldExtent);
            transform.position = _orchestratorOrigin + centerOffset;

            // Update Wireframe Visuals
            if (_activeWireframe != null)
            {
                float totalWidth = _currentWorldExtent * 2f;
                float height = ContinuousWorldSettings.Instance.GetMaxPhysicalSize().y;

                _activeWireframe.transform.localScale = new Vector3(totalWidth, height, totalWidth);

                _activeWireframe.transform.localPosition = new Vector3(0, height / 2f, 0);
            }
        }

        private IEnumerator SpawnLogic()
        {
            const float safetyMargin = 0.9f;
            const float safetyMaxTimeForColliderGeneration = 5.0f;

            HeightMapSettings currentHeightSettings = ContinuousWorldSettings.Instance.HeightMapSettings;
            float unitSize = ContinuousWorldSettings.Instance.GetUnitSize();

            // We clamp the spawn radius to the SMALLER of Vision Limit or World Edge
            float maxSafeRadius = Mathf.Min(_visionRadius, _currentWorldExtent);
            float spawnRadius = maxSafeRadius * safetyMargin;

            // Generate Positions
            Vector2 goalCircle = GeneratePositionInsideBoundingCircle(spawnRadius);
            Vector3 newGoalPos = transform.position + new Vector3(goalCircle.x, 0f, goalCircle.y);

            Vector2 droneCircle = GeneratePositionInsideBoundingCircle(spawnRadius);

            // Pre-calculate rough drone height to avoid spawning inside a mountain before update
            Vector3 newDronePos = transform.position + new Vector3(
                droneCircle.x,
                currentHeightSettings.maxHeight + unitSize * 5f,
                droneCircle.y
            );

            // Teleport Agent & Force Update
            agent.SetTransformGlobalPosition(newDronePos);
            terrainGenerator.ForceUpdateNow();

            // Wait for Data (HeightMaps)
            while (!terrainGenerator.IsChunkLoadedAt(newGoalPos) || !terrainGenerator.IsChunkLoadedAt(newDronePos))
            {
                terrainGenerator.ForceUpdateNow();
                yield return null;
            }

            // Math-based Placement
            float goalGroundHeight = terrainGenerator.GetTerrainHeightAt(newGoalPos);
            goal.transform.position = new Vector3(newGoalPos.x, goalGroundHeight + goalSafetyHeight, newGoalPos.z);
            goal.transform.localScale = Vector3.one * unitSize;

            float droneGroundHeight = terrainGenerator.GetTerrainHeightAt(newDronePos);
            Vector3 finalSpawnPos = new Vector3(newDronePos.x, droneGroundHeight + agentSafetyHeight, newDronePos.z);
            agent.SetTransformGlobalPosition(finalSpawnPos);

            // Look at goal
            agent.transform.LookAt(goal.transform);

            // Wait for Physics (Safety)
            float timer = 0f;
            while (!terrainGenerator.HasColliderUnder(finalSpawnPos) && timer < safetyMaxTimeForColliderGeneration)
            {
                terrainGenerator.ForceUpdateNow();
                yield return null;
                timer += Time.deltaTime;
            }

            AgentEvents.EnvironmentReady(this.agent);
        }

        private static Vector2 GeneratePositionInsideBoundingCircle(float circleRadius)
        {
            return UnityEngine.Random.insideUnitCircle * circleRadius;
        }

        public Vector3 GetAreaCenteredPosition()
        {
            return transform.position;
        }

        public Vector3 GetAreaBoundsHalfSize()
        {
            return new Vector3(_currentWorldExtent, ContinuousWorldSettings.Instance.GetMaxPhysicalSize().y / 2f, _currentWorldExtent);
        }

        public void TriggerSuccess()
        {
            foreach (ColorFlashFeedback feedback in colorFlashFeedbacks)
            {
                feedback.FlashSuccess();
            }
        }

        public void TriggerFailure()
        {
            foreach (ColorFlashFeedback feedback in colorFlashFeedbacks)
            {
                feedback.FlashFailure();
            }
        }

        private void AgentListController_OnNewAgentSelected(IAgent newAgent)
        {
            if (newAgent is not ContinuousDroneAgent continuousAgent)
            {
                return;
            }
            UpdateCameraVisibility(newAgent);
            Debug.Log(continuousAgent);
        }

        private void OnEnable()
        {
            AgentListController.OnNewAgentSelected += AgentListController_OnNewAgentSelected;
            ContinuousSideChannel.OnRequestFullSnapshot += SendVisualObservation;
        }

        private void OnDisable()
        {
            AgentListController.OnNewAgentSelected -= AgentListController_OnNewAgentSelected;
            ContinuousSideChannel.OnRequestFullSnapshot -= SendVisualObservation;
        }

        private void SendVisualObservation()
        {
            var selected = AgentListController.Instance.CurrentSelectedAgent;
            if (selected is not ContinuousDroneAgent a || a.AgentId != this.agent.AgentId) return;

            if (ContinuousSideChannel.Instance != null && _camCapture != null)
            {
                Debug.Log("Selected is + " + selected);

                byte[] bytes = _camCapture.CaptureFrame();
                Debug.Log("frame captured");
                ContinuousSideChannel.Instance.SendView(ContinuousViewType.Trailing, bytes);
                Debug.Log("frame sent");
            }
        }

        public RenderTexture GetTrailingCameraTexture()
        {
            return _camCapture != null ? _camCapture.OutputTexture : null;
        } 

        private void UpdateCameraVisibility(IAgent selectedAgent)
        {
            bool isMeSelected = selectedAgent is ContinuousDroneAgent && selectedAgent.AgentId == this.agent.AgentId;
            if (_camObject != null)
            {
                _camObject.SetActive(isMeSelected);
            }
        }
    }
}
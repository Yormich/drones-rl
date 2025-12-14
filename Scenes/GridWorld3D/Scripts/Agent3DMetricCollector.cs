using System;
using System.Collections.Generic;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace GridWorld.Metrics
{
    [RequireComponent(typeof(Grid3DAgent))]
    public class Agent3DMetricCollector : MonoBehaviour
    {
        public event Action<Agent3DUiData> OnDataUpdated;

        [Header("Configuration")]
        [SerializeField] private int maxHistorySize = 1000;

        private Grid3DAgent _agent;
        private Agent3DUiData _currentData;
        private RayPerceptionSensorComponent3D[] _sensors;

        private readonly string[] _actionLabels = {
            "Stay", "Forward", "Back", "Right", "Left", "Up", "Down"
        };

        private void Awake()
        {
            _agent = GetComponent<Grid3DAgent>();
            // Cache sensors so we don't search for them every frame
            _sensors = GetComponentsInChildren<RayPerceptionSensorComponent3D>();

            _currentData = new Agent3DUiData
            {
                ActionHistory = new List<ActionHistoryEntry3D>(maxHistorySize)
            };
        }

        public void ResetHistory()
        {
            _currentData.ActionHistory.Clear();
            UpdateStats(0);
        }

        public void RegisterAction(int stepIndex, int actionIndex, Vector3Int prevPos, Vector3Int newPos, float stepReward)
        {
            string label = (actionIndex >= 0 && actionIndex < _actionLabels.Length)
                ? _actionLabels[actionIndex]
                : $"Unknown({actionIndex})";

            var entry = new ActionHistoryEntry3D
            {
                StepIndex = stepIndex,
                ActionLabel = label,
                FromPos = prevPos,
                ToPos = newPos,
                StepReward = stepReward
            };

            _currentData.ActionHistory.Insert(0, entry);
            if (_currentData.ActionHistory.Count > maxHistorySize)
            {
                _currentData.ActionHistory.RemoveAt(_currentData.ActionHistory.Count - 1);
            }
        }

        public void UpdateStats(int currentStep)
        {
            if (_agent == null || _agent.Area == null) return;

            // 1. Basic Stats
            GridArea3D area = _agent.Area;
            _currentData.EpisodeNumber = _agent.CompletedEpisodes;
            _currentData.StepCount = currentStep;
            _currentData.CumulativeReward = _agent.GetCumulativeReward();

            // ENVIRONMENT STATS
            _currentData.GridSize = area.CurrentEnvironmentSize;
            _currentData.DensityLevel = area.Density;
            _currentData.GenerationType = GridArea3D.GenerationType;

            // 2. RAYCAST PROCESSING
            // Reset all observations to "Clear" before processing
            _currentData.RayFront = "Clear";
            _currentData.RayBack = "Clear";
            _currentData.RayLeft = "Clear";
            _currentData.RayRight = "Clear";
            _currentData.RayUp = "Clear";
            _currentData.RayDown = "Clear";

            ProcessRaySensors();

            // 3. Normalized Distances
            Vector3Int agentPos = _agent.Area.LocalPositionToCellCoordinates(transform.localPosition);
            Vector3Int goalPos = area.GetGoalEnvironmentPosition();
            Vector3Int envSize = area.CurrentEnvironmentSize;

            float normX = (float)(envSize.x - 1);
            float normY = (float)(envSize.y - 1);
            float normZ = (float)(envSize.z - 1);

            _currentData.NormalizedDistanceX = (goalPos.x - agentPos.x) / (normX > 0 ? normX : 1f);
            _currentData.NormalizedDistanceY = (goalPos.y - agentPos.y) / (normY > 0 ? normY : 1f);
            _currentData.NormalizedDistanceZ = (goalPos.z - agentPos.z) / (normZ > 0 ? normZ : 1f);

            OnDataUpdated?.Invoke(_currentData);
        }

        private void ProcessRaySensors()
        {
            if (_sensors == null) return;

            foreach (var sensorComponent in _sensors)
            {
                var input = sensorComponent.GetRayPerceptionInput();

                var output = RayPerceptionSensor.Perceive(input, false);

                for (int i = 0; i < output.RayOutputs.Length; i++)
                {
                    var ray = output.RayOutputs[i];

                    if (!ray.HasHit || !ray.HitTaggedObject) continue;

                    Vector3 rayDirectionWorld = (input.RayExtents(i).EndPositionWorld - input.RayExtents(i).StartPositionWorld).normalized;
                    int tagIndex = ray.HitTagIndex;

                    if (tagIndex >= 0 && tagIndex < sensorComponent.DetectableTags.Count)
                    {
                        string hitTag = sensorComponent.DetectableTags[tagIndex];
                        float hitDistance = ray.HitFraction * sensorComponent.RayLength;
                        string infoString = $"{hitTag} ({hitDistance:F1}m)";

                        AssignObservationToDirection(rayDirectionWorld, infoString);
                    }
                }
            }
        }

        private void AssignObservationToDirection(Vector3 direction, string info)
        {
            // We compare the ray direction to global axes. 
            // 0.707 (45 degrees) is the threshold, but 0.5 is safer for loose alignments.
            float threshold = 0.5f;

            if (Vector3.Dot(direction, Vector3.forward) > threshold) _currentData.RayFront = info;
            else if (Vector3.Dot(direction, Vector3.back) > threshold) _currentData.RayBack = info;
            else if (Vector3.Dot(direction, Vector3.left) > threshold) _currentData.RayLeft = info;
            else if (Vector3.Dot(direction, Vector3.right) > threshold) _currentData.RayRight = info;
            else if (Vector3.Dot(direction, Vector3.up) > threshold) _currentData.RayUp = info;
            else if (Vector3.Dot(direction, Vector3.down) > threshold) _currentData.RayDown = info;
        }
    }
}
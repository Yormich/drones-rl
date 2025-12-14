using System;
using System.Collections.Generic;
using UnityEngine;

namespace GridWorld.Metrics
{
    public class Agent2DMetricCollector : MonoBehaviour
    {
        public event Action<AgentUIData> OnDataUpdated;

        [SerializeField] private int MaxHistorySize = 1000;

        private Grid2DAgent _agent;
        private AgentUIData _currentData;

        private readonly string[] _actionLabels = { "Stay", "Up", "Down", "Left", "Right" };
        private readonly Vector2Int[] _directions = new Vector2Int[]
        {
            new Vector2Int(0, 0), new Vector2Int(0, -1), new Vector2Int(0, 1), new Vector2Int(-1, 0), new Vector2Int(1, 0)
        };

        private void Awake()
        {
            _agent = GetComponent<Grid2DAgent>();
            _currentData = new AgentUIData();
            _currentData.ActionHistory = new List<ActionHistoryEntry>(MaxHistorySize);
        }

        public void ResetHistory()
        {
            _currentData.ActionHistory.Clear();
            UpdateStats(0);
        }

        public void RegisterAction(int stepIndex, int actionIndex, Vector2Int prevPos, Vector2Int newPos, float stepReward)
        {
            string label = (actionIndex >= 0 && actionIndex < _actionLabels.Length)
                ? _actionLabels[actionIndex]
                : "Unknown";

            var entry = new ActionHistoryEntry
            {
                StepIndex = stepIndex,
                ActionLabel = label,
                FromPos = prevPos,
                ToPos = newPos,
                StepReward = stepReward
            };

            _currentData.ActionHistory.Insert(0, entry);

            // Maintain max size (remove oldest)
            if (_currentData.ActionHistory.Count > MaxHistorySize)
            {
                _currentData.ActionHistory.RemoveAt(_currentData.ActionHistory.Count - 1);
            }
        }

        public void UpdateStats(int currentStep)
        {
            if (_agent == null || _agent.Area == null) return;

            _currentData.EpisodeNumber = _agent.CompletedEpisodes;
            _currentData.StepCount = currentStep;
            _currentData.CumulativeReward = _agent.GetCumulativeReward();
            _currentData.GridSize = _agent.Area.CurrentGridSize;

            // Recalculate Observations for UI
            _currentData.ObsNorth = GetObservationString(1);
            _currentData.ObsSouth = GetObservationString(2);
            _currentData.ObsWest = GetObservationString(3);
            _currentData.ObsEast = GetObservationString(4);

            Vector2Int goalPos = _agent.Area.GetGoalGridPosition();
            Vector2Int currentPos = _agent.Area.LocalPositionToGrid(transform.localPosition);
            float gridSize = _agent.Area.CurrentGridSize;

            _currentData.NormalizedDistanceX = (goalPos.x - currentPos.x) / (gridSize - 1.0f);
            _currentData.NormalizedDistanceY = (goalPos.y - currentPos.y) / (gridSize - 1.0f);

            OnDataUpdated?.Invoke(_currentData);
        }

        private string GetObservationString(int directionIndex)
        {
            Vector2Int currentPos = _agent.Area.LocalPositionToGrid(transform.localPosition);
            Vector2Int targetPos = currentPos + _directions[directionIndex];
            bool blocked = !_agent.Area.IsPositionFree(targetPos);
            return blocked ? "Blocked" : "Free";
        }
    }
}
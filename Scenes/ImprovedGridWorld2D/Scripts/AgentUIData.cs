using System.Collections.Generic;
using UnityEngine;

namespace GridWorld.Metrics
{
    [System.Serializable]
    public struct ActionHistoryEntry
    {
        public int StepIndex;
        public string ActionLabel;
        public Vector2Int FromPos;
        public Vector2Int ToPos;
        public float StepReward; // The reward gained just in this specific step
    }

    [System.Serializable]
    public class AgentUIData
    {
        public int EpisodeNumber;
        public int StepCount;
        public float CumulativeReward;

        // Environment & Observations
        public float GridSize;
        public int NumObstacles;
        public string ObsNorth;
        public string ObsSouth;
        public string ObsWest;
        public string ObsEast;
        public float NormalizedDistanceX;
        public float NormalizedDistanceY;

        public List<ActionHistoryEntry> ActionHistory = new List<ActionHistoryEntry>();
    }
}
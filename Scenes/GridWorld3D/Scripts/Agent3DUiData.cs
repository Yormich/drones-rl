using System.Collections.Generic;
using UnityEngine;

namespace GridWorld.Metrics
{
    public class Agent3DUiData
    {
        public int EpisodeNumber;
        public int StepCount;
        public float CumulativeReward;

        public Vector3Int GridSize;
        public float DensityLevel;
        public MapGenerationType GenerationType;

        // RAYCAST OBSERVATIONS
        // Instead of "Free/Blocked", these will now say "Wall (2.5m)" or "Clear"
        public string RayFront;
        public string RayBack;
        public string RayLeft;
        public string RayRight;
        public string RayUp;
        public string RayDown;

        // Normalized Distances
        public float NormalizedDistanceX;
        public float NormalizedDistanceY;
        public float NormalizedDistanceZ;

        public List<ActionHistoryEntry3D> ActionHistory;
    }

    public struct ActionHistoryEntry3D
    {
        public int StepIndex;
        public string ActionLabel;
        public Vector3Int FromPos;
        public Vector3Int ToPos;
        public float StepReward;
    }
}
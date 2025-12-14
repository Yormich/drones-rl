using GridWorld.Generation;
using System;
using Unity.MLAgents;
using UnityEngine;

namespace GridWorld 
{
    [DefaultExecutionOrder(-100)]
    public class Grid2DSettings : SettingsBase
    {
        public static Grid2DSettings Instance;
        
        [Header("Defaults")]
        [SerializeField] float defaultGridSize;
        [SerializeField] float unitSize;
        [SerializeField] int defaultNumObstacles;
        [SerializeField] float maxScaleOfObstacles;

        [Header("Academy Keys")]
        [SerializeField] string gridSizeKey;
        [SerializeField] string numObstaclesKey;

        [Header("Orchestrator Limits")]
        [Tooltip("The absolute maximum size this grid will ever reach in Curriculum")]
        [SerializeField] private Vector3Int absoluteMaxGridSize = new Vector3Int(256, 100, 256);

        public void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            this.defaultGridSize = Academy.Instance.EnvironmentParameters.GetWithDefault(this.gridSizeKey, this.defaultGridSize);
            float obstaclesRaw = Mathf.Ceil(Academy.Instance.EnvironmentParameters.GetWithDefault(this.numObstaclesKey, this.defaultNumObstacles));

            const float minObstaclesAmount = 0.0f;
            this.defaultNumObstacles = Mathf.FloorToInt(Mathf.Clamp(obstaclesRaw, minObstaclesAmount, Mathf.Pow(this.defaultGridSize, 2f) * this.maxScaleOfObstacles));
        }

        public float GetActiveGridSize()
        {
            return Academy.Instance.EnvironmentParameters.GetWithDefault(this.gridSizeKey, this.defaultGridSize);
        }

        public int GetActiveNumObstacles(float currentGridSize)
        {
            float obstaclesRaw = Academy.Instance.EnvironmentParameters.GetWithDefault(this.numObstaclesKey, this.defaultNumObstacles);

            float maxObstacles = Mathf.Pow(currentGridSize, 2f) * this.maxScaleOfObstacles;

            return Mathf.FloorToInt(Mathf.Clamp(obstaclesRaw, 0f, maxObstacles));
        }

        public override Vector3 GetMaxPhysicalSize()
        {
            return (Vector3)absoluteMaxGridSize * this.unitSize;
        }

        public override float GetUnitSize()
        {
            return this.unitSize;
        }
    }
}
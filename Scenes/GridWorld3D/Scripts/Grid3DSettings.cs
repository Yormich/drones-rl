using GridWorld.Generation;
using System;
using Unity.MLAgents;
using UnityEngine;


namespace GridWorld
{
    public enum MapGenerationType
    {
        Random = 0,
        Cityscape = 1,
        CellularAutomata = 2,
        Simplex = 3,
        Maze = 4
    }


    [DefaultExecutionOrder(-100)]
    public class Grid3DSettings : SettingsBase
    {
        public static Grid3DSettings Instance;

        [Header("Defaults")]
        [Tooltip("Size of one side of the cube (e.g., 10 means 10x10x10)")]
        [SerializeField] private float defaultGridSize = 10f;
        [SerializeField] private float unitSize = 1f;

        [Range(0f, 1f)]
        [SerializeField] private float defaultDensity = 0.2f;

        [SerializeField] MapGenerationType defaultMapType = MapGenerationType.Random;

        [Header("Orchestrator Limits")]
        [Tooltip("The absolute maximum size this grid will ever reach in Curriculum")]
        [SerializeField] private Vector3Int absoluteMaxGridSize = new Vector3Int(64, 64, 64);

        [Header("Academy Keys (Must match YAML)")]
        [SerializeField] private string gridSizeKey = "grid_size";
        [SerializeField] private string densityKey = "density";
        [SerializeField] private string mapTypeKey = "map_type";

        public float UnitSize => this.unitSize;

        public void Awake()
        {
            if (Instance == null) Instance = this;
        }

        public Vector3Int GetActiveGridSize()
        {
            float size = Academy.Instance.EnvironmentParameters.GetWithDefault(this.gridSizeKey, this.defaultGridSize);
            int s = Mathf.FloorToInt(size);

            const int minPossibleSize = 3;
            s = Mathf.Max(minPossibleSize, s);

            return new Vector3Int(s, s, s);
        }

        public float GetActiveDensity()
        {
            float val = Academy.Instance.EnvironmentParameters.GetWithDefault(this.densityKey, this.defaultDensity);
            return Mathf.Clamp01(val);
        }

        public MapGenerationType GetActiveGenerationType()
        {
            float val = Academy.Instance.EnvironmentParameters.GetWithDefault(this.mapTypeKey, (float)this.defaultMapType);

            // Cast float to Int, then to Enum
            int typeIndex = Mathf.RoundToInt(val);

            if (Enum.IsDefined(typeof(MapGenerationType), typeIndex))
            {
                return (MapGenerationType)typeIndex;
            }

            return MapGenerationType.Random;
        }

        public override Vector3 GetMaxPhysicalSize()
        {
            return (Vector3)absoluteMaxGridSize * unitSize;
        }

        public override float GetUnitSize()
        {
            return this.unitSize;
        }
    }
}
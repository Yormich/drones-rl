using GridWorld.Generation;
using Unity.MLAgents;
using UnityEngine;

namespace ContinuousWorld
{
    [DefaultExecutionOrder(-100)]
    public class ContinuousWorldSettings : SettingsBase
    {
        public static ContinuousWorldSettings Instance;

        [Header("Data References")]
        [SerializeField] private MeshSettings meshSettings;
        [SerializeField] private HeightMapSettings heightMapSettings;
        [SerializeField] private TextureData textureSettings;

        [Header("Training Area Configuration")]
        [Tooltip("Max radius in chunks expected during training (for spacing calculation).")]
        [SerializeField] private int maxCurriculumChunkRadius = 10;

        [Range(0, 8)]
        [Tooltip("Chunk size index related to the mesh configuration of specific terrain chunk. Bigger index = bigger size")]
        [SerializeField] private int chunkSizeIndex = 8;

        [Tooltip("How many chunks to generate in each direction from center. 0 = 1x1, 1 = 3x3, 2 = 5x5.")]
        [SerializeField] private int trainingChunkRadius = 1;

        [Header("Global Generation Settings")]
        [Tooltip("Seed for the noise generator")]
        [SerializeField] private int globalSeed = 42;

        [Header("Physics Settings")]
        [SerializeField] private float dragCoefficient = 0.5f;

        [Header("Academy Keys")]
        [SerializeField] private string chunkSizeIndexKey = "chunk_size_index";
        [SerializeField] private string chunkRadiusKey = "chunk_radius";
        [SerializeField] private string seedKey = "terrain_seed";
        [SerializeField] private string dragKey = "drone_drag";

        private int _previousGlobalSeed;
        private int _previousChunkSizeIndex;

        private void Awake()
        {
            if (Instance == null) Instance = this;

            _previousGlobalSeed = HeightMapSettings.noiseSettings.seed;
            _previousChunkSizeIndex = meshSettings.chunkSizeIndex;
        }

        public MeshSettings MeshSettings => meshSettings;

        public HeightMapSettings HeightMapSettings => heightMapSettings;

        public TextureData TextureSettings => textureSettings;

        public void UpdateActiveSeed()
        {
            int activeSeed = (int)Academy.Instance.EnvironmentParameters.GetWithDefault(this.seedKey, this.globalSeed);

            HeightMapSettings.noiseSettings.seed = activeSeed;
        }

        public void UpdateActiveChunkSizeIndex()
        {
            int activeChunkSizeIndex = (int)Academy.Instance.EnvironmentParameters.GetWithDefault(this.chunkSizeIndexKey, this.chunkSizeIndex);

            meshSettings.chunkSizeIndex = Mathf.Clamp(activeChunkSizeIndex, 0, 8);
        }

        public float GetActiveDragOverride()
        {
            float activeDragCoefficient = Academy.Instance.EnvironmentParameters.GetWithDefault(this.dragKey, dragCoefficient);

            return activeDragCoefficient;
        }

        public int GetActiveChunkRadius()
        {
            int chunksRadius = (int)Academy.Instance.EnvironmentParameters.GetWithDefault(this.chunkRadiusKey, this.trainingChunkRadius);
            return chunksRadius;
        }

        public override Vector3 GetMaxPhysicalSize()
        {
            if (meshSettings == null) return new Vector3(100, 50, 100);

            const float breathingRoomFactor = 1.5f;

            float totalChunksEdge = (maxCurriculumChunkRadius * 2) + 1;
            float worldWidth = totalChunksEdge * meshSettings.MeshWorldSize;

            // add breathing room in height dimension
            return new Vector3(worldWidth, heightMapSettings.maxHeight * breathingRoomFactor, worldWidth);
        }

        public override float GetUnitSize()
        {
            return meshSettings.meshScale;
        }

        private void OnDestroy()
        {
            HeightMapSettings.noiseSettings.seed = _previousGlobalSeed;
            meshSettings.chunkSizeIndex = _previousChunkSizeIndex;
        }
    }

}
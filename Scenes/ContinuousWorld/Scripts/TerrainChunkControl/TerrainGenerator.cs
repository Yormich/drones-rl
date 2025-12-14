using UnityEngine;
using System.Collections.Generic;


namespace ContinuousWorld
{
    [System.Serializable]
    public struct LodInfo
    {
        [Range(0, MeshSettings.numSupportedLODs - 1)]
        public int lod;
        public float visibleDstThreshold;

        public float squareVisibleDistanceThreshold
        {
            get
            {
                return visibleDstThreshold * visibleDstThreshold;
            }
        }
    }

    public class TerrainGenerator : MonoBehaviour
    {
        [Header("Update Thresholds")]
        [Tooltip("How far the player must move before we calculate new chunks.")]
        private const float viewerMoveThresholdForChunkUpdate = 25f;
        private const float sqrViewerMoveThresholdForChunkUpdate = viewerMoveThresholdForChunkUpdate * viewerMoveThresholdForChunkUpdate;

        [Tooltip("How far past the visible distance we keep chunks in memory.")]
        private const float chunkDestroyOffset = 50f;

        [Header("Physics")]
        [SerializeField] private  int colliderLODIndex;
        [SerializeField] private float colliderGenerationDistanceThreshold = 5f;

        [Header("World Data")]
        [SerializeField] private LodInfo[] detailLevels;
        
        private MeshSettings meshSettings;
        private HeightMapSettings heightMapSettings;

        [Header("References")]
        [SerializeField] private Transform viewer;
        [SerializeField] private Material mapMaterial;

        [Header("State")]
        private Vector2 viewerPosition;
        private Vector2 viewerPositionOld;
        private int chunksVisibleInViewDst;

        // We use a Dictionary for fast lookups by Coordinate
        private readonly Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new();
        // We use a List for fast iteration of visible objects (for physics updates)
        private readonly List<TerrainChunk> visibleTerrainChunks = new();


        public LodInfo[] DetailLevels => this.detailLevels;

        private void Start()
        {
            var settings = ContinuousWorldSettings.Instance;
            this.meshSettings = settings.MeshSettings;
            this.heightMapSettings = settings.HeightMapSettings;


            this.mapMaterial = new Material(mapMaterial);

            float maxViewDst = detailLevels[detailLevels.Length - 1].visibleDstThreshold;
            chunksVisibleInViewDst = Mathf.RoundToInt(maxViewDst / meshSettings.MeshWorldSize);

            settings.TextureSettings.ApplyToMaterial(mapMaterial);
            settings.TextureSettings.UpdateMeshHeights(mapMaterial, heightMapSettings.minHeight, heightMapSettings.maxHeight);

            UpdateVisibleChunks();
        }

        private void Update()
        {
            viewerPosition = new Vector2(viewer.position.x, viewer.position.z);

            // 1. Physics Update (Frequent)
            if (viewerPosition != viewerPositionOld)
            {
                foreach (TerrainChunk chunk in visibleTerrainChunks)
                {
                    chunk.UpdateCollisionMesh(viewerPosition);
                }
            }

            // 2. Chunk Logic Update (Infrequent)
            if ((viewerPositionOld - viewerPosition).sqrMagnitude > sqrViewerMoveThresholdForChunkUpdate)
            {
                viewerPositionOld = viewerPosition;
                UpdateVisibleChunks();
            }
        }

        public void ResetTerrain()
        {
            foreach (var chunk in terrainChunkDictionary.Values)
            {
                chunk.Retire();
            }
            terrainChunkDictionary.Clear();
            visibleTerrainChunks.Clear();

            viewerPositionOld = new Vector2(float.MaxValue, float.MaxValue);
        }
        public void ForceUpdateNow()
        {
            // Force the update logic to run immediately (useful after teleporting viewer)
            Update();
        }

        private TerrainChunkSettings TerrainChunkSettings => new TerrainChunkSettings
        {
            heightMapSettings = heightMapSettings,
            meshSettings = meshSettings,
            detailLevels = detailLevels,
            colliderLODIndex = colliderLODIndex,
            material = mapMaterial,
            parent = transform,
            colliderGenerationDistanceThreshold = colliderGenerationDistanceThreshold
        };

        private void UpdateVisibleChunks()
        {
            TerrainChunkSettings chunkSettings = TerrainChunkSettings;

            RunGarbageCollection();


            HashSet<Vector2> alreadyUpdatedChunkCoords = new HashSet<Vector2>();
            for (int i = visibleTerrainChunks.Count - 1; i >= 0; i--)
            {
                TerrainChunk chunk = visibleTerrainChunks[i];
                alreadyUpdatedChunkCoords.Add(chunk.ChunkCoordinate);
                chunk.UpdateTerrainChunk(viewerPosition);
            }

            Vector2 chunkCoord = GetChunkCoordinate(viewerPosition.x, viewerPosition.y);
            int currentChunkCoordX = (int)chunkCoord.x;
            int currentChunkCoordY = (int)chunkCoord.y;


            for (int yOffset = -chunksVisibleInViewDst; yOffset <= chunksVisibleInViewDst; yOffset++)
            {
                for (int xOffset = -chunksVisibleInViewDst; xOffset <= chunksVisibleInViewDst; xOffset++)
                {
                    Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

                    if (!alreadyUpdatedChunkCoords.Contains(viewedChunkCoord))
                    {
                        if (terrainChunkDictionary.ContainsKey(viewedChunkCoord))
                        {
                            // Chunk exists but was not visible (in memory), update it
                            terrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk(viewerPosition);
                        }
                        else
                        {
                            // Chunk does not exist, spawn it
                            TerrainChunk newChunk = new TerrainChunk(viewedChunkCoord, chunkSettings, viewerPosition);
                            terrainChunkDictionary.Add(viewedChunkCoord, newChunk);
                            newChunk.OnVisibilityChanged += OnVisibilityChanged;
                            newChunk.Load();
                        }
                    }
                }
            }
        }

        private void OnVisibilityChanged(TerrainChunk terrainChunk, bool isVisible)
        {
            if (isVisible)
            {
                visibleTerrainChunks.Add(terrainChunk);
            }
            else
            {
                visibleTerrainChunks.Remove(terrainChunk);
            }
        }

        private void RunGarbageCollection()
        {
            // Calculate destroy distance (Max View Dist + Buffer)
            float maxViewDst = detailLevels[detailLevels.Length - 1].visibleDstThreshold;
            float destroyDst = maxViewDst + chunkDestroyOffset;
            float sqrDestroyDst = destroyDst * destroyDst;

            // Find chunks to remove
            List<Vector2> chunksToRemove = new List<Vector2>();

            foreach (KeyValuePair<Vector2, TerrainChunk> item in terrainChunkDictionary)
            {
                Vector2 chunkCenter = item.Key * this.meshSettings.MeshWorldSize;

                if ((viewerPosition - chunkCenter).sqrMagnitude > sqrDestroyDst)
                {
                    chunksToRemove.Add(item.Key);
                }
            }

            foreach (Vector2 coord in chunksToRemove)
            {
                TerrainChunk chunk = terrainChunkDictionary[coord];

                // Ensure it's removed from the visible list if it happened to be there
                if (visibleTerrainChunks.Contains(chunk))
                {
                    visibleTerrainChunks.Remove(chunk);
                }

                // Call cleanup on the chunk itself
                chunk.Retire();

                // Remove from dictionary
                terrainChunkDictionary.Remove(coord);
            }
        }

        public float GetTerrainHeightAt(Vector3 worldPosition)
        {
            Vector2 coord = GetChunkCoordinate(worldPosition.x, worldPosition.z);

            // Find the chunk
            if (terrainChunkDictionary.TryGetValue(coord, out TerrainChunk chunk))
            {
                // Ask chunk for precise height
                return chunk.GetHeightAtPosition(worldPosition);
            }

            Debug.LogWarning($"Requested height at {worldPosition} but chunk {coord} is not loaded.");
            return 0f;
        }

        public bool IsChunkLoadedAt(Vector3 worldPosition)
        {
            Vector2 coord = GetChunkCoordinate(worldPosition.x, worldPosition.z);
            return terrainChunkDictionary.TryGetValue(coord, out TerrainChunk chunk) && chunk.HasHeightMap;
        }

        private Vector2 GetChunkCoordinate(float x, float z)
        {
            float meshSize = meshSettings.MeshWorldSize;
            return new Vector2(
                Mathf.RoundToInt(x / meshSize),
                Mathf.RoundToInt(z / meshSize)
            );
        }

        public void SetColliderGenerationDistanceThreshold(float distance)
        {
            this.colliderGenerationDistanceThreshold = distance;
        }

        public void SetActiveViewer(Transform viewer)
        {
            this.viewer = viewer;
        }

        // Helper to check if the chunk under this position has a generated mesh collider
        public bool HasColliderUnder(Vector3 worldPosition)
        {
            Vector2 coord = GetChunkCoordinate(worldPosition.x, worldPosition.z);

            if (terrainChunkDictionary.TryGetValue(coord, out TerrainChunk chunk))
            {
                return chunk.HasCollider;
            }
            return false;
        }
    }
}
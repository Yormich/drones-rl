using System.Threading.Tasks;
using UnityEngine;

namespace ContinuousWorld
{
    public class TerrainChunk
    {
        public event System.Action<TerrainChunk, bool> OnVisibilityChanged;
        public Vector2 ChunkCoordinate { get; private set; }
        
        // Clean State Groups
        private readonly TerrainChunkSettings settings;
        private readonly TerrainChunkView view;
        private LodMesh[] lodMeshes;
        private readonly Vector2 sampleCenter;
        private readonly Bounds bounds;

        // Dynamic State
        private HeightMap heightMap;
        private bool heightMapReceived;

        private Vector2 lastViewerPosition;
        private bool hasReceivedViewerPosition;

        private int previousLODIndex = -1;
        private bool hasSetCollider;

        public TerrainChunk(Vector2 coord, TerrainChunkSettings settings, Vector2 initialViewerPosition)
        {
            this.ChunkCoordinate = coord;
            this.settings = settings;

            this.lastViewerPosition = initialViewerPosition;
            this.hasReceivedViewerPosition = true;

            // Calculate Position
            Vector2 position = coord * settings.meshSettings.MeshWorldSize;
            sampleCenter = coord * settings.meshSettings.MeshWorldSize / settings.meshSettings.meshScale;
            bounds = new Bounds(position, Vector2.one * settings.meshSettings.MeshWorldSize);

            // Initialize View (Unity Components)
            view = new TerrainChunkView(position, settings.material, settings.parent);

            // Initialize LOD Logic
            lodMeshes = new LodMesh[settings.detailLevels.Length];
            for (int i = 0; i < settings.detailLevels.Length; i++)
            {
                lodMeshes[i] = new LodMesh(settings.detailLevels[i].lod);

                lodMeshes[i].OnMeshDataReceived += OnLodMeshReceived;

                if (i == settings.colliderLODIndex)
                {
                    lodMeshes[i].OnMeshDataReceived += OnCollisionMeshReceived;
                }
            }
        }


        public void Load() => _ = LoadHeightMapAsync();

        private async Task LoadHeightMapAsync()
        {
            heightMap = await Task.Run(() => HeightMapGenerator.GenerateHeightMap(
                settings.meshSettings.NumVerticesPerLine,
                settings.meshSettings.NumVerticesPerLine,
                settings.heightMapSettings,
                sampleCenter
            ));

            heightMapReceived = true;

            if (hasReceivedViewerPosition)
            {
                UpdateTerrainChunk(lastViewerPosition);
            }
        }

        public void UpdateTerrainChunk(Vector2 viewerPosition)
        {
            lastViewerPosition = viewerPosition;
            hasReceivedViewerPosition = true;

            if (!heightMapReceived) return;

            float viewerDstFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(viewerPosition));
            bool visible = viewerDstFromNearestEdge <= settings.MaxViewDst;

            if (visible)
            {
                int lodIndex = GetLodIndex(viewerDstFromNearestEdge);
                if (lodIndex != previousLODIndex)
                {
                    LodMesh lodMesh = lodMeshes[lodIndex];
                    if (lodMesh.HasMesh)
                    {
                        previousLODIndex = lodIndex;
                        view.SetMesh(lodMesh.LevelOfDetailMesh);
                    }
                    else if (!lodMesh.HasRequestedMesh)
                    {
                        lodMesh.RequestMesh(heightMap, settings.meshSettings);
                    }
                }
            }

            if (visible != view.IsActive)
            {
                view.SetActive(visible);
                OnVisibilityChanged?.Invoke(this, visible);
            }
        }

        public void UpdateCollisionMesh(Vector2 viewerPosition)
        {
            if (hasSetCollider) return;

            float sqrDstToViewer = bounds.SqrDistance(viewerPosition);
            LodInfo colliderLod = settings.detailLevels[settings.colliderLODIndex];

            // 1. Request Collision Mesh if near
            if (sqrDstToViewer < colliderLod.squareVisibleDistanceThreshold)
            {
                LodMesh collisionLodMesh = lodMeshes[settings.colliderLODIndex];
                if (!collisionLodMesh.HasRequestedMesh)
                    collisionLodMesh.RequestMesh(heightMap, settings.meshSettings);
            }

            if (sqrDstToViewer < settings.SqrColliderGenerationThreshold)
            {
                LodMesh collisionLodMesh = lodMeshes[settings.colliderLODIndex];
                if (collisionLodMesh.HasMesh)
                {
                    view.SetCollisionMesh(collisionLodMesh.LevelOfDetailMesh);
                    hasSetCollider = true;
                }
            }
        }

        private void OnLodMeshReceived()
        {
            if (hasReceivedViewerPosition)
            {
                UpdateTerrainChunk(lastViewerPosition);
            }
        }

        private void OnCollisionMeshReceived()
        {
            if (hasReceivedViewerPosition)
            {
                UpdateCollisionMesh(lastViewerPosition);
            }
        }

        private int GetLodIndex(float dist)
        {
            for (int i = 0; i < settings.detailLevels.Length - 1; i++)
            {
                if (dist < settings.detailLevels[i].visibleDstThreshold)
                {
                    return i;
                }
            }

            return settings.detailLevels.Length - 1;
        }

        public void SetVisible(bool visible) => view.SetActive(visible);
        public bool IsVisible() => view.IsActive;

        public void Retire()
        {
            if (view != null) Object.Destroy(view.GameObject);

            for (int i = 0; i < lodMeshes.Length; i++)
            {
                lodMeshes[i].OnMeshDataReceived -= OnLodMeshReceived;
                if (i == settings.colliderLODIndex)
                    lodMeshes[i].OnMeshDataReceived -= OnCollisionMeshReceived;
            }

            lodMeshes = null;
            OnVisibilityChanged = null;
        }

        public bool HasHeightMap => heightMapReceived;
        public bool HasCollider => hasSetCollider;


        // Bilinear interpolation to get height at world position within this chunk
        public float GetHeightAtPosition(Vector3 worldPosition)
        {
            if (!heightMapReceived) return 0f;

            // The chunk is centered at its coordinate position
            Vector2 chunkWorldCenter = ChunkCoordinate * settings.meshSettings.MeshWorldSize;

            // get local position of the world position relative to chunk center
            float localX = worldPosition.x - chunkWorldCenter.x;
            float localZ = worldPosition.z - chunkWorldCenter.y;

            // In MeshBuilder, top-left is (-size/2, size/2). 
            float meshSize = settings.meshSettings.MeshWorldSize;
            float halfSize = meshSize / 2f;

            // Calculate 0..1 percentage across the mesh
            // x: -half -> +half maps to 0 -> 1
            // z: +half -> -half maps to 0 -> 1
            float pctX = (localX + halfSize) / meshSize;
            float pctY = (halfSize - localZ) / meshSize;

            int verticesPerLine = settings.meshSettings.NumVerticesPerLine;

            // The valid mesh area is inside the padding (index 1 to Max-1)
            // MeshBuilder: percent * (numVerticesPerLine - 3)
            float gridIndexX = pctX * (verticesPerLine - 3) + 1;
            float gridIndexY = pctY * (verticesPerLine - 3) + 1;

            // Bilinear Interpolation
            int xFloor = Mathf.FloorToInt(gridIndexX);
            int xCeil = Mathf.CeilToInt(gridIndexX);
            int yFloor = Mathf.FloorToInt(gridIndexY);
            int yCeil = Mathf.CeilToInt(gridIndexY);

            // Safety Clamp
            xFloor = Mathf.Clamp(xFloor, 0, verticesPerLine - 1);
            xCeil = Mathf.Clamp(xCeil, 0, verticesPerLine - 1);
            yFloor = Mathf.Clamp(yFloor, 0, verticesPerLine - 1);
            yCeil = Mathf.Clamp(yCeil, 0, verticesPerLine - 1);

            // Get 4 heights
            float h00 = heightMap.values[xFloor, yFloor];
            float h10 = heightMap.values[xCeil, yFloor];
            float h01 = heightMap.values[xFloor, yCeil];
            float h11 = heightMap.values[xCeil, yCeil];

            // Interpolate
            float tX = gridIndexX - xFloor;
            float tY = gridIndexY - yFloor;

            float lerpTop = Mathf.Lerp(h00, h10, tX);
            float lerpBot = Mathf.Lerp(h01, h11, tX);

            return Mathf.Lerp(lerpTop, lerpBot, tY);
        }
    }
}
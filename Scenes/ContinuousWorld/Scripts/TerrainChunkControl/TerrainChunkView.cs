using UnityEngine;

namespace ContinuousWorld
{
    [System.Serializable]
    public struct TerrainChunkSettings
    {
        public HeightMapSettings heightMapSettings;
        public MeshSettings meshSettings;
        public LodInfo[] detailLevels;
        public int colliderLODIndex;
        public Material material;
        public Transform parent;

        public float colliderGenerationDistanceThreshold;

        public readonly float SqrColliderGenerationThreshold => colliderGenerationDistanceThreshold * colliderGenerationDistanceThreshold;

        public readonly float MaxViewDst => detailLevels[detailLevels.Length - 1].visibleDstThreshold;
    }

    public class TerrainChunkView
    {
        private readonly GameObject meshObject;
        private readonly MeshRenderer meshRenderer;
        private readonly MeshFilter meshFilter;
        private readonly MeshCollider meshCollider;

        public TerrainChunkView(Vector2 position, Material material, Transform parent)
        {
            meshObject = new GameObject("Terrain Chunk");
            meshObject.tag = "Obstacle";

            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshCollider = meshObject.AddComponent<MeshCollider>();

            meshRenderer.material = material;
            meshObject.transform.position = new Vector3(position.x, 0, position.y);
            meshObject.transform.parent = parent;

            SetActive(false);
        }

        public void SetMesh(Mesh mesh) => meshFilter.mesh = mesh;

        public void SetCollisionMesh(Mesh mesh) => meshCollider.sharedMesh = mesh;

        public void SetActive(bool active) => meshObject.SetActive(active);

        public bool IsActive => meshObject.activeSelf;

        public GameObject GameObject => meshObject;
    }
}
using UnityEngine;
using System.Threading.Tasks;

namespace ContinuousWorld
{
    public class LodMesh
    {                
        private readonly int lod;

        public event System.Action OnMeshDataReceived;

        public LodMesh(int lod)
        {
            this.lod = lod;
        }

        public bool HasMesh { get; private set; }
        public bool HasRequestedMesh { get; private set; }

        public Mesh LevelOfDetailMesh { get; private set; }

        public void RequestMesh(HeightMap heightMap, MeshSettings meshSettings)
        {
            if (HasRequestedMesh) return;

            HasRequestedMesh = true;
            _ = GenerateMeshAsync(heightMap, meshSettings);
        }

        private async Task GenerateMeshAsync(HeightMap heightMap, MeshSettings meshSettings)
        {
            MeshData data = await Task.Run(() => MeshGenerator.GenerateTerrainMesh(
                heightMap.values,
                lod,
                meshSettings
            ));

            LevelOfDetailMesh = data.CreateMesh();
            HasMesh = true;

            OnMeshDataReceived?.Invoke();
        }
    }
}

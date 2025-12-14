using UnityEngine;
using System.Collections;

namespace ContinuousWorld
{
    public class MapPreview : MonoBehaviour
    {
        public enum DrawMode 
        { 
            NoiseMap, 
            Mesh, 
            FalloffMap 
        };



        [SerializeField] private Renderer textureRender;
        [SerializeField] private MeshFilter meshFilter;

        [SerializeField] private DrawMode drawMode;

        [SerializeField] private MeshSettings meshSettings;
        [SerializeField] private HeightMapSettings heightMapSettings;
        [SerializeField] private TextureData textureData;

        [SerializeField] private Material terrainMaterial;

        [Range(0, MeshSettings.numSupportedLODs - 1)]
        [SerializeField] private int editorPreviewLOD;
        [SerializeField] private bool autoUpdate;

        public bool IsAutoUpdate => autoUpdate;

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (meshSettings != null) meshSettings.OnValuesUpdated += OnValuesUpdated;
            if (heightMapSettings != null) heightMapSettings.OnValuesUpdated += OnValuesUpdated;
            if (textureData != null) textureData.OnValuesUpdated += OnTextureValuesUpdated;
        }

        private void Unsubscribe()
        {
            if (meshSettings != null) meshSettings.OnValuesUpdated -= OnValuesUpdated;
            if (heightMapSettings != null) heightMapSettings.OnValuesUpdated -= OnValuesUpdated;
            if (textureData != null) textureData.OnValuesUpdated -= OnTextureValuesUpdated;
        }

        private void OnValidate()
        {
            if (meshSettings != null)
            {
                Unsubscribe();
                Subscribe();
            }

            if (autoUpdate)
            {
                DrawMapInEditor();
            }
        }

        public void DrawMapInEditor()
        {
            if (meshSettings == null || heightMapSettings == null || textureData == null) return;

            textureData.ApplyToMaterial(terrainMaterial);
            textureData.UpdateMeshHeights(terrainMaterial, heightMapSettings.minHeight, heightMapSettings.maxHeight);

            HeightMap heightMap = HeightMapGenerator.GenerateHeightMap(
                meshSettings.NumVerticesPerLine,
                meshSettings.NumVerticesPerLine,
                heightMapSettings,
                Vector2.zero
            );

            switch (drawMode)
            {
                case DrawMode.NoiseMap:
                    DrawTexture(TextureGenerator.TextureFromHeightMap(heightMap));
                    break;

                case DrawMode.Mesh:
                    DrawMesh(MeshGenerator.GenerateTerrainMesh(heightMap.values, editorPreviewLOD, meshSettings));
                    break;

                case DrawMode.FalloffMap:
                    DrawTexture(TextureGenerator.TextureFromHeightMap(
                        new HeightMap(FalloffGenerator.GenerateFalloffMap(meshSettings.NumVerticesPerLine), 0, 1f)
                    ));
                    break;
            }
        }

        private void DrawTexture(Texture2D texture)
        {
            textureRender.sharedMaterial.mainTexture = texture;
            textureRender.transform.localScale = new Vector3(texture.width, 1, texture.height) / 10f;

            textureRender.gameObject.SetActive(true);
            meshFilter.gameObject.SetActive(false);
        }

        private void DrawMesh(MeshData meshData)
        {
            meshFilter.sharedMesh = meshData.CreateMesh();

            textureRender.gameObject.SetActive(false);
            meshFilter.gameObject.SetActive(true);
        }

        void OnValuesUpdated()
        {
            if (!Application.isPlaying && autoUpdate)
            {
                DrawMapInEditor();
            }
        }

        void OnTextureValuesUpdated()
        {
            textureData.ApplyToMaterial(terrainMaterial);
        }
    }
}
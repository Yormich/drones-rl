using UnityEngine;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ContinuousWorld
{

    [CreateAssetMenu(fileName = "TextureData", menuName = "Terrain Generation/TextureData")]
    public class TextureData : UpdatableData
    {
        const int textureSize = 512;
        const TextureFormat textureFormat = TextureFormat.RGB565;

        public Layer[] layers;

        float savedMinHeight;
        float savedMaxHeight;

        public void ApplyToMaterial(Material material)
        {
            UpdateMeshHeights(material, savedMinHeight, savedMaxHeight);
            material.SetInt("_LayerCount", layers.Length);

            Texture2DArray texturesArray = GenerateTextureArray(layers.Select(x => x.texture).ToArray());
            material.SetTexture("_TextureArray", texturesArray);

            Texture2D dataTex = new Texture2D(layers.Length, 2, TextureFormat.RGBAFloat, false);
            dataTex.filterMode = FilterMode.Point;
            dataTex.wrapMode = TextureWrapMode.Clamp;

            for (int i = 0; i < layers.Length; i++)
            {
                Layer l = layers[i];
                dataTex.SetPixel(i, 0, l.tint);

                dataTex.SetPixel(i, 1, new Color(l.startHeight, l.blendStrength, l.textureScale, l.tintStrength));
            }
            dataTex.Apply();
            material.SetTexture("_PerLayerData", dataTex);
        }

        public void UpdateMeshHeights(Material material, float minHeight, float maxHeight)
        {
            savedMinHeight = minHeight;
            savedMaxHeight = maxHeight;

            material.SetFloat("_MinHeight", minHeight);
            material.SetFloat("_MaxHeight", maxHeight);
        }

        private static Texture2DArray GenerateTextureArray(Texture2D[] textures)
        {
            Texture2DArray textureArray = new Texture2DArray(textureSize, textureSize, textures.Length, textureFormat, true);

            for (int i = 0; i < textures.Length; i++)
            {
                if (textures[i] == null) continue;

#if UNITY_EDITOR
                string path = AssetDatabase.GetAssetPath(textures[i]);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null && !importer.isReadable)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                }
#endif

                textureArray.SetPixels(textures[i].GetPixels(), i);
            }

            textureArray.Apply();
            return textureArray;
        }

        [System.Serializable]
        public class Layer
        {
            public Texture2D texture;
            public Color tint;
            [Range(0, 1)] public float tintStrength;
            [Range(0, 1)] public float startHeight;
            [Range(0, 1)] public float blendStrength;
            public float textureScale;
        }
    }
}
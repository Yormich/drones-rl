using UnityEngine;
using System;

namespace ContinuousWorld
{
    public enum NoiseType { UnityPerlin, Simplex }

    [Serializable]
    public class NoiseSettings
    {
        public NoiseType noiseType;
        public Noise.NormalizeMode normalizeMode;

        public float scale = 50;

        public int octaves = 6;
        [Range(0, 1)]
        public float persistance = 0.6f;
        public float lacunarity = 2;

        public int seed;
        public Vector2 offset;

        public void ValidateValues()
        {
            scale = Mathf.Max(scale, 0.01f);
            octaves = Mathf.Max(octaves, 1);
            lacunarity = Mathf.Max(lacunarity, 1);
            persistance = Mathf.Clamp01(persistance);
        }
    }
}
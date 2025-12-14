using UnityEngine;

namespace ContinuousWorld
{
    public class PerlinNoiseSampler : INoiseSampler
    {
        public float Sample(float x, float y)
        {
            // Mathf.PerlinNoise returns 0..1
            // We remap it to -1..1 to match standard noise definitions (like Simplex)
            return Mathf.PerlinNoise(x, y) * 2f - 1f;
        }
    }
}
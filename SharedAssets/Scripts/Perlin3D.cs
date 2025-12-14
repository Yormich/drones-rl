using UnityEngine;


namespace GenerationUtilities
{
    public static class Perlin3D
    {
        // Generate 3D Noise (returns 0 to 1)
        public static float Noise(float x, float y, float z)
        {
            float xy = Mathf.PerlinNoise(x, y);
            float yz = Mathf.PerlinNoise(y, z);
            float xz = Mathf.PerlinNoise(x, z);

            float yx = Mathf.PerlinNoise(y, x);
            float zy = Mathf.PerlinNoise(z, y);
            float zx = Mathf.PerlinNoise(z, x);

            return (xy + yz + xz + yx + zy + zx) / 6f;
        }
    }
}
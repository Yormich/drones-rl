using GenerationUtilities;
using System.Collections.Generic;
using UnityEngine;

namespace GridWorld.Generation
{
    public class SimplexMapGenerator : IMapGenerator
    {
        private const float NoiseFrequency = 0.15f;

        public HashSet<Vector3Int> Generate(Vector3Int gridSize, int seed, float density)
        {
            var obstacles = new HashSet<Vector3Int>();

            System.Random prng = new System.Random(seed);
            float offsetX = prng.Next(-10000, 10000);
            float offsetY = prng.Next(-10000, 10000);
            float offsetZ = prng.Next(-10000, 10000);


            for (int x = 0; x < gridSize.x; x++)
            {
                for (int y = 0; y < gridSize.y; y++)
                {
                    for (int z = 0; z < gridSize.z; z++)
                    {
                        float noiseValue = SimplexNoise.Noise3D(
                            x * NoiseFrequency + offsetX,
                            y * NoiseFrequency + offsetY,
                            z * NoiseFrequency + offsetZ
                        );

                        // Normalize from approx [-1, 1] to [0, 1]
                        float normalizedNoise = (noiseValue + 1f) / 2f;

                        // Clamp strictly to 0-1 to handle mathematical edge case overshoots
                        normalizedNoise = Mathf.Clamp01(normalizedNoise);

                        if (normalizedNoise > (1.0f - density))
                        {
                            obstacles.Add(new Vector3Int(x, y, z));
                        }
                    }
                }
            }

            return obstacles;
        }
    }
}
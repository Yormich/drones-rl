using UnityEngine;
using System.Collections.Generic;
using GenerationUtilities;

namespace GridWorld.Generation
{
    public class PerlinCaveGenerator : IMapGenerator
    {
        private readonly float scale = 0.15f; // Controls the "Zoom" (Lower = larger caves)

        public HashSet<Vector3Int> Generate(Vector3Int gridSize, int seed, float density)
        {
            HashSet<Vector3Int> obstacles = new HashSet<Vector3Int>();

            // Randomize the noise origin so every seed looks different
            Vector3 offset = new Vector3(seed % 1000, (seed * 2) % 1000, (seed * 3) % 1000);

            for (int x = 0; x < gridSize.x; x++)
            {
                for (int y = 0; y < gridSize.y; y++)
                {
                    for (int z = 0; z < gridSize.z; z++)
                    {
                        float pX = (x * scale) + offset.x;
                        float pY = (y * scale) + offset.y;
                        float pZ = (z * scale) + offset.z;

                        // Get 3D Noise Value (0.0 to 1.0)
                        float noiseValue = Perlin3D.Noise(pX, pY, pZ);

                        // Density Check
                         if (noiseValue > (1.0f - density))
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
using UnityEngine;
using System.Collections.Generic;

namespace GridWorld.Generation
{
    public class CellularAutomataGenerator : IMapGenerator
    {
        private readonly int iterations;
        private readonly int fillPercent;

        public CellularAutomataGenerator(int iterations = 5, int fillPercent = 45)
        {
            this.iterations = iterations;
            this.fillPercent = fillPercent;
        }

        public HashSet<Vector3Int> Generate(Vector3Int gridSize, int seed, float density)
        {
            // 1. Initialize the Map
            int[,,] map = new int[gridSize.x, gridSize.y, gridSize.z];
            RandomFill(map, gridSize, seed);

            // 2. Smooth the Map (The "Game of Life" part)
            for (int i = 0; i < iterations; i++)
            {
                SmoothMap(map, gridSize);
            }

            // 3. Convert int array to HashSet for the GridArea
            HashSet<Vector3Int> obstacles = new HashSet<Vector3Int>();
            for (int x = 0; x < gridSize.x; x++)
            {
                for (int y = 0; y < gridSize.y; y++)
                {
                    for (int z = 0; z < gridSize.z; z++)
                    {
                        if (map[x, y, z] != 0)
                        {
                            obstacles.Add(new Vector3Int(x, y, z));
                        }
                    }
                }
            }

            return obstacles;
        }

        private void RandomFill(int[,,] map, Vector3Int size, int seed)
        {
            System.Random pseudoRandom = new System.Random(seed);

            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    for (int z = 0; z < size.z; z++)
                    {
                        // Edges are always walls (prevents leaking)

                        // ifd(x == 0 || x == size.x - 1 ||
                        //    y == 0 || y == size.y - 1 ||
                        //    z == 0 || z == size.z - 1)
                        //{d
                        //    map[x, y, z] = 1
                        //}d
                        //else
                        //{d
                        //    // Randomly fill based on percentage
                        //    map[x, y, z] = (pseudoRandom.Next(0, 100) < fillPercent) ? 1 : 0
                        //}d

                        map[x, y, z] = (pseudoRandom.Next(0, 100) < fillPercent) ? 1 : 0;
                    }
                }
            }
        }

        private static void SmoothMap(int[,,] map, Vector3Int size)
        {
            int[,,] nextMap = new int[size.x, size.y, size.z];

            // In 3D, we have 26 neighbors. 
            // Threshold: 13 is roughly half.
            const int neighborsThreshold = 13;

            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    for (int z = 0; z < size.z; z++)
                    {
                        int neighborWallCount = GetSurroundingWallCount(x, y, z, map, size);

                        if (neighborWallCount > neighborsThreshold)
                        {
                            // If surrounded by walls
                            nextMap[x, y, z] = 1;
                        }
                        else if (neighborWallCount < neighborsThreshold)
                        {
                            // If surrounded by air, become air
                            nextMap[x, y, z] = 0;
                        }
                        else
                        {
                            // Stay as you were
                            nextMap[x, y, z] = map[x, y, z];
                        }
                    }
                }
            }

            System.Array.Copy(nextMap, map, map.Length);
        }

        private static int GetSurroundingWallCount(int gridX, int gridY, int gridZ, int[,,] map, Vector3Int size)
        {
            int wallCount = 0;

            // Loop through 3x3x3 block around the point
            for (int neighborX = gridX - 1; neighborX <= gridX + 1; neighborX++)
            {
                for (int neighborY = gridY - 1; neighborY <= gridY + 1; neighborY++)
                {
                    for (int neighborZ = gridZ - 1; neighborZ <= gridZ + 1; neighborZ++)
                    {
                        // count borders as walls
                        wallCount += IsValidForCount(gridX, gridY, gridZ, neighborX, neighborY, neighborZ, size) ? 
                            map[neighborX, neighborY, neighborZ] : 1;
                    }
                }
            }
            return wallCount;
        }

        private static bool IsValidForCount(int x, int y, int z, int nX, int nY, int nZ, Vector3Int size)
        {
            return IsInsideGridBounds(nX, nY, nZ, size) && (nX != x || nY != y || nZ != z);
        }

        private static bool IsInsideGridBounds(int x, int y, int z, Vector3Int size)
        {
            return x >= 0 && y >= 0 && z >= 0 && x < size.x && y < size.y && z < size.z;
        }
    }
}
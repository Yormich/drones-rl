using GridWorld.Generation;
using System.Collections.Generic;
using UnityEngine;


namespace GridWorld.Generation
{
    public class CityscapeGenerator : IMapGenerator
    {
        private const int MinBuildingHeight = 4;
        private const int MinBridgeHeight = 2;
        private const int MaxBridgeLength = 6;
        private const float BridgeChance = 0.4f;

        private const int EmptySpace = -1;

        public HashSet<Vector3Int> Generate(Vector3Int gridSize, int seed, float density)
        {
            HashSet<Vector3Int> obstacles = new HashSet<Vector3Int>();
            Random.InitState(seed);

            int[,] heightMap = GenerateBuildingHeightmap(gridSize, density, obstacles);

            GenerateBridges(gridSize, heightMap, obstacles);

            return obstacles;
        }

        private static int[,] GenerateBuildingHeightmap(Vector3Int gridSize, float density, HashSet<Vector3Int> obstacles)
        {
            int[,] map = new int[gridSize.x, gridSize.z];

            for (int x = 0; x < gridSize.x; x++)
            {
                for (int z = 0; z < gridSize.z; z++)
                {
                    map[x, z] = EmptySpace;

                    if (Random.value < density)
                    {
                        int h = Random.Range(MinBuildingHeight, gridSize.y);
                        map[x, z] = h;

                        for (int y = 0; y < h; y++)
                        {
                            obstacles.Add(new Vector3Int(x, y, z));
                        }
                    }
                }
            }
            return map;
        }

        private static void GenerateBridges(Vector3Int gridSize, int[,] heightMap, HashSet<Vector3Int> obstacles)
        {
            for (int x = 0; x < gridSize.x; x++)
            {
                for (int z = 0; z < gridSize.z; z++)
                {
                    if (heightMap[x, z] != EmptySpace)
                    {
                        // Look Right (X+)
                        TryBuildBridge(new Vector3Int(x, 0, z), Vector3Int.right, gridSize, heightMap, obstacles);

                        // Look Forward (Z+)
                        TryBuildBridge(new Vector3Int(x, 0, z), Vector3Int.forward, gridSize, heightMap, obstacles);
                    }
                }
            }
        }

        private static void TryBuildBridge(Vector3Int startPos, Vector3Int direction, Vector3Int gridSize, int[,] heightMap, HashSet<Vector3Int> obstacles)
        {
            int gapSize = 0;
            Vector3Int targetPos = Vector3Int.zero;
            bool foundTarget = false;

            for (int dist = 1; dist <= MaxBridgeLength; dist++)
            {
                Vector3Int currentLookup = startPos + (direction * dist);

                if (currentLookup.x >= gridSize.x || currentLookup.z >= gridSize.z) break;

                int currentHeight = heightMap[currentLookup.x, currentLookup.z];

                if (currentHeight == EmptySpace)
                {
                    gapSize++;
                }
                else
                {
                    foundTarget = true;
                    targetPos = currentLookup;
                    break;
                }
            }

            // Validation: We need a target, a gap > 0, and luck
            if (foundTarget && gapSize > 0 && Random.value < BridgeChance)
            {
                int startHeight = heightMap[startPos.x, startPos.z];
                int targetHeight = heightMap[targetPos.x, targetPos.z];

                CreateBridge(startPos, direction, gapSize, startHeight, targetHeight, obstacles);
            }
        }

        private static void CreateBridge(Vector3Int startPos, Vector3Int direction, int length, int h1, int h2, HashSet<Vector3Int> obstacles)
        {
            // Bridge must be lower than the shortest building's roof
            int maxPossibleHeight = Mathf.Min(h1, h2);

            if (maxPossibleHeight <= MinBridgeHeight) return;

            // Pick a random height for the bridge
            int bridgeY = Random.Range(MinBridgeHeight, maxPossibleHeight);

            for (int i = 1; i <= length; i++)
            {
                Vector3Int bridgePart = startPos + (direction * i);
                bridgePart.y = bridgeY;
                obstacles.Add(bridgePart);
            }
        }
    }
}
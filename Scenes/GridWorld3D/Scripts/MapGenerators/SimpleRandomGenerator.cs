using System.Collections.Generic;
using UnityEngine;

namespace GridWorld.Generation
{
    public class SimpleRandomGenerator : IMapGenerator
    {
        public HashSet<Vector3Int> Generate(Vector3Int gridSize, int seed, float density)
        {
            Random.InitState(seed);

            HashSet<Vector3Int> obstacles = new HashSet<Vector3Int>();

            int environmentVolume = gridSize.x * gridSize.y * gridSize.z;
            int obstaclesAmountNeeded = Mathf.FloorToInt(environmentVolume * density);

            while (obstacles.Count < obstaclesAmountNeeded)
            {
                int coordinate = Random.Range(0, environmentVolume);

                obstacles.Add(new Vector3Int(coordinate % gridSize.x, coordinate / (gridSize.x * gridSize.z), (coordinate / gridSize.x) % gridSize.z));
            }
            return obstacles;
        }
    }

}
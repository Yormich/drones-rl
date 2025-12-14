using UnityEngine;
using System.Collections.Generic;

namespace GridWorld.Generation
{
    public interface IMapGenerator
    {
        HashSet<Vector3Int> Generate(Vector3Int gridSize, int seed, float density);
    }
}
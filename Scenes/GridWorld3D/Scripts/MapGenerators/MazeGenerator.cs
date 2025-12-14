using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace GridWorld.Generation
{
    public class MazeGenerator : IMapGenerator
    {
        // 6 Directions for the digger (Up, Down, Left, Right, Fwd, Back)
        private readonly Vector3Int[] directions = {
            Vector3Int.up, Vector3Int.down,
            Vector3Int.left, Vector3Int.right,
            Vector3Int.forward, Vector3Int.back
        };

        public HashSet<Vector3Int> Generate(Vector3Int gridSize, int seed, float density)
        {
            // 1. Start with everything being a wall (Obstacle)
            HashSet<Vector3Int> obstacles = new HashSet<Vector3Int>();
            for (int x = 0; x < gridSize.x; x++)
            {
                for (int y = 0; y < gridSize.y; y++)
                {
                    for (int z = 0; z < gridSize.z; z++)
                    {
                        obstacles.Add(new Vector3Int(x, y, z));
                    }
                }
            }

            // 2. Setup Maze Logic
            // We dig on odd coordinates to ensure walls remain between paths
            Random.InitState(seed);
            Stack<Vector3Int> stack = new Stack<Vector3Int>();

            // Start digging at (1,1,1)
            Vector3Int startPos = new Vector3Int(1, 1, 1);
            stack.Push(startPos);
            obstacles.Remove(startPos); // Remove wall (create air)

            // 3. The Digger Loop
            while (stack.Count > 0)
            {
                Vector3Int current = stack.Peek();
                List<Vector3Int> neighbors = GetUnvisitedNeighbors(current, gridSize, obstacles);

                if (neighbors.Count > 0)
                {
                    // Choose a random neighbor
                    Vector3Int next = neighbors[Random.Range(0, neighbors.Count)];

                    // Calculate the wall *between* current and next
                    Vector3Int wallBetween = current + (next - current) / 2;

                    // Remove the wall between and the target block
                    obstacles.Remove(wallBetween);
                    obstacles.Remove(next);

                    stack.Push(next);
                }
                else
                {
                    // Dead end, backtrack
                    stack.Pop();
                }
            }

            return obstacles;
        }

        private List<Vector3Int> GetUnvisitedNeighbors(Vector3Int pos, Vector3Int size, HashSet<Vector3Int> currentWalls)
        {
            List<Vector3Int> list = new List<Vector3Int>();

            foreach (var dir in directions)
            {
                // We jump 2 blocks! This is key for maze generation.
                // 1 block jump = wall between, 2 block jump = next cell
                Vector3Int neighbor = pos + (dir * 2);

                // If the cell is in bounds and we haven't visited it yet
                if (IsInBounds(neighbor, size) && currentWalls.Contains(neighbor))
                {
                    list.Add(neighbor);
                }
            }
            return list;
        }

        private static bool IsInBounds(Vector3Int pos, Vector3Int size)
        {
            // Don't keep a one-unit margin because environment is a locked box itself
            return pos.x >= 0 && pos.x < size.x &&
                   pos.y >= 0 && pos.y < size.y &&
                   pos.z >= 0 && pos.z < size.z;
        }
    }
}
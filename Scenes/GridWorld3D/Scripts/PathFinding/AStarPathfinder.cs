using System;
using System.Collections.Generic;
using UnityEngine;

namespace GridWorld.AI
{
    public class AStarPathfinder
    {
        private readonly GridArea3D _gridEnvironment;
        private readonly Vector3Int[] _directions;

        public AStarPathfinder(GridArea3D gridEnvironment)
        {
            _gridEnvironment = gridEnvironment;
            _directions = _gridEnvironment.Agent.ActionSet;
        }

        public List<Vector3Int> FindPath(Vector3Int startPos, Vector3Int endPos)
        {
            if (!ValidateEndPosition(endPos))
            {
                return null;
            }

            var openSet = new SimplePriorityQueue<Node>();

            var gCostTracker = new Dictionary<Vector3Int, int>();

            Node startNode = new Node(startPos, null, 0, GetManhattanDistance(startPos, endPos));
            openSet.Enqueue(startNode);
            gCostTracker[startPos] = 0;

            while (openSet.Count > 0)
            {
                Node currentNode = openSet.Dequeue();

                if (gCostTracker.ContainsKey(currentNode.Position) && gCostTracker[currentNode.Position] < currentNode.G)
                {
                    continue;
                }

                if (currentNode.Position == endPos)
                {
                    return RetracePath(currentNode);
                }

                TraverseNeighbours(currentNode, gCostTracker, openSet, endPos);
            }

            return null;
        }

        private void TraverseNeighbours(Node currentNode, Dictionary<Vector3Int, int> gCostTracker, SimplePriorityQueue<Node> openSet, Vector3Int endPos)
        {
            foreach (Vector3Int dir in _directions)
            {
                Vector3Int neighborPos = currentNode.Position + dir;

                if (!_gridEnvironment.IsPositionFree(neighborPos)) continue;

                int newGCost = currentNode.G + 1;

                if (!gCostTracker.ContainsKey(neighborPos) || newGCost < gCostTracker[neighborPos])
                {
                    gCostTracker[neighborPos] = newGCost;

                    int hCost = GetManhattanDistance(neighborPos, endPos);
                    Node neighborNode = new Node(neighborPos, currentNode, newGCost, hCost);

                    openSet.Enqueue(neighborNode);
                }
            }
        }

        private static List<Vector3Int> RetracePath(Node endNode)
        {
            List<Vector3Int> path = new List<Vector3Int>();
            Node currentNode = endNode;

            while (currentNode != null)
            {
                path.Add(currentNode.Position);
                currentNode = currentNode.Parent;
            }

            path.Reverse();
            return path;
        }

        private static int GetManhattanDistance(Vector3Int a, Vector3Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) + Mathf.Abs(a.z - b.z);
        }

        private bool ValidateEndPosition(Vector3Int endPos)
        {
            if (!_gridEnvironment.IsPositionFree(endPos) && endPos != _gridEnvironment.GetGoalEnvironmentPosition())
            {
                Debug.LogError("A*: Target position is obstructed or out of bounds.");
                return false;
            }

            return true;
        }

        private class Node : IComparable<Node>
        {
            public Vector3Int Position;
            public Node Parent;
            public int G;
            public int H;
            public int F => G + H;

            public Node(Vector3Int position, Node parent, int g, int h)
            {
                Position = position;
                Parent = parent;
                G = g;
                H = h;
            }

            // Priority Queue sorting logic
            public int CompareTo(Node other)
            {
                int compare = F.CompareTo(other.F);

                if (compare == 0) compare = H.CompareTo(other.H);

                return compare;
            }
        }
    }
}

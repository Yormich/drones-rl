using UnityEngine;

namespace ContinuousWorld
{
    public static class MeshGenerator
    {
        public static MeshData GenerateTerrainMesh(float[,] heightMap, int levelOfDetail, MeshSettings meshSettings)
        {
            var builder = new MeshBuilder(heightMap, levelOfDetail, meshSettings);
            return builder.ConstructMesh();
        }

        private struct MeshBuilder
        {
            private readonly float[,] heightMap;
            private readonly MeshSettings settings;
            private readonly int skipIncrement;
            private readonly int numVerticesPerLine;
            private readonly Vector2 topLeft;
            private readonly int[,] vertexIndicesMap;

            private int meshVertexIndex;
            private int outOfMeshVertexIndex;

            // Define critical indices for ring logic
            private readonly int indexHighLodStart; // 1
            private readonly int indexConnectorStart; // 2
            private readonly int indexConnectorEnd; // Max - 3
            private readonly int indexHighLodEnd; // Max - 2

            private enum VertexType { Ghost, VisibleHighLOD, VisibleConnector, VisibleLowLOD, Skipped }

            public MeshBuilder(float[,] heightMap, int levelOfDetail, MeshSettings settings)
            {
                this.heightMap = heightMap;
                this.settings = settings;
                this.skipIncrement = (levelOfDetail == 0) ? 1 : levelOfDetail * 2;
                this.numVerticesPerLine = settings.NumVerticesPerLine;
                this.topLeft = new Vector2(-1, 1) * settings.MeshWorldSize / 2f;

                this.vertexIndicesMap = new int[numVerticesPerLine, numVerticesPerLine];

                // Initialize map with -1 to detect unassigned errors
                for (int x = 0; x < numVerticesPerLine; x++)
                    for (int y = 0; y < numVerticesPerLine; y++)
                        vertexIndicesMap[x, y] = -1;

                this.meshVertexIndex = 0;
                this.outOfMeshVertexIndex = -1;

                // Pre-calculate ring indices
                this.indexHighLodStart = 1;
                this.indexConnectorStart = 2;
                this.indexConnectorEnd = numVerticesPerLine - 3;
                this.indexHighLodEnd = numVerticesPerLine - 2;
            }

            public MeshData ConstructMesh()
            {
                MeshData meshData = new MeshData(numVerticesPerLine, skipIncrement, settings.useFlatShading);

                // Pass 1: Map Topology
                for (int y = 0; y < numVerticesPerLine; y++)
                {
                    for (int x = 0; x < numVerticesPerLine; x++)
                    {
                        AssignVertexIndex(x, y);
                    }
                }

                // Pass 2: Generate Geometry
                for (int y = 0; y < numVerticesPerLine; y++)
                {
                    for (int x = 0; x < numVerticesPerLine; x++)
                    {
                        VertexType type = GetVertexType(x, y);
                        if (type == VertexType.Skipped) continue;

                        ProcessVertex(x, y, type, meshData);
                    }
                }

                meshData.FinalizeMesh(); // Trim arrays
                return meshData;
            }

            private VertexType GetVertexType(int x, int y)
            {
                // 1. Ghost Vertices (Outer Ring)
                if (x == 0 || y == 0 || x == numVerticesPerLine - 1 || y == numVerticesPerLine - 1)
                    return VertexType.Ghost;

                // 2. High LOD Border (Ring 1)
                if (x == indexHighLodStart || y == indexHighLodStart || x == indexHighLodEnd || y == indexHighLodEnd)
                    return VertexType.VisibleHighLOD;

                // 3. Connector Ring (Ring 2)
                if (x == indexConnectorStart || y == indexConnectorStart || x == indexConnectorEnd || y == indexConnectorEnd)
                    return VertexType.VisibleConnector;

                // 4. Center (Low LOD)
                if ((x - 2) % skipIncrement == 0 && (y - 2) % skipIncrement == 0)
                    return VertexType.VisibleLowLOD;

                return VertexType.Skipped;
            }

            private void AssignVertexIndex(int x, int y)
            {
                VertexType type = GetVertexType(x, y);

                if (type == VertexType.Ghost)
                {
                    vertexIndicesMap[x, y] = outOfMeshVertexIndex;
                    outOfMeshVertexIndex--;
                }
                else if (type != VertexType.Skipped)
                {
                    vertexIndicesMap[x, y] = meshVertexIndex;
                    meshVertexIndex++;
                }
            }

            private void ProcessVertex(int x, int y, VertexType type, MeshData meshData)
            {
                int vertexIndex = vertexIndicesMap[x, y];
                if (vertexIndex == -1) return; // Should not happen given loop structure

                // --- Position ---
                Vector2 percent = new Vector2(x - 1, y - 1) / (numVerticesPerLine - 3);
                Vector2 vertexPosition2D = topLeft + new Vector2(percent.x, -percent.y) * settings.MeshWorldSize;
                float height = heightMap[x, y];

                // Fix T-Junctions
                if (type == VertexType.VisibleConnector)
                {
                    height = GetInterpolatedHeight(x, y, height);
                }

                meshData.AddVertex(new Vector3(vertexPosition2D.x, height, vertexPosition2D.y), percent, vertexIndex);

                // --- Triangles ---
                // We only generate Right and Down.
                // We stop at the second to last index (because the last index is Ghost).
                bool canGoRight = x < numVerticesPerLine - 1;
                bool canGoDown = y < numVerticesPerLine - 1;

                if (canGoRight && canGoDown)
                {
                    int incX = GetNextValidStep(x, y, true);
                    int incY = GetNextValidStep(x, y, false);

                    // Ensure we don't go out of bounds (Ghost check)
                    if (x + incX < numVerticesPerLine && y + incY < numVerticesPerLine)
                    {
                        int a = vertexIndicesMap[x, y];
                        int b = vertexIndicesMap[x + incX, y];
                        int c = vertexIndicesMap[x, y + incY];
                        int d = vertexIndicesMap[x + incX, y + incY];

                        // If any vertex index is -1 (Skipped), we cannot build a triangle.
                        // (Ghost vertices have negative indices < -1, which IS valid for rendering)
                        if (a != -1 && b != -1 && c != -1 && d != -1)
                        {
                            meshData.AddTriangle(a, d, c);
                            meshData.AddTriangle(d, a, b);
                        }
                    }
                }
            }

            // Calculates the step to the next visible vertex.
            // Handles the transition from HighLOD -> Connector -> LowLOD -> Connector -> HighLOD
            private int GetNextValidStep(int x, int y, bool checkXAxis)
            {
                int i = checkXAxis ? x : y;

                // 1. Ghost -> HighLOD Border
                if (i == 0) return 1;

                // 2. HighLOD Border -> Connector
                if (i == indexHighLodStart) return 1; // 1 -> 2

                // 3. Connector -> LowLOD (Start of Grid)
                if (i == indexConnectorStart) return skipIncrement;

                // 4. Inside LowLOD Grid
                if (i > indexConnectorStart && i < indexConnectorEnd)
                {
                    // Check if a normal step would overshoot the Connector Ring
                    if (i + skipIncrement > indexConnectorEnd)
                    {
                        // Bridge the gap: Step exactly to the Connector Ring
                        return indexConnectorEnd - i;
                    }
                    return skipIncrement;
                }

                // 5. Connector -> HighLOD Border
                if (i == indexConnectorEnd) return 1;

                // 6. HighLOD Border -> Ghost
                if (i == indexHighLodEnd) return 1;

                return 1;
            }

            private float GetInterpolatedHeight(int x, int y, float originalHeight)
            {
                bool isVertical = x == indexConnectorStart || x == indexConnectorEnd;

                // Identify the two main LOD parents
                int distA = ((isVertical ? y : x) - 2) % skipIncrement;
                int distB = skipIncrement - distA;
                float pct = distA / (float)skipIncrement;

                int x1 = isVertical ? x : x - distA;
                int y1 = isVertical ? y - distA : y;

                int x2 = isVertical ? x : x + distB;
                int y2 = isVertical ? y + distB : y;

                // Safety Check
                if (x1 < 0 || x2 >= numVerticesPerLine || y1 < 0 || y2 >= numVerticesPerLine)
                    return originalHeight;

                float h1 = heightMap[x1, y1];
                float h2 = heightMap[x2, y2];

                return h1 * (1 - pct) + h2 * pct;
            }
        }
    }
}
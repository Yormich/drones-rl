using UnityEngine;
using System.Collections.Generic;

namespace ContinuousWorld
{
    public class MeshData
    {
        // Mesh Data Arrays
        private int[] triangles;
        private Vector3[] vertices;
        private Vector2[] uvs;
        private Vector3[] bakedNormals;

        // Border Data (for seamless normal calculation)
        private readonly Vector3[] outOfMeshVertices;
        private readonly int[] outOfMeshTriangles;

        // Tracking Indices
        private int vertexIndexCounter;
        private int triangleIndexCounter;
        private int outOfMeshTriangleIndexCounter;

        // Settings
        private readonly bool useFlatShading;

        public MeshData(int numVerticesPerLine, int skipIncrement, bool useFlatShading)
        {
            this.useFlatShading = useFlatShading;

            int maxPossibleVertices = numVerticesPerLine * numVerticesPerLine;

            this.vertices = new Vector3[maxPossibleVertices];
            this.uvs = new Vector2[maxPossibleVertices];

            // Allocate triangles (Max estimate: 2 triangles per square)
            int maxTriangles = (numVerticesPerLine - 1) * (numVerticesPerLine - 1) * 6;
            this.triangles = new int[maxTriangles];

            // Border data is predictable
            int numGhostVertices = 4 * numVerticesPerLine - 4;
            this.outOfMeshVertices = new Vector3[numGhostVertices];
            this.outOfMeshTriangles = new int[maxTriangles];

            this.vertexIndexCounter = 0;
            this.triangleIndexCounter = 0;
            this.outOfMeshTriangleIndexCounter = 0;
        }

        public void AddVertex(Vector3 vertexPosition, Vector2 uv, int vertexIndex)
        {
            if (vertexIndex < 0)
            {
                // Border/Ghost Vertex
                int index = -vertexIndex - 1;
                if (index < outOfMeshVertices.Length)
                {
                    outOfMeshVertices[index] = vertexPosition;
                }
            }
            else
            {
                // Main Mesh Vertex
                // Safety check to prevent IndexOutOfRange if logic goes wild
                if (vertexIndex < vertices.Length)
                {
                    vertices[vertexIndex] = vertexPosition;
                    uvs[vertexIndex] = uv;

                    // Keep track of how many we actually used
                    if (vertexIndex >= vertexIndexCounter)
                        vertexIndexCounter = vertexIndex + 1;
                }
            }
        }

        public void AddTriangle(int sideA, int sideB, int sideC)
        {
            // If any vertex is a ghost (-1), this is a border triangle
            if (sideA < 0 || sideB < 0 || sideC < 0)
            {
                if (outOfMeshTriangleIndexCounter < outOfMeshTriangles.Length - 3)
                {
                    outOfMeshTriangles[outOfMeshTriangleIndexCounter] = sideA;
                    outOfMeshTriangles[outOfMeshTriangleIndexCounter + 1] = sideB;
                    outOfMeshTriangles[outOfMeshTriangleIndexCounter + 2] = sideC;
                    outOfMeshTriangleIndexCounter += 3;
                }
            }
            else
            {
                if (triangleIndexCounter < triangles.Length - 3)
                {
                    triangles[triangleIndexCounter] = sideA;
                    triangles[triangleIndexCounter + 1] = sideB;
                    triangles[triangleIndexCounter + 2] = sideC;
                    triangleIndexCounter += 3;
                }
            }
        }

        /// <summary>
        /// Trims arrays to actual size and calculates normals.
        /// </summary>
        public void FinalizeMesh()
        {
            // Resize Vertices/UVs to actual count
            if (vertexIndexCounter < vertices.Length)
            {
                System.Array.Resize(ref vertices, vertexIndexCounter);
                System.Array.Resize(ref uvs, vertexIndexCounter);
            }

            // Resize Triangles to actual count
            if (triangleIndexCounter < triangles.Length)
            {
                System.Array.Resize(ref triangles, triangleIndexCounter);
            }

            if (useFlatShading)
            {
                FlatShading();
            }
            else
            {
                BakeNormals();
            }
        }

        public Mesh CreateMesh()
        {
            // Ensure data is prepped
            Mesh mesh = new Mesh();

            // Unity supports 32 bit indices (allows > 65k vertices)
            if (vertices.Length > 65000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;

            if (useFlatShading)
            {
                mesh.RecalculateNormals();
            }
            else
            {
                mesh.normals = bakedNormals;
            }

            return mesh;
        }

        #region Normals & Shading
        private void BakeNormals()
        {
            bakedNormals = CalculateNormals();
        }

        private void FlatShading()
        {
            Vector3[] flatShadedVertices = new Vector3[triangles.Length];
            Vector2[] flatShadedUvs = new Vector2[triangles.Length];

            for (int i = 0; i < triangles.Length; i++)
            {
                flatShadedVertices[i] = vertices[triangles[i]];
                flatShadedUvs[i] = uvs[triangles[i]];
                triangles[i] = i;
            }

            vertices = flatShadedVertices;
            uvs = flatShadedUvs;
        }

        private Vector3[] CalculateNormals()
        {
            Vector3[] vertexNormals = new Vector3[vertices.Length];

            // Visible Triangles
            int triCount = triangleIndexCounter / 3;
            for (int i = 0; i < triCount; i++)
            {
                int normalIndex = i * 3;
                int a = triangles[normalIndex];
                int b = triangles[normalIndex + 1];
                int c = triangles[normalIndex + 2];

                Vector3 triNormal = SurfaceNormalFromIndices(a, b, c);
                vertexNormals[a] += triNormal;
                vertexNormals[b] += triNormal;
                vertexNormals[c] += triNormal;
            }

            // Border Triangles
            int borderTriCount = outOfMeshTriangleIndexCounter / 3;
            for (int i = 0; i < borderTriCount; i++)
            {
                int normalIndex = i * 3;
                int a = outOfMeshTriangles[normalIndex];
                int b = outOfMeshTriangles[normalIndex + 1];
                int c = outOfMeshTriangles[normalIndex + 2];

                Vector3 triNormal = SurfaceNormalFromIndices(a, b, c);

                if (a >= 0) vertexNormals[a] += triNormal;
                if (b >= 0) vertexNormals[b] += triNormal;
                if (c >= 0) vertexNormals[c] += triNormal;
            }

            for (int i = 0; i < vertexNormals.Length; i++)
            {
                vertexNormals[i].Normalize();
            }

            return vertexNormals;
        }

        private Vector3 SurfaceNormalFromIndices(int indexA, int indexB, int indexC)
        {
            Vector3 pA = (indexA < 0) ? outOfMeshVertices[-indexA - 1] : vertices[indexA];
            Vector3 pB = (indexB < 0) ? outOfMeshVertices[-indexB - 1] : vertices[indexB];
            Vector3 pC = (indexC < 0) ? outOfMeshVertices[-indexC - 1] : vertices[indexC];

            Vector3 sideAB = pB - pA;
            Vector3 sideAC = pC - pA;
            return Vector3.Cross(sideAB, sideAC).normalized;
        }
        #endregion
    }
}
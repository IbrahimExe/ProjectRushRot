using UnityEngine;

namespace LevelGenerator
{
    public static class MeshGenerator
    {
        // Generates a terrain mesh from a heightmap.
        // Width and height of the mesh match the heightmap dimensions.
        // Heights are scaled by heightMultiplier for world-space displacement.

        public static MeshData GenerateTerrainMesh(float[,] heightMap, float heightMultiplier)
        {
            int width  = heightMap.GetLength(0);
            int height = heightMap.GetLength(1);

            // Centre the mesh on the origin
            float topLeftX = (width  - 1) / -2f;
            float topLeftZ = (height - 1) /  2f;

            var meshData    = new MeshData(width, height);
            int vertexIndex = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    meshData.Vertices[vertexIndex] = new Vector3(
                        topLeftX + x,
                        heightMap[x, y] * heightMultiplier,
                        topLeftZ - y);

                    meshData.UVs[vertexIndex] = new Vector2(
                        x / (float)width,
                        y / (float)height);

                    // Add two triangles per quad, skip last row and column
                    if (x < width - 1 && y < height - 1)
                    {
                        meshData.AddTriangle(vertexIndex,             vertexIndex + width + 1, vertexIndex + width);
                        meshData.AddTriangle(vertexIndex + width + 1, vertexIndex,             vertexIndex + 1);
                    }

                    vertexIndex++;
                }
            }

            return meshData;
        }
    }

    public class MeshData
    {
        public Vector3[] Vertices;
        public Vector2[] UVs;

        int[] _triangles;
        int   _triangleIndex;

        public MeshData(int meshWidth, int meshHeight)
        {
            Vertices   = new Vector3[meshWidth * meshHeight];
            UVs        = new Vector2[meshWidth * meshHeight];
            _triangles = new int[(meshWidth - 1) * (meshHeight - 1) * 6];
        }

        public void AddTriangle(int a, int b, int c)
        {
            _triangles[_triangleIndex]     = a;
            _triangles[_triangleIndex + 1] = b;
            _triangles[_triangleIndex + 2] = c;
            _triangleIndex += 3;
        }

        public Mesh CreateMesh()
        {
            var mesh = new Mesh();
            mesh.vertices  = Vertices;
            mesh.triangles = _triangles;
            mesh.uv        = UVs;
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}

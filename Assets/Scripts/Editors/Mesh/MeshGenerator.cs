using UnityEngine;

namespace LevelGenerator
{
    public static class MeshGenerator
    {
        public static MeshData GenerateTerrainMesh(float[,] heightMap, float heightMultiplier, AnimationCurve _heightCurve, int levelOfDetail, float chunkWorldSize)
        {
            AnimationCurve heightCurve = new AnimationCurve(_heightCurve.keys);
            int width = heightMap.GetLength(0);
            int height = heightMap.GetLength(1);

            float scale = chunkWorldSize / (width - 1);
            float topLeftX = chunkWorldSize / -2f;
            float topLeftZ = chunkWorldSize / 2f;

            int meshSimplificationIncrement = levelOfDetail == 0 ? 1 : levelOfDetail * 2;

            // Clamp increment so it always divides width-1 evenly
            while ((width - 1) % meshSimplificationIncrement != 0)
                meshSimplificationIncrement--;

            int verticesPerLine = (width - 1) / meshSimplificationIncrement + 1;

            MeshData meshData = new MeshData(verticesPerLine, verticesPerLine);
            int vertexIndex = 0;

            for (int y = 0; y < height; y += meshSimplificationIncrement)
            {
                for (int x = 0; x < width; x += meshSimplificationIncrement)
                {
                    meshData.vertices[vertexIndex] = new Vector3(
                        topLeftX + x * scale,
                        heightCurve.Evaluate(heightMap[x, y]) * heightMultiplier,
                        topLeftZ - y * scale);

                    meshData.uvs[vertexIndex] = new Vector2(x / (float)(width - 1), y / (float)(height - 1));

                    if (x < width - 1 && y < height - 1)
                    {
                        meshData.AddTriangle(vertexIndex, vertexIndex + verticesPerLine + 1, vertexIndex + verticesPerLine);
                        meshData.AddTriangle(vertexIndex + verticesPerLine + 1, vertexIndex, vertexIndex + 1);
                    }

                    vertexIndex++;
                }
            }

            return meshData;
        }
    }

    public class MeshData
    {
        public Vector3[] vertices;
        public int[]     triangles;
        public Vector2[] uvs;

        int triangleIndex;

        public MeshData(int meshWidth, int meshHeight)
        {
            vertices  = new Vector3[meshWidth * meshHeight];
            uvs       = new Vector2[meshWidth * meshHeight];
            triangles = new int[(meshWidth - 1) * (meshHeight - 1) * 6];
        }

        public void AddTriangle(int a, int b, int c)
        {
            triangles[triangleIndex]     = a;
            triangles[triangleIndex + 1] = b;
            triangles[triangleIndex + 2] = c;
            triangleIndex += 3;
        }

        public Mesh CreateMesh()
        {
            Mesh mesh      = new Mesh();
            mesh.vertices  = vertices;
            mesh.triangles = triangles;
            mesh.uv        = uvs;
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}

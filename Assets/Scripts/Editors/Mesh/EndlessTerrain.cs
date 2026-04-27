using UnityEngine;
using System.Collections.Generic;

namespace LevelGenerator
{
    public class EndlessTerrain : MonoBehaviour
    {

        public LODInfo[] detailLevels;
        [Header("View")]
        public static float maxViewDist;
        const float viewerMoveThresholdForChunkUpdate = 25f;
        const float sqrViewerMoveThresholdForChunkUpdate = viewerMoveThresholdForChunkUpdate * viewerMoveThresholdForChunkUpdate;
        public Transform Viewer;
        public LevelGeneratorCommon Common;
        public Material TerrainMaterial;

        [Header("Chunk")]
        [Range(2, 240)]
        public int vertexResolution = 120;
        public float chunkWorldSize = 240f;

        public static Vector2 viewerPosition;
        Vector2 viewerPositionOld;
        public static MapGenerator mapGenerator;

        int _chunkSize;
        int _chunksVisibleInViewDst;

        Dictionary<Vector2, TerrainChunk> _terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
        List<TerrainChunk> _terrainChunksVisibleLastUpdate = new List<TerrainChunk>();

        void OnValidate()
        {
            vertexResolution = Mathf.Max(2, (vertexResolution / 2) * 2);
            chunkWorldSize = Mathf.Max(1f, chunkWorldSize);
        }

        void Start()
        {

            mapGenerator = FindObjectOfType<MapGenerator>();
            maxViewDist = detailLevels[detailLevels.Length - 1].distanceFraction;

            Common.VertexResolution = vertexResolution;
            Common.ChunkWorldSize = chunkWorldSize;

            _chunkSize = Mathf.RoundToInt(chunkWorldSize);
            _chunksVisibleInViewDst = Mathf.RoundToInt(maxViewDist / _chunkSize);

            UpdateVisibleChunks();
        }

        void Update()
        {
            viewerPosition = new Vector2(Viewer.position.x, Viewer.position.z);

            if ((viewerPositionOld - viewerPosition ).sqrMagnitude > sqrViewerMoveThresholdForChunkUpdate)
                { 
                viewerPositionOld = viewerPosition;
                UpdateVisibleChunks(); }
        }

        void UpdateVisibleChunks()
        {
            for (int i = 0; i < _terrainChunksVisibleLastUpdate.Count; i++)
                _terrainChunksVisibleLastUpdate[i].SetVisible(false);
            _terrainChunksVisibleLastUpdate.Clear();

            int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / _chunkSize);
            int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / _chunkSize);

            for (int yOffset = -_chunksVisibleInViewDst; yOffset <= _chunksVisibleInViewDst; yOffset++)
            {
                for (int xOffset = -_chunksVisibleInViewDst; xOffset <= _chunksVisibleInViewDst; xOffset++)
                {
                    Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

                    if (_terrainChunkDictionary.ContainsKey(viewedChunkCoord))
                    {
                        _terrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk();
                        if (_terrainChunkDictionary[viewedChunkCoord].IsVisible())
                            _terrainChunksVisibleLastUpdate.Add(_terrainChunkDictionary[viewedChunkCoord]);
                    }
                    else
                    {
                        var chunk = new TerrainChunk(
                            viewedChunkCoord, _chunkSize, transform,
                            TerrainMaterial, detailLevels, maxViewDist);
                        _terrainChunkDictionary.Add(viewedChunkCoord, chunk);
                    }
                }
            }
        }

        public class TerrainChunk
        {
            GameObject _meshObject;
            Vector2 _position;
            Bounds _bounds;
            MeshRenderer _meshRenderer;
            MeshFilter _meshFilter;
            Texture2D _texture;
            float _maxViewDist;

            // One LODMesh per LOD level (requested on demand, cached after first load)
            MapData _mapData;
            bool _mapDataReceived;
            LODInfo[] detailLevels;
            LODMesh[] lODMeshes;

            int previousLODindex = -1;

            public TerrainChunk(Vector2 coord, int size, Transform parent,
            Material material, LODInfo[] detailLevels, float maxViewDist)
            {
                this.detailLevels = detailLevels;
                _maxViewDist = maxViewDist;

                _position = coord * size;
                _bounds = new Bounds(_position, Vector2.one * size);

                Vector3 positionV3 = new Vector3(_position.x, 0, _position.y);

                _meshObject = new GameObject("Terrain Chunk");
                _meshObject.transform.parent = parent;
                _meshObject.transform.position = positionV3;
                _meshObject.transform.localScale = Vector3.one;

                _meshRenderer = _meshObject.AddComponent<MeshRenderer>();
                _meshFilter = _meshObject.AddComponent<MeshFilter>();
                _meshRenderer.material = new Material(material);

                // Request map data from the singleton using this chunk's world centre
                mapGenerator.RequestMapData(_position, OnMapDataReceived);

                SetVisible(false);

                lODMeshes = new LODMesh[detailLevels.Length];
                    for (int i = 0; i < detailLevels.Length; i++)
                        lODMeshes[i] = new LODMesh(detailLevels[i].lod, UpdateTerrainChunk);
            }

            void OnMapDataReceived(MapData mapData)
            {
                _mapData = mapData;
                _mapDataReceived = true;

                // Build texture once from colour map
                _texture = TextureGenerator.TextureFromColourMap(
                    _mapData.colorMap, MapGenerator.mapChunkSize, MapGenerator.mapChunkSize);
                _meshRenderer.sharedMaterial.mainTexture = _texture;

                // Trigger chunk update now that data is ready
                UpdateTerrainChunk();
            }

            public void UpdateTerrainChunk()
            {
                if (!_mapDataReceived) return;

                float dst = Mathf.Sqrt(_bounds.SqrDistance(viewerPosition));
                bool visible = dst <= _maxViewDist;

                if (visible)
                {
                    int lodIndex = 0;
                    for (int i = 0; i < detailLevels.Length - 1; i++)
                    {
                        if (dst > detailLevels[i].distanceFraction) lodIndex = i + 1;
                        else break;
                    }

                    if (lodIndex != previousLODindex)
                    {
                        LODMesh lodMesh = lODMeshes[lodIndex];
                        if (lodMesh.HasMesh)
                        {
                            previousLODindex = lodIndex;
                            _meshFilter.mesh = lodMesh.Mesh;
                        }
                        else if (!lodMesh.HasRequestedMesh)
                            lodMesh.RequestMesh(_mapData);
                    }
                }

                SetVisible(visible);
            }

            public void SetVisible(bool visible) => _meshObject.SetActive(visible);
            public bool IsVisible() => _meshObject.activeSelf;
        }

        // Holds a mesh for one LOD level (requests from mapGenerator on demand, caches result)
        class LODMesh
        {
            public Mesh Mesh;
            public bool HasRequestedMesh;
            public bool HasMesh;

            int _lod;
            System.Action _updateCallback;

            public LODMesh(int lod, System.Action updateCallback)
            {
                _lod = lod;
                _updateCallback = updateCallback;
            }

            public void RequestMesh(MapData mapData)
            {
                HasRequestedMesh = true;
                mapGenerator.RequestMeshData(mapData, _lod, OnMeshDataReceived);
            }

            void OnMeshDataReceived(MeshData meshData)
            {
                Mesh = meshData.CreateMesh();
                HasMesh = true;
                _updateCallback();
            }
        }
    }

    [System.Serializable]
    public struct LODInfo
    {
        public float distanceFraction;
        public int lod;
    }

}
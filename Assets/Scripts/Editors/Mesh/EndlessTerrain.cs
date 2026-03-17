using UnityEngine;
using System.Collections.Generic;

namespace LevelGenerator
{
    public class EndlessTerrain : MonoBehaviour
    {
        public static float MaxViewDist = 300f;
        public Transform Viewer;
        public LevelGeneratorCommon Common;
        public Material TerrainMaterial;

        public static Vector2 ViewerPosition;

        int _chunkSize;
        int _chunksVisibleInViewDst;

        Dictionary<Vector2, TerrainChunk> _terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
        List<TerrainChunk> _terrainChunksVisibleLastUpdate = new List<TerrainChunk>();

        void Start()
        {
            _chunkSize              = MapGenerator.mapChunkSize - 1;
            _chunksVisibleInViewDst = Mathf.RoundToInt(MaxViewDist / _chunkSize);
        }

        void Update()
        {
            ViewerPosition = new Vector2(Viewer.position.x, Viewer.position.z);
            UpdateVisibleChunks();
        }

        void UpdateVisibleChunks()
        {
            for (int i = 0; i < _terrainChunksVisibleLastUpdate.Count; i++)
                _terrainChunksVisibleLastUpdate[i].SetVisible(false);
            _terrainChunksVisibleLastUpdate.Clear();

            int currentChunkCoordX = Mathf.RoundToInt(ViewerPosition.x / _chunkSize);
            int currentChunkCoordY = Mathf.RoundToInt(ViewerPosition.y / _chunkSize);

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
                        var chunk = new TerrainChunk(viewedChunkCoord, _chunkSize, transform, Common, TerrainMaterial);
                        _terrainChunkDictionary.Add(viewedChunkCoord, chunk);
                    }
                }
            }
        }

        public class TerrainChunk
        {
            GameObject _meshObject;
            Vector2    _position;
            Bounds     _bounds;

            public TerrainChunk(Vector2 coord, int size, Transform parent, LevelGeneratorCommon common, Material material)
            {
               
                _position = coord * size;
                _bounds   = new Bounds(_position, Vector2.one * size);

                Vector3 positionV3 = new Vector3(_position.x, 0, _position.y);

                // Create the chunk as a child of the EndlessTerrain object
                _meshObject = new GameObject($"Chunk_{coord.x}_{coord.y}");
                _meshObject.transform.parent        = parent;
                _meshObject.transform.position = positionV3;
                _meshObject.transform.localScale = Vector3.one;

                var meshFilter   = _meshObject.AddComponent<MeshFilter>();
                var meshRenderer = _meshObject.AddComponent<MeshRenderer>();
                var mapDisplay   = _meshObject.AddComponent<MapDisplay>();

                meshRenderer.material = new Material(material);

                mapDisplay.MeshFilter    = meshFilter;
                mapDisplay.MeshRenderer  = meshRenderer;
                mapDisplay.TextureRenderer = meshRenderer;

                // Generate the chunk
                var mapGen        = _meshObject.AddComponent<MapGenerator>();
                mapGen.NoiseOffset = coord;
                mapGen.Common     = common;
                mapGen.meshHeightMultiplier = common.HeightMultiplier;
                mapGen.meshHeightCurve = common.HeightCurve;
                mapGen.drawMode   = MapGenerator.DrawMode.Mesh;
                mapGen.GenerateMap();

                SetVisible(false);
            }

            public void UpdateTerrainChunk()
            {
                float viewerDstFromNearestEdge = Mathf.Sqrt(_bounds.SqrDistance(ViewerPosition));
                bool  visible                  = viewerDstFromNearestEdge <= MaxViewDist;
                SetVisible(visible);
            }

            public void SetVisible(bool visible)  => _meshObject.SetActive(visible);
            public bool IsVisible()               => _meshObject.activeSelf;
        }
    }
}

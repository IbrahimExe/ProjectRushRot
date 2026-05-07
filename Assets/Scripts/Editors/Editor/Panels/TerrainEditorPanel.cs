#if UNITY_EDITOR
 
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditorInternal;

namespace Level.Editor
{
    public class TerrainEditorPanel
    {
        TerrainConfig _config;
        SerializedObject _so;
        TerrainConfig _runtimeConfig;
        ReorderableList _reorderableList;
        LevelGeneratorCommon _common;
        SerializedObject _soCommon;

        bool _hasUnsavedChanges = false;
        List<bool> _foldouts = new List<bool>();

        public event System.Action OnRepaintNeeded;
        public event System.Action OnPreviewDirty;
        public TerrainConfig RuntimeConfig => _runtimeConfig;
        public TerrainConfig Config => _config;

        public void ApplyChanges()
        {
            _so.ApplyModifiedProperties();
            _soCommon?.ApplyModifiedProperties();
        }

        public void OnEnable()
        {
            _runtimeConfig = ScriptableObject.CreateInstance<TerrainConfig>();
            _so = new SerializedObject(_runtimeConfig);
        }

        public void SetCommon(LevelGeneratorCommon common)
        {
            _common = common;
            _soCommon = common != null ? new SerializedObject(common) : null;
        }
        public void OnDisable()
        {
            if (_runtimeConfig != null) Object.DestroyImmediate(_runtimeConfig);
        }

        public void Draw(float windowWidth)
        {
            if (_runtimeConfig == null)
            {
                _runtimeConfig = ScriptableObject.CreateInstance<TerrainConfig>();
                _runtimeConfig.name = "TerrainConfig_Runtime";
                _so = new SerializedObject(_runtimeConfig);
                _reorderableList = null; 
                if (_config != null)
                    EditorUtility.CopySerializedIfDifferent(_config, _runtimeConfig);
            }
            else if (_so == null || _so.targetObject == null)
            {
                _so = new SerializedObject(_runtimeConfig);
                _reorderableList = null; 
            }

            EditorGUILayout.Space(4);

            EditorGUI.BeginChangeCheck();
            var newConfig = (TerrainConfig)EditorGUILayout.ObjectField(
                "Load from Config", _config, typeof(TerrainConfig), false);
            if (EditorGUI.EndChangeCheck() && newConfig != _config)
            {
                bool proceed = !_hasUnsavedChanges || EditorUtility.DisplayDialog(
                    "Load Terrain Config",
                    "You have unsaved changes. Loading will discard them.",
                    "Discard & Load", "Cancel");
                if (proceed) LoadConfig(newConfig);
            }

            _so.Update();

            EditorGUI.BeginChangeCheck();

            if (_soCommon != null)
            {
                Separator("Chunk Dimensions");
                _soCommon.Update();

                var chunkWidthProp = _soCommon.FindProperty("ChunkWidth");
                var chunkLengthProp = _soCommon.FindProperty("ChunkLength");
                var vertexResProp = _soCommon.FindProperty("VertexResolution");
                var heightMultProp = _soCommon.FindProperty("HeightMultiplier");
                var heightCurveProp = _soCommon.FindProperty("HeightCurve");

                if (chunkWidthProp != null) EditorGUILayout.PropertyField(chunkWidthProp, new GUIContent("Chunk Width"));
                if (chunkLengthProp != null) EditorGUILayout.PropertyField(chunkLengthProp, new GUIContent("Chunk Length"));
                if (vertexResProp != null) EditorGUILayout.PropertyField(vertexResProp, new GUIContent("Vertex Resolution"));
                if (heightMultProp != null) EditorGUILayout.PropertyField(heightMultProp, new GUIContent("Height Multiplier"));
                if (heightCurveProp != null) EditorGUILayout.PropertyField(heightCurveProp, new GUIContent("Height Curve"));

                if (_soCommon.ApplyModifiedProperties())
                    EditorUtility.SetDirty(_common);
            }
            EditorGUILayout.Space(6);

            Separator("Terrain Regions");
            EnsureReorderableList();
            _reorderableList.DoLayoutList();

            bool changed = EditorGUI.EndChangeCheck();
            _so.ApplyModifiedPropertiesWithoutUndo();
            if (changed)
            {
                _hasUnsavedChanges = true;
                OnPreviewDirty?.Invoke();
            }

            if (_hasUnsavedChanges)
                EditorGUILayout.HelpBox("Unsaved changes — use Save As New or Update Loaded.", MessageType.Warning);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save As New"))
            {
                var path = EditorUtility.SaveFilePanelInProject(
                    "Save Terrain Config", "TerrainConfig", "asset", "Choose location");
                if (!string.IsNullOrEmpty(path))
                {
                    var copy = Object.Instantiate(_runtimeConfig);
                    AssetDatabase.CreateAsset(copy, path);
                    AssetDatabase.SaveAssets();
                    LoadConfig(copy);
                }
            }
            using (new EditorGUI.DisabledGroupScope(_config == null))
            {
                if (GUILayout.Button("Update Loaded"))
                {
                    EditorUtility.CopySerializedIfDifferent(_runtimeConfig, _config);
                    EditorUtility.SetDirty(_config);
                    AssetDatabase.SaveAssets();
                    _hasUnsavedChanges = false;
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        // Called by LevelEditor to composite the terrain color overlay onto the noise texture.
        // Returns a new texture with biome colors blended over the grayscale noise.
        public Texture2D BuildPreviewTexture(Texture2D noiseTexture)
        {
            if (noiseTexture == null || _runtimeConfig.Regions == null || _runtimeConfig.Regions.Count == 0)
                return noiseTexture;

            int w = noiseTexture.width;
            int h = noiseTexture.height;

            var result = new Texture2D(w, h, TextureFormat.RGB24, false)
            { filterMode = noiseTexture.filterMode };

            var srcPixels = noiseTexture.GetPixels();
            var dstPixels = new Color[srcPixels.Length];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    float noiseVal = srcPixels[idx].r;
                    Color final = srcPixels[idx];

                    for (int i = 0; i < _runtimeConfig.Regions.Count; i++)
                    {
                        var region = _runtimeConfig.Regions[i];

                        if (noiseVal <= region.Height)
                        {
                            if (region.Color.a > 0f)
                            {
                                float regionStart = i > 0 ? _runtimeConfig.Regions[i - 1].Height : 0f;
                                float regionRange = region.Height - regionStart;
                                float blendEnd = regionStart + regionRange * region.BlendWidth;

                                // Source color: previous region's color, or noise if previous has no color
                                Color sourceColor = srcPixels[idx];
                                if (i > 0 && _runtimeConfig.Regions[i - 1].Color.a > 0f)
                                    sourceColor = _runtimeConfig.Regions[i - 1].Color;

                                float t = 1f;
                                if (region.BlendWidth > 0f && noiseVal < blendEnd)
                                {
                                    float raw = Mathf.InverseLerp(regionStart, blendEnd, noiseVal);

                                    switch (region.BlendMode)
                                    {
                                        case TerrainBlendMode.Linear:
                                            t = raw;
                                            break;
                                        case TerrainBlendMode.Smoothstep:
                                            t = Mathf.SmoothStep(0f, 1f, raw);
                                            break;
                                        case TerrainBlendMode.BayerOrdered:
                                            int matSize = region.BlendSettings.BayerMatrixSize;
                                            float threshold = BayerThreshold(x % matSize, y % matSize, matSize);
                                            t = raw > threshold * region.BlendSettings.BayerStrength ? 1f : 0f;
                                            break;
                                        //case TerrainBlendMode.BlueNoise:
                                        //    if (region.BlendSettings.BlueNoiseTexture != null)
                                        //    {
                                        //        int bw = region.BlendSettings.BlueNoiseTexture.width;
                                        //        int bh = region.BlendSettings.BlueNoiseTexture.height;
                                        //        float bn = region.BlendSettings.BlueNoiseTexture.GetPixel(x % bw, y % bh).r;
                                        //        t = raw > bn * region.BlendSettings.BlueNoiseStrength ? 1f : 0f;
                                        //    }
                                        //    break;
                                        case TerrainBlendMode.VoronoiJitter:
                                            t = VoronoiThreshold(x, y, raw, region.BlendSettings);
                                            break;
                                    }
                                }

                                final = Color.Lerp(sourceColor, region.Color, t * region.Color.a);
                            }
                            break;
                        }
                    }

                    dstPixels[idx] = final;
                }
            }

            result.SetPixels(dstPixels);
            result.Apply();
            return result;
        }

        public void LoadConfig(TerrainConfig cfg)
        {
            _config = cfg;
            if (cfg != null)
                EditorUtility.CopySerializedIfDifferent(cfg, _runtimeConfig);
            _hasUnsavedChanges = false;
            _reorderableList = null;
            OnPreviewDirty?.Invoke();
            OnRepaintNeeded?.Invoke();
        }

        public bool TryWarnUnsaved()
        {
            if (!_hasUnsavedChanges) return true;
            return EditorUtility.DisplayDialog(
                "Unsaved Terrain Changes",
                "You have unsaved changes that will be lost. Discard them?",
                "Discard", "Cancel");
        }

        static void Separator(string label)
        {
            EditorGUILayout.Space(8);
            Rect r = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(r, new Color(0.35f, 0.35f, 0.35f, 0.6f));
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        }

        static float BayerThreshold(int x, int y, int size)
        {
            // 2x2 and 4x4 Bayer matrices, falls back to 2x2 for unsupported sizes
            int[,] bayer2 = { { 0, 2 }, { 3, 1 } };
            int[,] bayer4 = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };

            if (size <= 2) return bayer2[x % 2, y % 2] / 4f;
            return bayer4[x % 4, y % 4] / 16f;
        }

        static float VoronoiThreshold(int x, int y, float raw, TerrainBlendSettings s)
        {
            Vector2 p = new Vector2(x, y) / (16f / s.VoronoiCells);
            Vector2 cell = new Vector2(Mathf.Floor(p.x), Mathf.Floor(p.y));
            float minDist = float.MaxValue;

            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    Vector2 nb = cell + new Vector2(dx, dy);
                    float rx = Mathf.Sin(nb.x * 127.1f + nb.y * 311.7f + 1.4f) * 43758.5453f;
                    float ry = Mathf.Sin(nb.x * 269.5f + nb.y * 183.3f + 1.4f) * 43758.5453f;
                    rx -= Mathf.Floor(rx); ry -= Mathf.Floor(ry);
                    Vector2 pt = nb + new Vector2(rx, ry) * s.VoronoiJitter;
                    float d = Vector2.Distance(p, pt);
                    if (d < minDist) minDist = d;
                }

            return raw > minDist ? 1f : 0f;
        }

    void EnsureReorderableList()
        {
            if (_reorderableList != null) return;

            _reorderableList = new ReorderableList(_so, _so.FindProperty("Regions"), true, true, true, true);

            _reorderableList.drawHeaderCallback = rect =>
                EditorGUI.LabelField(rect, "Terrain Regions");

            _reorderableList.drawElementCallback = (rect, index, active, focused) =>
            {
                var element = _reorderableList.serializedProperty.GetArrayElementAtIndex(index);
                var nameProp = element.FindPropertyRelative("Name");
                var heightProp = element.FindPropertyRelative("Height");
                var colorProp = element.FindPropertyRelative("Color");
                var blendModeProp = element.FindPropertyRelative("BlendMode");
                var blendWidthProp = element.FindPropertyRelative("BlendWidth");
                var textureProp = element.FindPropertyRelative("Texture");
                var blendSettingsProp = element.FindPropertyRelative("BlendSettings");

                // sync foldout list size
                while (_foldouts.Count <= index) _foldouts.Add(false);

                // header row inside element
                Rect headerRect = new Rect(rect.x, rect.y + 2, rect.width - 20, EditorGUIUtility.singleLineHeight);
                Rect swatchRect = new Rect(rect.x, rect.y + 2, 14, EditorGUIUtility.singleLineHeight);
                Rect foldRect = new Rect(rect.x + 18, rect.y + 2, rect.width - 60, EditorGUIUtility.singleLineHeight);
                Rect heightRect = new Rect(rect.xMax - 38, rect.y + 2, 38, EditorGUIUtility.singleLineHeight);

                EditorGUI.DrawRect(swatchRect, colorProp.colorValue);
                _foldouts[index] = EditorGUI.Foldout(foldRect,
                    _foldouts[index],
                    string.IsNullOrEmpty(nameProp.stringValue) ? $"Region {index}" : nameProp.stringValue,
                    true);
                EditorGUI.LabelField(heightRect, $"≤ {heightProp.floatValue:F2}", EditorStyles.miniLabel);

                if (_foldouts[index])
                {
                    float y = rect.y + EditorGUIUtility.singleLineHeight + 4;
                    float lineH = EditorGUIUtility.singleLineHeight + 2;

                    EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, EditorGUIUtility.singleLineHeight), nameProp, new GUIContent("Name")); y += lineH;
                    EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, EditorGUIUtility.singleLineHeight), heightProp, new GUIContent("Height")); y += lineH;
                    EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, EditorGUIUtility.singleLineHeight), colorProp, new GUIContent("Color")); y += lineH;
                    EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, EditorGUIUtility.singleLineHeight), blendWidthProp, new GUIContent("Blend Width")); y += lineH;
                    EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, EditorGUIUtility.singleLineHeight), blendModeProp, new GUIContent("Blend Mode")); y += lineH;

                    switch ((TerrainBlendMode)blendModeProp.enumValueIndex)
                    {
                        case TerrainBlendMode.BayerOrdered:
                            EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, EditorGUIUtility.singleLineHeight), blendSettingsProp.FindPropertyRelative("BayerMatrixSize"), new GUIContent("Matrix Size")); y += lineH;
                            EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, EditorGUIUtility.singleLineHeight), blendSettingsProp.FindPropertyRelative("BayerStrength"), new GUIContent("Strength")); y += lineH;
                            break;
                        //case TerrainBlendMode.BlueNoise:
                        //    EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, EditorGUIUtility.singleLineHeight), blendSettingsProp.FindPropertyRelative("BlueNoiseTexture"), new GUIContent("Texture")); y += lineH;
                        //    EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, EditorGUIUtility.singleLineHeight), blendSettingsProp.FindPropertyRelative("BlueNoiseStrength"), new GUIContent("Strength")); y += lineH;
                        //    break;
                        case TerrainBlendMode.VoronoiJitter:
                            EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, EditorGUIUtility.singleLineHeight), blendSettingsProp.FindPropertyRelative("VoronoiJitter"), new GUIContent("Jitter")); y += lineH;
                            EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, EditorGUIUtility.singleLineHeight), blendSettingsProp.FindPropertyRelative("VoronoiCells"), new GUIContent("Cells")); y += lineH;
                            break;
                    }

                    EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, EditorGUIUtility.singleLineHeight), textureProp, new GUIContent("Texture (future)"));
                }
            };

            _reorderableList.elementHeightCallback = index =>
            {
                if (index >= _foldouts.Count || !_foldouts[index])
                    return EditorGUIUtility.singleLineHeight + 4;

                var element = _reorderableList.serializedProperty.GetArrayElementAtIndex(index);
                var blendModeProp = element.FindPropertyRelative("BlendMode");
                float lineH = EditorGUIUtility.singleLineHeight + 2;

                int extraLines = (TerrainBlendMode)blendModeProp.enumValueIndex switch
                {
                    TerrainBlendMode.BayerOrdered => 2,
                    //TerrainBlendMode.BlueNoise => 2,
                    TerrainBlendMode.VoronoiJitter => 2,
                    _ => 0
                };

                return lineH + (6 + extraLines) * lineH;
            };

            _reorderableList.onReorderCallback = list => OnPreviewDirty?.Invoke();
        }

    }
}
#endif

#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Level.Editor
{
    /// <summary>
    /// Tab 4 — Final Preview.
    /// Runs the full world generation pipeline per pixel:
    ///   Pass 1: WorldNoise → ocean mask (blue)
    ///   Pass 2: WorldGenerator Voronoi → biome cell assignment
    ///   Pass 3: Border blend → lerp between two nearest biome colors
    /// Uses BiomeEntry.PreviewColor for each cell.
    /// Controls exposed: all generation parameters for quick tuning.
    /// </summary>
    public class WorldPreviewPanel
    {
        public event Action OnRepaintNeeded;
        public bool PreviewDirty { get; set; } = true;

        float _previewWorldScale = 50000f;
        bool  _generating        = false;

        const int TEX = 256;
        Texture2D _tex;

        static readonly Color OCEAN_COLOR = new Color(0.08f, 0.18f, 0.38f);

        public void OnEnable()  { PreviewDirty = true; }
        public void OnDisable() { if (_tex != null) UnityEngine.Object.DestroyImmediate(_tex); }

        // ── Left panel ────────────────────────────────────────────────────────

        public void Draw(WorldConfig config, SerializedObject so)
        {
            if (config == null || so == null) return;

            Label("Preview Scale");
            EditorGUI.BeginChangeCheck();
            _previewWorldScale = EditorGUILayout.FloatField("World Scale", _previewWorldScale);
            if (EditorGUI.EndChangeCheck()) MarkDirty();

            Label("Ocean");
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(so.FindProperty("WorldNoise"),  new GUIContent("World Noise"),  true);
            EditorGUILayout.PropertyField(so.FindProperty("OceanLevel"),  new GUIContent("Ocean Level"));
            if (EditorGUI.EndChangeCheck()) MarkDirty();

            Label("Voronoi");
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(so.FindProperty("CellSize"),                 new GUIContent("Cell Size"));
            EditorGUILayout.PropertyField(so.FindProperty("BorderWidth"),              new GUIContent("Border Width"));
            EditorGUILayout.PropertyField(so.FindProperty("BiomeDistortionFrequency"), new GUIContent("Distortion Freq"));
            EditorGUILayout.PropertyField(so.FindProperty("BiomeDistortionStrength"),  new GUIContent("Distortion Strength"));
            if (EditorGUI.EndChangeCheck()) MarkDirty();

            Label("Climate");
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(so.FindProperty("TemperatureNoise"), new GUIContent("Temperature"), true);
            EditorGUILayout.PropertyField(so.FindProperty("HumidityNoise"),    new GUIContent("Humidity"),    true);
            if (EditorGUI.EndChangeCheck()) MarkDirty();

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Regenerate Preview"))
            {
                MarkDirty();
                OnRepaintNeeded?.Invoke();
            }

            // Legend
            if (config.Biomes.Count > 0)
            {
                EditorGUILayout.Space(6);
                Label("Biome Legend");
                foreach (var b in config.Biomes)
                {
                    EditorGUILayout.BeginHorizontal();
                    Rect sr = GUILayoutUtility.GetRect(14, 14, GUILayout.Width(14), GUILayout.Height(14));
                    EditorGUI.DrawRect(sr, b.PreviewColor);
                    EditorGUILayout.LabelField(b.Name, EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();
                }

                // Ocean entry
                EditorGUILayout.BeginHorizontal();
                Rect or2 = GUILayoutUtility.GetRect(14, 14, GUILayout.Width(14), GUILayout.Height(14));
                EditorGUI.DrawRect(or2, OCEAN_COLOR);
                EditorGUILayout.LabelField("Ocean", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }
        }

        // ── Preview texture ───────────────────────────────────────────────────

        public Texture2D BuildPreviewTexture(WorldConfig config)
        {
            if (!PreviewDirty && _tex != null) return _tex;
            if (config == null) return _tex;

            if (_tex != null) UnityEngine.Object.DestroyImmediate(_tex);
            _tex = new Texture2D(TEX, TEX, TextureFormat.RGB24, false)
                { filterMode = FilterMode.Bilinear };

            var   pixels = new Color[TEX * TEX];
            float scale  = _previewWorldScale;

            for (int py = 0; py < TEX; py++)
            {
                for (int px = 0; px < TEX; px++)
                {
                    float wx = (px / (float)TEX) * scale;
                    float wz = (py / (float)TEX) * scale;

                    pixels[py * TEX + px] = SampleWorldColor(config, wx, wz);
                }
            }

            _tex.SetPixels(pixels);
            _tex.Apply();
            PreviewDirty = false;
            return _tex;
        }

        // ── Per-pixel pipeline ────────────────────────────────────────────────

        static Color SampleWorldColor(WorldConfig config, float wx, float wz)
        {
            // Pass 1 — ocean mask
            float worldNoise = ClimateSampler.Sample01(config.WorldNoise, wx, wz);
            if (worldNoise < config.OceanLevel)
                return OCEAN_COLOR;

            if (config.Biomes == null || config.Biomes.Count == 0)
                return Color.grey;

            // Pass 2+3 — Voronoi + border blend (mirrors WorldGenerator logic)
            float cellSize = config.CellSize;

            // Distortion
            float distFreq = config.BiomeDistortionFrequency;
            float distX = (Mathf.PerlinNoise(
                (wx + 10000f) * distFreq,
                (wz + 10000f) * distFreq) - 0.5f) * config.BiomeDistortionStrength;
            float distZ = (Mathf.PerlinNoise(
                (wx + 11739f) * distFreq,
                (wz + 13319f) * distFreq) - 0.5f) * config.BiomeDistortionStrength;

            Vector2 distorted = new Vector2(wx + distX, wz + distZ);

            int baseCX = Mathf.FloorToInt(distorted.x / cellSize);
            int baseCY = Mathf.FloorToInt(distorted.y / cellSize);

            float nearestDist = float.MaxValue;
            float secondDist  = float.MaxValue;
            int   nearestCX   = baseCX, nearestCY = baseCY;
            int   secondCX    = baseCX, secondCY  = baseCY;

            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    int cx = baseCX + i;
                    int cy = baseCY + j;
                    Vector2 pt = GetCellPoint(cx, cy, cellSize, config.Seed);
                    float   d  = Vector2.Distance(distorted, pt);

                    if (d < nearestDist)
                    {
                        secondDist = nearestDist; secondCX = nearestCX; secondCY = nearestCY;
                        nearestDist = d;          nearestCX = cx;       nearestCY = cy;
                    }
                    else if (d < secondDist)
                    {
                        secondDist = d; secondCX = cx; secondCY = cy;
                    }
                }
            }

            Color primaryColor   = GetCellColor(nearestCX, nearestCY, cellSize, config);
            float borderDistance = secondDist - nearestDist;

            if (borderDistance < config.BorderWidth)
            {
                float t            = 0.5f - (borderDistance / config.BorderWidth * 0.5f);
                Color secondColor  = GetCellColor(secondCX, secondCY, cellSize, config);
                return Color.Lerp(primaryColor, secondColor, t);
            }

            return primaryColor;
        }

        static readonly Color DEBUG_UNASSIGNED = new Color(1f, 0f, 1f); // magenta

        static Color GetCellColor(int cellX, int cellY, float cellSize, WorldConfig config)
        {
            float cx = (cellX + 0.5f) * cellSize;
            float cz = (cellY + 0.5f) * cellSize;
            float temp = ClimateSampler.Sample(config.TemperatureNoise, cx, cz);
            float hum = ClimateSampler.Sample(config.HumidityNoise, cx, cz);

            BiomeEntry entry = config.GetNearestBiome(temp, hum);
            if (entry == null) return DEBUG_UNASSIGNED;
            if (entry.Config == null) return DEBUG_UNASSIGNED; // no LevelGeneratorCommon assigned
            return entry.PreviewColor;
        }

        static Vector2 GetCellPoint(int cellX, int cellY, float cellSize, int seed)
        {
            uint  h  = HashCell(cellX, cellY, seed);
            float rx = (h & 0xFFFF) / (float)0x10000;
            float ry = ((h >> 16) & 0xFFFF) / (float)0x10000;
            return new Vector2((cellX + rx) * cellSize, (cellY + ry) * cellSize);
        }

        static uint Hash(uint x)
        {
            x ^= x >> 16; x *= 0x7feb352d;
            x ^= x >> 15; x *= 0x846ca68b;
            x ^= x >> 16; return x;
        }

        static uint HashCell(int gx, int gy, int seed)
        {
            uint h = (uint)seed;
            h ^= Hash((uint)gx);
            h ^= Hash((uint)gy);
            return Hash(h);
        }

        void MarkDirty() { PreviewDirty = true; OnRepaintNeeded?.Invoke(); }

        static void Label(string text)
        {
            EditorGUILayout.Space(6);
            Rect r = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(r, new Color(0.35f, 0.35f, 0.35f, 0.6f));
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
        }
    }
}
#endif

#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Level.Editor
{
    public class BiomesPanel
    {
        public event Action OnRepaintNeeded;
        public bool PreviewDirty { get; set; } = true;

        public int  SelectedIndex { get; private set; } = -1;
        public int  DragIndex     { get; private set; } = -1;

        const int TEX = 256;
        Texture2D _voronoiTex;

        public void OnEnable()  { PreviewDirty = true; }
        public void OnDisable() { if (_voronoiTex != null) UnityEngine.Object.DestroyImmediate(_voronoiTex); }

        // ── Left panel draw ───────────────────────────────────────────────────

        public void Draw(WorldConfig config, SerializedObject so)
        {
            if (config == null || so == null) return;

            Label("Voronoi Settings");
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(so.FindProperty("CellSize"),                new GUIContent("Cell Size"));
            EditorGUILayout.PropertyField(so.FindProperty("BorderWidth"),             new GUIContent("Border Width"));
            EditorGUILayout.PropertyField(so.FindProperty("BiomeDistortionFrequency"),new GUIContent("Distortion Freq"));
            EditorGUILayout.PropertyField(so.FindProperty("BiomeDistortionStrength"), new GUIContent("Distortion Strength"));
            if (EditorGUI.EndChangeCheck()) MarkDirty();

            EditorGUILayout.Space(6);
            Label("Biomes");
            DrawBiomeList(config);

            if (SelectedIndex >= 0 && SelectedIndex < config.Biomes.Count)
                DrawBiomeDetails(config, SelectedIndex);
        }

        void DrawBiomeList(WorldConfig config)
        {
            var biomes = config.Biomes;
            for (int i = 0; i < biomes.Count; i++)
            {
                bool sel   = SelectedIndex == i;
                var  entry = biomes[i];

                EditorGUILayout.BeginHorizontal(sel ? EditorStyles.helpBox : GUIStyle.none);

                Rect sr = GUILayoutUtility.GetRect(14, 14, GUILayout.Width(14), GUILayout.Height(14));
                EditorGUI.DrawRect(sr, entry.PreviewColor);

                if (GUILayout.Button(
                    string.IsNullOrEmpty(entry.Name) ? $"Biome {i}" : entry.Name,
                    EditorStyles.label, GUILayout.ExpandWidth(true)))
                {
                    SelectedIndex = i;
                    GUI.FocusControl(null);
                }

                if (GUILayout.Button("✕", GUILayout.Width(20)))
                {
                    biomes.RemoveAt(i);
                    if (SelectedIndex >= biomes.Count) SelectedIndex = biomes.Count - 1;
                    MarkDirty();
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add Biome"))
            {
                biomes.Add(new BiomeEntry
                {
                    Name         = $"Biome {biomes.Count}",
                    PreviewColor = Color.HSVToRGB((biomes.Count * 0.17f) % 1f, 0.6f, 0.8f),
                    Weight       = 1f
                });
                SelectedIndex = biomes.Count - 1;
                MarkDirty();
            }
        }

        void DrawBiomeDetails(WorldConfig config, int idx)
        {
            var entry = config.Biomes[idx];
            EditorGUILayout.Space(4);
            Label($"— {(string.IsNullOrEmpty(entry.Name) ? $"Biome {idx}" : entry.Name)}");

            EditorGUI.BeginChangeCheck();
            entry.Name         = EditorGUILayout.TextField("Name",   entry.Name);
            entry.Config       = (LevelGeneratorCommon)EditorGUILayout.ObjectField(
                "Config", entry.Config, typeof(LevelGeneratorCommon), false);
            entry.Weight       = EditorGUILayout.Slider("Weight / Influence", entry.Weight, 0.01f, 2f);
            entry.PreviewColor = EditorGUILayout.ColorField("Preview Color", entry.PreviewColor);

            Vector2 cp         = EditorGUILayout.Vector2Field("Climate Position", entry.ClimatePosition);
            entry.ClimatePosition = new Vector2(
                Mathf.Clamp(cp.x, -1f, 1f), Mathf.Clamp(cp.y, -1f, 1f));

            if (EditorGUI.EndChangeCheck())
            {
                config.Biomes[idx] = entry;
                MarkDirty();
                OnRepaintNeeded?.Invoke();
            }
        }

        // ── Right panel — graph + nodes ───────────────────────────────────────

        public void DrawGraph(WorldConfig config, float w, float h)
        {
            if (config == null) return;

            if (PreviewDirty || _voronoiTex == null)
            {
                RebuildVoronoi(config);
                PreviewDirty = false;
            }

            float size = Mathf.Min(w, h) - 48f;
            float ox   = (w - size) * 0.5f;
            float oy   = (h - size) * 0.5f + 16f;
            var   gr   = new Rect(ox, oy, size, size);

            // Axis labels
            GUI.Label(new Rect(ox, oy - 18f, size, 16f),
                "← Cold   Temperature   Hot →", EditorStyles.centeredGreyMiniLabel);

            Matrix4x4 mat = GUI.matrix;
            GUIUtility.RotateAroundPivot(-90f, new Vector2(ox - 14f, oy + size * 0.5f));
            GUI.Label(new Rect(ox - 14f - size * 0.5f, oy + size * 0.5f - 8f, size, 16f),
                "← Dry   Humidity   Wet →", EditorStyles.centeredGreyMiniLabel);
            GUI.matrix = mat;

            GUI.DrawTexture(gr, _voronoiTex, ScaleMode.StretchToFill);

            // Border
            Handles.color = new Color(0.5f, 0.5f, 0.5f);
            Handles.DrawLines(new Vector3[] {
                new Vector3(gr.xMin,gr.yMin), new Vector3(gr.xMax,gr.yMin),
                new Vector3(gr.xMax,gr.yMin), new Vector3(gr.xMax,gr.yMax),
                new Vector3(gr.xMax,gr.yMax), new Vector3(gr.xMin,gr.yMax),
                new Vector3(gr.xMin,gr.yMax), new Vector3(gr.xMin,gr.yMin),
            });

            // Axis lines
            Handles.color = new Color(1f, 1f, 1f, 0.15f);
            Handles.DrawLine(new Vector3(gr.xMin + gr.width * 0.5f, gr.yMin),
                             new Vector3(gr.xMin + gr.width * 0.5f, gr.yMax));
            Handles.DrawLine(new Vector3(gr.xMin, gr.yMin + gr.height * 0.5f),
                             new Vector3(gr.xMax, gr.yMin + gr.height * 0.5f));

            HandleNodes(config, gr);
        }

        void HandleNodes(WorldConfig config, Rect gr)
        {
            var   biomes = config.Biomes;
            Event e      = Event.current;

            for (int i = 0; i < biomes.Count; i++)
            {
                var    entry  = biomes[i];
                Vector2 gp   = ToGraph(entry.ClimatePosition, gr);
                float  radius = Mathf.Lerp(6f, 18f, (entry.Weight - 0.01f) / 1.99f);
                var    nr    = new Rect(gp.x - radius, gp.y - radius, radius * 2f, radius * 2f);

                Color border = SelectedIndex == i ? Color.white : new Color(0.1f, 0.1f, 0.1f);
                EditorGUI.DrawRect(
                    new Rect(gp.x - radius - 1f, gp.y - radius - 1f, radius * 2f + 2f, radius * 2f + 2f),
                    border);
                EditorGUI.DrawRect(nr, entry.PreviewColor);
                GUI.Label(new Rect(gp.x - 40f, gp.y + radius + 2f, 80f, 16f),
                    entry.Name, EditorStyles.centeredGreyMiniLabel);

                if (e.type == EventType.MouseDown && nr.Contains(e.mousePosition))
                {
                    DragIndex = SelectedIndex = i;
                    e.Use();
                }
            }

            if (DragIndex >= 0)
            {
                if (e.type == EventType.MouseDrag)
                {
                    var entry = biomes[DragIndex];
                    entry.ClimatePosition  = FromGraph(e.mousePosition, gr);
                    biomes[DragIndex]      = entry;
                    MarkDirty();
                    e.Use(); OnRepaintNeeded?.Invoke();
                }
                else if (e.type == EventType.MouseUp)
                {
                    DragIndex = -1; e.Use();
                }
            }
        }

        // ── Voronoi texture builder ───────────────────────────────────────────

        void RebuildVoronoi(WorldConfig config)
        {
            if (_voronoiTex != null) UnityEngine.Object.DestroyImmediate(_voronoiTex);
            _voronoiTex = new Texture2D(TEX, TEX, TextureFormat.RGB24, false)
                { filterMode = FilterMode.Bilinear };

            var   biomes     = config.Biomes;
            var   pixels     = new Color[TEX * TEX];
            float cellRef    = Mathf.Max(config.CellSize, 1f);
            float normBorder = (config.BorderWidth / cellRef) * 0.5f;

            for (int py = 0; py < TEX; py++)
            {
                for (int px = 0; px < TEX; px++)
                {
                    float temp = (px / (float)(TEX - 1)) * 2f - 1f;
                    float hum  = (py / (float)(TEX - 1)) * 2f - 1f;

                    float nd = float.MaxValue, sd = float.MaxValue;
                    int   ni = -1,             si = -1;

                    for (int b = 0; b < biomes.Count; b++)
                    {
                        float dx    = temp - biomes[b].ClimatePosition.x;
                        float dy    = hum  - biomes[b].ClimatePosition.y;
                        float score = (dx * dx + dy * dy) /
                            Mathf.Max(0.0001f, biomes[b].Weight * biomes[b].Weight);

                        if (score < nd) { sd = nd; si = ni; nd = score; ni = b; }
                        else if (score < sd) { sd = score; si = b; }
                    }

                    Color pixel = Color.black;
                    if (ni >= 0)
                    {
                        Color ca = biomes[ni].PreviewColor;
                        if (si >= 0 && normBorder > 0f)
                        {
                            float bd = Mathf.Sqrt(sd) - Mathf.Sqrt(nd);
                            pixel = bd < normBorder
                                ? Color.Lerp(ca, biomes[si].PreviewColor, 0.5f - bd / normBorder * 0.5f)
                                : ca;
                        }
                        else pixel = ca;
                    }

                    pixels[py * TEX + px] = pixel;
                }
            }

            _voronoiTex.SetPixels(pixels);
            _voronoiTex.Apply();
        }

        // ── Coordinate helpers ────────────────────────────────────────────────

        static Vector2 ToGraph(Vector2 c, Rect gr) => new Vector2(
            gr.xMin + (c.x + 1f) * 0.5f * gr.width,
            gr.yMax - (c.y + 1f) * 0.5f * gr.height);

        static Vector2 FromGraph(Vector2 p, Rect gr) => new Vector2(
            Mathf.Clamp((p.x - gr.xMin) / gr.width  * 2f - 1f, -1f, 1f),
            Mathf.Clamp((gr.yMax - p.y) / gr.height * 2f - 1f, -1f, 1f));

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

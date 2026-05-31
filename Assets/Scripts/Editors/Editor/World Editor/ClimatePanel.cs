#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Level.Editor
{
    public class ClimatePanel
    {
        public event Action OnRepaintNeeded;
        public bool PreviewDirty { get; set; } = true;

        const int TEX = 256;
        Texture2D _combined; // temperature left, humidity right, composited

        public void OnEnable()  { PreviewDirty = true; }
        public void OnDisable() { if (_combined != null) UnityEngine.Object.DestroyImmediate(_combined); }

        public void Draw(WorldConfig config, SerializedObject so)
        {
            if (config == null || so == null) return;

            Label("Climate Noise");
            EditorGUILayout.HelpBox(
                "Two independent Perlin noise layers. Temperature drives the X axis of the " +
                "Whittaker diagram (hot/cold), humidity drives Y (wet/dry). " +
                "Preview is masked by the ocean — dark areas are sea.",
                MessageType.None);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(
                so.FindProperty("TemperatureNoise"), new GUIContent("Temperature"), true);
            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(
                so.FindProperty("HumidityNoise"), new GUIContent("Humidity"), true);
            if (EditorGUI.EndChangeCheck()) MarkDirty();
        }

        // Returns a wide texture: left half = temperature, right half = humidity
        public Texture2D BuildPreviewTexture(WorldConfig config)
        {
            if (!PreviewDirty && _combined != null) return _combined;

            int W = TEX * 2 + 8; // side by side with small gap
            int H = TEX;

            if (_combined != null) UnityEngine.Object.DestroyImmediate(_combined);
            _combined = new Texture2D(W, H, TextureFormat.RGB24, false)
                { filterMode = FilterMode.Bilinear };

            var pixels = new Color[W * H];

            // Fill with dark grey for the gap
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(0.15f, 0.15f, 0.15f);

            var wn   = config?.WorldNoise;
            float ol = config?.OceanLevel ?? 0.4f;

            for (int py = 0; py < H; py++)
            {
                for (int px = 0; px < TEX; px++)
                {
                    float wx = px * 4f;
                    float wz = py * 4f;

                    float world = Sample01(wn, wx, wz);
                    bool  ocean = world <= ol;

                    Color tempC, humC;

                    if (ocean)
                    {
                        tempC = humC = new Color(0.08f, 0.1f, 0.18f);
                    }
                    else
                    {
                        float t  = SampleN11(config?.TemperatureNoise, wx, wz);
                        tempC = t >= 0f
                            ? Color.Lerp(new Color(0.55f, 0.55f, 0.55f), new Color(0.9f, 0.2f, 0.1f), t)
                            : Color.Lerp(new Color(0.1f,  0.3f,  0.9f),  new Color(0.55f, 0.55f, 0.55f), t + 1f);

                        float h  = SampleN11(config?.HumidityNoise, wx, wz);
                        humC = h >= 0f
                            ? Color.Lerp(new Color(0.55f, 0.55f, 0.55f), new Color(0.15f, 0.75f, 0.2f), h)
                            : Color.Lerp(new Color(0.9f,  0.8f,  0.1f),  new Color(0.55f, 0.55f, 0.55f), h + 1f);
                    }

                    // Temperature on left half
                    pixels[py * W + px] = tempC;
                    // Humidity on right half (offset by TEX + gap)
                    pixels[py * W + (px + TEX + 8)] = humC;
                }
            }

            _combined.SetPixels(pixels);
            _combined.Apply();
            PreviewDirty = false;
            return _combined;
        }

        void MarkDirty() { PreviewDirty = true; OnRepaintNeeded?.Invoke(); }

        static float Sample01(ClimateNoiseSettings s, float wx, float wz)
        {
            if (s == null) return 0.5f;
            float v = 0f, amp = 1f, freq = Mathf.Max(0.00001f, s.Frequency), maxV = 0f;
            for (int i = 0; i < s.Octaves; i++)
            {
                v += Mathf.PerlinNoise((wx + s.Offset.x + 10000f) * freq,
                                           (wz + s.Offset.y + 10000f) * freq) * amp;
                maxV += amp;
                amp *= s.Persistence;
                freq *= s.Lacunarity;
            }
            return maxV > 0f ? Mathf.Clamp01(v / maxV) : 0f;
        }

        static float SampleN11(ClimateNoiseSettings s, float wx, float wz)
        {
            if (s == null) return 0f;
            float v = 0f, amp = 1f, freq = Mathf.Max(0.00001f, s.Frequency), maxV = 0f;
            for (int i = 0; i < s.Octaves; i++)
            {
                v    += (Mathf.PerlinNoise((wx + s.Offset.x) * freq, (wz + s.Offset.y) * freq) - 0.5f) * amp;
                maxV += amp * 0.5f;
                amp  *= s.Persistence;
                freq *= s.Lacunarity;
            }
            return maxV > 0f ? Mathf.Clamp(v / maxV, -1f, 1f) : 0f;
        }

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

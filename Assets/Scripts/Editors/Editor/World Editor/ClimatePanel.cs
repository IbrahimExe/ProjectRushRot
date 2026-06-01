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

        float _previewWorldScale = 50000f;
        float _mergeSlider = 0f; // 0 = side by side, 1 = merged

        const int TEX = 256;
        Texture2D _combined;

        public void OnEnable() { PreviewDirty = true; }
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
            _mergeSlider = EditorGUILayout.Slider("Preview Merge", _mergeSlider, 0f, 1f);
            EditorGUILayout.PropertyField(so.FindProperty("TemperatureNoise"), new GUIContent("Temperature"), true);
            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(so.FindProperty("HumidityNoise"), new GUIContent("Humidity"), true);
            _previewWorldScale = EditorGUILayout.FloatField("Preview World Scale", _previewWorldScale);
            if (EditorGUI.EndChangeCheck()) MarkDirty();
        }

        // Returns a wide texture: left half = temperature, right half = humidity
        // Both use ClimateSampler directly — matches runtime exactly
        public Texture2D BuildPreviewTexture(WorldConfig config)
        {
            if (!PreviewDirty && _combined != null) return _combined;

            int W = TEX;
            int H = TEX;

            if (_combined != null) UnityEngine.Object.DestroyImmediate(_combined);
            _combined = new Texture2D(W, H, TextureFormat.RGB24, false)
            { filterMode = FilterMode.Bilinear };

            var pixels = new Color[W * H];
            float ol = config?.OceanLevel ?? 0.4f;

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color(0.15f, 0.15f, 0.15f);

            // At merge=0: two squares side by side
            // At merge=1: one square, colours blended
            // In between: squares slide toward each other and overlap-blend

            for (int py = 0; py < TEX; py++)
            {
                for (int px = 0; px < TEX; px++)
                {
                    float wx = (px / (float)TEX) * _previewWorldScale;
                    float wz = (py / (float)TEX) * _previewWorldScale;

                    float world = ClimateSampler.Sample01(config?.WorldNoise, wx, wz);
                    bool ocean = world <= ol;

                    Color tempC, humC;

                    if (ocean)
                    {
                        tempC = humC = new Color(0.08f, 0.1f, 0.18f);
                    }
                    else
                    {
                        // ClimateSampler.Sample returns -1..1
                        float t = ClimateSampler.Sample(config?.TemperatureNoise, wx, wz);
                        tempC = t >= 0f
                            ? Color.Lerp(new Color(0.55f, 0.55f, 0.55f), new Color(0.9f, 0.2f, 0.1f), t)
                            : Color.Lerp(new Color(0.1f, 0.3f, 0.9f), new Color(0.55f, 0.55f, 0.55f), t + 1f);

                        float h = ClimateSampler.Sample(config?.HumidityNoise, wx, wz);
                        humC = h >= 0f
                            ? Color.Lerp(new Color(0.55f, 0.55f, 0.55f), new Color(0.15f, 0.75f, 0.2f), h)
                            : Color.Lerp(new Color(0.9f, 0.8f, 0.1f), new Color(0.55f, 0.55f, 0.55f), h + 1f);
                    }

                    Color pixel;
                    if (_mergeSlider < 0.01f)
                    {
                        // Split — left = temp, right = hum
                        pixel = px < TEX / 2 ? tempC : humC;
                    }
                    else if (_mergeSlider > 0.99f)
                    {
                        // Fully merged
                        pixel = new Color(
                            (tempC.r + humC.r) * 0.5f,
                            (tempC.g + humC.g) * 0.5f,
                            (tempC.b + humC.b) * 0.5f);
                    }
                    else
                    {
                        // Blend zone — left side fades from temp to merged, right side from hum to merged
                        float splitX = TEX * (1f - _mergeSlider) * 0.5f;        // left boundary
                        float mergedX = TEX * 0.5f;                               // center
                        Color merged = new Color(
                            (tempC.r + humC.r) * 0.5f,
                            (tempC.g + humC.g) * 0.5f,
                            (tempC.b + humC.b) * 0.5f);

                        if (px < mergedX)
                            pixel = Color.Lerp(tempC, merged, Mathf.InverseLerp(splitX, mergedX, px));
                        else
                            pixel = Color.Lerp(humC, merged, Mathf.InverseLerp(TEX - splitX, mergedX, px));
                    }

                    pixels[py * TEX + px] = pixel;
                }
            }

            _combined.SetPixels(pixels);
            _combined.Apply();
            PreviewDirty = false;
            return _combined;
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
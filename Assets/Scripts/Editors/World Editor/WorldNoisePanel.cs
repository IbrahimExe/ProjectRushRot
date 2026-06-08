#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Level.Editor
{
    public class WorldNoisePanel
    {
        public event Action OnRepaintNeeded;
        public bool PreviewDirty { get; set; } = true;

        float _previewWorldScale = 50000f;

        const int TEX = 256;
        Texture2D _tex;

        public void OnEnable() { PreviewDirty = true; }
        public void OnDisable() { if (_tex != null) UnityEngine.Object.DestroyImmediate(_tex); }

        public void Draw(WorldConfig config, SerializedObject so)
        {
            if (config == null || so == null) return;

            Label("World Noise");
            EditorGUI.BeginChangeCheck();
            _previewWorldScale = EditorGUILayout.FloatField("Preview World Scale", _previewWorldScale);
            if (EditorGUI.EndChangeCheck()) MarkDirty();

            EditorGUILayout.HelpBox(
                "Simple Perlin FBM used only to determine land vs ocean. " +
                "Keep frequency very low (0.00001–0.001) for continental-scale shapes.",
                MessageType.None);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(so.FindProperty("WorldNoise"), new GUIContent("World Noise"), true);
            if (EditorGUI.EndChangeCheck()) MarkDirty();

           

            EditorGUILayout.Space(6);
            Label("Ocean");

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(so.FindProperty("OceanLevel"), new GUIContent("Ocean Level"));
            EditorGUILayout.PropertyField(so.FindProperty("OceanConfig"), new GUIContent("Ocean Config"));
            if (EditorGUI.EndChangeCheck()) MarkDirty();
        }

        public Texture2D BuildPreviewTexture(WorldConfig config)
        {
            if (!PreviewDirty && _tex != null) return _tex;

            if (_tex != null) UnityEngine.Object.DestroyImmediate(_tex);
            _tex = new Texture2D(TEX, TEX, TextureFormat.RGB24, false)
            { filterMode = FilterMode.Bilinear };

            var wn = config?.WorldNoise;
            float olvl = config?.OceanLevel ?? 0.4f;
            var pixels = new Color[TEX * TEX];

            for (int py = 0; py < TEX; py++)
            {
                for (int px = 0; px < TEX; px++)
                {
                    float wx = (px / (float)TEX) * _previewWorldScale;
                    float wz = (py / (float)TEX) * _previewWorldScale;

                    // Uses ClimateSampler — same code path as runtime
                    float v = ClimateSampler.Sample01(wn, wx, wz);
                    bool land = v > olvl;
                    float g = land
                        ? Mathf.InverseLerp(olvl, 1f, v) * 0.7f + 0.3f
                        : Mathf.InverseLerp(0f, olvl, v) * 0.25f;

                    pixels[py * TEX + px] = new Color(g, g, g);
                }
            }

            _tex.SetPixels(pixels);
            _tex.Apply();
            PreviewDirty = false;
            return _tex;
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
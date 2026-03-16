#if UNITY_EDITOR
using LevelGenerator.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Level.Editor
{
    public class LevelEditorPreviewPanel : VisualElement
    {
        readonly Image _image;
        public int Resolution { get; private set; } = 128;
        //Pass from TerrainConfig, used to convert footprint size to pixels for the preview
        public float WorldScale = 1f;
        public LevelEditorPreviewPanel()
        {
            style.flexGrow = 1;
            style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));

            // Image fills remaining space after , important otherwise the image will render over the controlls
            _image = new Image { scaleMode = ScaleMode.ScaleToFit };
            _image.style.flexGrow = 1;
            Add(_image);
        }
        public void DrawControls()
        {
            WorldScale = EditorGUILayout.Slider("World Scale", WorldScale, 0.1f, 8f);
        }

        public void UpdatePreview(Texture2D tex)
        {
            _image.image = tex;
           // Debug.Log($"UpdatePreview called with tex: {tex?.width}x{tex?.height}");
        }

        public Texture2D DrawFootprintCircle(Texture2D source, float footprint, float worldWidth)
        {
            if (source == null) return null;

            int w = source.width;
            int h = source.height;

            var result = new Texture2D(w, h, TextureFormat.RGB24, false)
            { filterMode = source.filterMode };

            var pixels = source.GetPixels();

            float radius = (footprint / worldWidth) * (1f / WorldScale) * w * 0.5f;
            float cx = w * 0.5f;
            float cy = h * 0.5f;
            float thickness = Mathf.Max(1f, w * 0.015f / WorldScale);

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (Mathf.Abs(dist - radius) <= thickness)
                        pixels[y * w + x] = Color.white;
                }

            result.SetPixels(pixels);
            result.Apply();
            return result;
        }
    }
}
#endif
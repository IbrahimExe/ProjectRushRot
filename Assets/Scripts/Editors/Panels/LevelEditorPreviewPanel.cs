#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace Level.Editor
{
    public class LevelEditorPreviewPanel : VisualElement
    {
        readonly Image _image;

        public LevelEditorPreviewPanel()
        {
            style.flexGrow = 1;
            style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));

            _image = new Image { scaleMode = ScaleMode.ScaleToFit };
            _image.style.flexGrow = 1;
            Add(_image);
        }

        public void UpdatePreview(Texture2D tex)
        {
            _image.image = tex;
        }
    }
}
#endif
#if UNITY_EDITOR
using UnityEditor;

namespace Level.Editor
{
    public class NoiseEditorWindow : EditorWindow
    {
        NoiseEditorPanel _panel;
        //resolution is passed to the panel so it can draw the preview at the correct size, and also to save it with the correct resolution
        int _resolution = 512;

        [MenuItem("Window/Noise Generator")]
        public static void Open() => GetWindow<NoiseEditorWindow>("Noise Generator");
 

        void OnEnable()
        {
            _panel = new NoiseEditorPanel();
            _panel.OnRepaintNeeded += Repaint;
            _panel.OnEnable();
        }

        void OnDisable()
        {
            _panel.OnDisable();
            _panel.OnRepaintNeeded -= Repaint;
        }

        void OnGUI() => _panel.Draw(position.width, _resolution);

        private void OnDestroy()
        {
            if (!_panel.TryWarnUnsaved())
                EditorApplication.delayCall += () => GetWindow<NoiseEditorWindow>("Noise Generator");
        }
    }
}
#endif
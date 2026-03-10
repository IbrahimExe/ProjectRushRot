#if UNITY_EDITOR
using UnityEditor;

namespace Level.Editor
{
    public class NoiseEditorWindow : EditorWindow
    {
        NoiseEditorPanel _panel;

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

        void OnGUI() => _panel.Draw(position.width);

        private void OnDestroy()
        {
            if (!_panel.TryWarnUnsaved())
                EditorApplication.delayCall += () => GetWindow<NoiseEditorWindow>("Noise Generator");
        }
    }
}
#endif
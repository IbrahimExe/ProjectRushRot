using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Level.Editor
{
    public enum LevelEditorTabs
    {
        Noise,
        Terrain,
        Prefabs,
        Overlays,
        Spawning
    }

    public class LevelEditor : EditorWindow
    {
        private LevelEditorTabs _activeTab = LevelEditorTabs.Noise;

        // Add one field per tab as you migrate each sub-window.
        private LevelEditorPreviewPanel _previewPanel;
        private NoiseEditorPanel _noisePanel;

        //Open 

        [MenuItem("Window/Level Editor")]
        public static void Open() => GetWindow<LevelEditor>("Level Editor");

        private void OnEnable()
        {
            _noisePanel = new NoiseEditorPanel();
            _noisePanel.OnRepaintNeeded += Repaint;
            _noisePanel.OnEnable();
        }

        private void OnDisable()
        {
            _noisePanel.OnDisable();
            _noisePanel.OnRepaintNeeded -= Repaint;
        }

        private void CreateGUI()
        {
            var splitView = new TwoPaneSplitView(0, 340, TwoPaneSplitViewOrientation.Horizontal);
            rootVisualElement.Add(splitView);

            var leftPane = new ScrollView(ScrollViewMode.Vertical);
            splitView.Add(leftPane);

            var imgui = new IMGUIContainer(DrawTabs);
            imgui.style.flexGrow = 1;
            leftPane.Add(imgui);

            _previewPanel = new LevelEditorPreviewPanel();
            splitView.Add(_previewPanel);

            // Noise panel pushes its rebuilt texture into the preview
            _noisePanel.OnPreviewRebuilt += tex => _previewPanel.UpdatePreview(tex);
        }

        private void DrawTabs()
        {
            GUILayout.Space(6);
            var newTab = (LevelEditorTabs)GUILayout.Toolbar(
                (int)_activeTab,
                System.Enum.GetNames(typeof(LevelEditorTabs))
            );
            if (newTab != _activeTab) { _activeTab = newTab; Repaint(); }

            GUILayout.Space(8);
            DrawActiveTabUI(_activeTab);
        }

        private void DrawActiveTabUI(LevelEditorTabs tab)
        {
            switch (tab)
            {
                case LevelEditorTabs.Noise: DrawNoiseTab(); break;
                case LevelEditorTabs.Terrain: DrawTerrainTab(); break;
                case LevelEditorTabs.Prefabs: DrawPrefabsTab(); break;
                case LevelEditorTabs.Overlays: DrawOverlaysTab(); break;
                case LevelEditorTabs.Spawning: DrawSpawningTab(); break;
            }
        }

        private void UpdatePreviewForTab(LevelEditorTabs tab)
        {
            // Placeholder: switch overlay textures / scene previews per tab
        }
        
        private void DrawNoiseTab()
        {
            // All noise UI + preview lives in the panel.
            // Pass position.width so the preview texture sizes correctly.
            _noisePanel.Draw(position.width);
        }

        private void DrawTerrainTab() { }
        private void DrawPrefabsTab() { } 
        private void DrawOverlaysTab() { }
        private void DrawSpawningTab() { }
    }
}
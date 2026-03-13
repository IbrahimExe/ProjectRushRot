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
        private PrefabCatalogPanel _catalogPanel;

        //Open 

        [MenuItem("Window/Level Editor")]
        public static void Open() => GetWindow<LevelEditor>("Level Editor");

        private void OnEnable()
        {
            _noisePanel = new NoiseEditorPanel();
            _noisePanel.OnRepaintNeeded += Repaint;
            _noisePanel.OnEnable();

            _catalogPanel = new PrefabCatalogPanel();
            _catalogPanel.OnRepaintNeeded += Repaint;
            _catalogPanel.OnEnable();
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
            if (newTab != _activeTab)
            {
                if (_activeTab == LevelEditorTabs.Noise && !_noisePanel.TryWarnUnsaved()) return;
                if (_activeTab == LevelEditorTabs.Prefabs && !_catalogPanel.TryWarnUnsaved()) return;
                _activeTab = newTab;
                Repaint();
            }

            GUILayout.Space(8);
            DrawActiveTabUI(_activeTab);
        }

        private void DrawActiveTabUI(LevelEditorTabs tab)
        {
            switch (tab)
            {
                case LevelEditorTabs.Noise:
                    _noisePanel.Draw(position.width, _previewPanel.Resolution); // All noise UI + preview lives in the panel.
                    break;
                case LevelEditorTabs.Terrain:
                  
                    break;
                case LevelEditorTabs.Prefabs:
                    _catalogPanel.Draw(position.width); 
                    Debug.Log("Drawing prefabs tab");
                    break;
                case LevelEditorTabs.Overlays: 
                    DrawOverlaysTab();
                    break;
                case LevelEditorTabs.Spawning: 
                    DrawSpawningTab(); 
                    break;
            }
        }

        private void UpdatePreviewForTab(LevelEditorTabs tab)
        {
            // Placeholder: switch overlay textures / scene previews per tab
        }
        private void DrawPrefabsTab() {
            
        } 
        private void DrawOverlaysTab() { }
        private void DrawSpawningTab() { }
    }
}
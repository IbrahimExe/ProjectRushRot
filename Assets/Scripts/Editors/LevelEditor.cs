using UnityEditor;
using UnityEngine;

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

        [MenuItem("Window/Level Editor")]
        public static void Open()
        {
            GetWindow<LevelEditor>("Level Editor");
        }

        private void OnGUI()
        {
            // Tabs
            GUILayout.Space(6);
            var newTab = (LevelEditorTabs)GUILayout.Toolbar(
                (int)_activeTab,
                System.Enum.GetNames(typeof(LevelEditorTabs))
            );

            // If the tab changed, update preview / overlay / whatever
            if (newTab != _activeTab)
            {
                _activeTab = newTab;

                UpdatePreviewForTab(_activeTab);

                // Repaint this window
                Repaint();
            }

            GUILayout.Space(8);

            // Tab contents
            DrawActiveTabUI(_activeTab);
        }

        private void DrawActiveTabUI(LevelEditorTabs tab)
        {
            switch (tab)
            {
                case LevelEditorTabs.Noise:
                    DrawNoiseTab();
                    break;

                case LevelEditorTabs.Terrain:
                    DrawTerrainTab();
                    break;

                case LevelEditorTabs.Prefabs:
                    DrawPrefbsTabs();
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
            // Placeholder: this is where you'd switch overlay textures,
            // regenerate a RenderTexture, change preview mode, etc.
            // Example debug:
            // Debug.Log($"Switched tab -> {tab}");
        }

        private void DrawNoiseTab()
        {

        }

        private void DrawTerrainTab()
        {
        }

        private void DrawPrefbsTabs()
        {
        }

        private void DrawOverlaysTab()
        {
        }

        private void DrawSpawningTab()
        {
        }
}
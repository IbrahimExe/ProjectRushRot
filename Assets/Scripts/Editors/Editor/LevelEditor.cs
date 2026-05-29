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
        private LevelGeneratorCommon _common;

        private LevelEditorPreviewPanel _previewPanel;
        private NoiseEditorPanel _noisePanel;
        private Texture2D _lastNoiseTexture;
        private PrefabCatalogPanel _catalogPanel;
        private TerrainEditorPanel _terrainPanel;
        private OverlaysPanel _overlaysPanel;
        private SpawningPanel _spawningPanel;

        [MenuItem("Window/Level Editor")]
        public static void Open()
        {
            GetWindow<LevelEditor>("Level Editor");
        }

        private void OnEnable()
        {
            _noisePanel = new NoiseEditorPanel();
            _noisePanel.OnRepaintNeeded += Repaint;
            _noisePanel.OnEnable();

            _catalogPanel = new PrefabCatalogPanel();
            _catalogPanel.OnRepaintNeeded += Repaint;
            _catalogPanel.OnEnable();

            _terrainPanel = new TerrainEditorPanel();
            _terrainPanel.OnRepaintNeeded += Repaint;
            _terrainPanel.OnEnable();
            _terrainPanel.OnPreviewDirty += HandlePreviewDirty;

            _overlaysPanel = new OverlaysPanel();
            _overlaysPanel.OnRepaintNeeded += Repaint;
            _overlaysPanel.OnEnable();

            _spawningPanel = new SpawningPanel();
            _spawningPanel.OnRepaintNeeded += Repaint;
            _spawningPanel.OnEnable();

            _catalogPanel.OnSelectionChanged += HandleSelectionChanged;


            if (_common != null)
            {
                LoadProjectIntoPanels(_common);
            }
        }

        private void OnDisable()
        {
            if (_noisePanel != null)
            {
                _noisePanel.OnDisable();
                _noisePanel.OnRepaintNeeded -= Repaint;
            }

            if (_catalogPanel != null)
            {
                _catalogPanel.OnDisable();
                _catalogPanel.OnRepaintNeeded -= Repaint;
                _catalogPanel.OnSelectionChanged -= HandleSelectionChanged;
            }

            if (_terrainPanel != null)
            {
                _terrainPanel.OnDisable();
                _terrainPanel.OnRepaintNeeded -= Repaint;
                _terrainPanel.OnPreviewDirty -= HandlePreviewDirty;
            }

            if (_overlaysPanel != null)
            {
                _overlaysPanel.OnDisable();
                _overlaysPanel.OnRepaintNeeded -= Repaint;
            }
            if (_spawningPanel != null)
            {
                _spawningPanel.OnDisable();
                _spawningPanel.OnRepaintNeeded -= Repaint;
            }

        }

        private void CreateGUI()
        {
            rootVisualElement.Clear();

            var splitView = new TwoPaneSplitView(0, 340, TwoPaneSplitViewOrientation.Horizontal);
            rootVisualElement.Add(splitView);

            var leftPane = new ScrollView(ScrollViewMode.Vertical);
            splitView.Add(leftPane);

            var imgui = new IMGUIContainer(DrawTabs);
            imgui.style.flexGrow = 1;
            leftPane.Add(imgui);

            _previewPanel = new LevelEditorPreviewPanel();
            splitView.Add(_previewPanel);

            var previewControls = new IMGUIContainer(() =>
            {
                EditorGUI.BeginChangeCheck();
                _previewPanel.DrawControls();
                if (EditorGUI.EndChangeCheck())
                {
                    _noisePanel.SetWorldScale(_previewPanel.WorldScale);
                    _noisePanel.SchedulePreviewRebuild();
                    UpdatePreviewForTab(_activeTab);
                }
            });

            _previewPanel.Insert(0, previewControls);

            _noisePanel.OnPreviewRebuilt += tex =>
            {
                _lastNoiseTexture = tex;
                UpdatePreviewForTab(_activeTab);
            };
        }

        private void DrawTabs()
        {
            EditorGUI.BeginChangeCheck();
            var selectedCommon = (LevelGeneratorCommon)EditorGUILayout.ObjectField(
                "Project",
                _common,
                typeof(LevelGeneratorCommon),
                false);

            if (EditorGUI.EndChangeCheck())
            {
                _common = selectedCommon;

                if (_common != null)
                {
                    LoadProjectIntoPanels(_common);
                }
            }

            GUILayout.Space(6);

            var newTab = (LevelEditorTabs)GUILayout.Toolbar(
                (int)_activeTab,
                System.Enum.GetNames(typeof(LevelEditorTabs)));

            if (newTab != _activeTab)
            {
                _activeTab = newTab;

                if (_lastNoiseTexture == null)
                {
                    _noisePanel.SchedulePreviewRebuild();
                }
                else
                {
                    UpdatePreviewForTab(_activeTab);
                }

                Repaint();
            }

            GUILayout.Space(8);
            DrawActiveTabUI(_activeTab);
            DrawProjectSave();
        }

        private void DrawActiveTabUI(LevelEditorTabs tab)
        {
            switch (tab)
            {
                case LevelEditorTabs.Noise:
                    _noisePanel.Draw(position.width, _previewPanel.WorldScale);
                    break;

                case LevelEditorTabs.Terrain:
                    _terrainPanel.Draw(position.width);
                    break;

                case LevelEditorTabs.Prefabs:
                    _catalogPanel.Draw(position.width);
                    break;

                case LevelEditorTabs.Overlays:
                    _overlaysPanel.Draw(position.width);
                    break;

                case LevelEditorTabs.Spawning:
                    _spawningPanel.Draw(position.width);
                    break;
            }
        }

        private void DrawProjectSave()
        {
            if (_common == null)
                return;

            EditorGUILayout.Space(8);
            Rect divider = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(divider, new Color(0.35f, 0.35f, 0.35f, 0.6f));
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Save Project As New"))
            {
                if (EditorUtility.DisplayDialog(
                    "Save Project",
                    "This will save ALL configs as new assets. Continue?",
                    "Save All",
                    "Cancel"))
                {
                    SaveAllConfigs(true);
                }
            }

            if (GUILayout.Button("Update Project"))
            {
                if (EditorUtility.DisplayDialog(
                    "Update Project",
                    "This will OVERWRITE all loaded configs (Noise, Terrain, Prefabs). Continue?",
                    "Overwrite All",
                    "Cancel"))
                {
                    SaveAllConfigs(false);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void SaveAllConfigs(bool saveAsNew)
        {
            if (_common == null)
                return;

            if (saveAsNew)
            {
                var path = EditorUtility.SaveFilePanelInProject(
                    "Save Project", "NewLevelProject", "asset", "Choose location");
                if (string.IsNullOrEmpty(path)) return;

                var commonCopy = Object.Instantiate(_common);
                commonCopy.name = System.IO.Path.GetFileNameWithoutExtension(path);
                commonCopy.NoiseConfig = _common.NoiseConfig;
                commonCopy.TerrainConfig = _common.TerrainConfig;
                commonCopy.PrefabCatalog = _common.PrefabCatalog;


                AssetDatabase.CreateAsset(commonCopy, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Selection.activeObject = commonCopy;
            }
            else
            {
                var soCommon = new SerializedObject(_common);
                soCommon.Update();

                soCommon.FindProperty("NoiseConfig").objectReferenceValue = _noisePanel.Config;
                soCommon.FindProperty("TerrainConfig").objectReferenceValue = _terrainPanel.Config;
                soCommon.FindProperty("PrefabCatalog").objectReferenceValue = _catalogPanel.LoadedCatalog;
                soCommon.FindProperty("OverlayConfig").objectReferenceValue = _overlaysPanel.Config;
                soCommon.FindProperty("SpawnConfig").objectReferenceValue = _spawningPanel?.Config;
                var spawnProp = soCommon.FindProperty("SpawnConfig");
                if (spawnProp != null)
                    spawnProp.objectReferenceValue = _common.SpawnConfig; // keep existing reference

                soCommon.ApplyModifiedProperties();
                EditorUtility.SetDirty(_common);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Selection.activeObject = _common;
            }
        }

        private void LoadProjectIntoPanels(LevelGeneratorCommon common)
        {
            if (common == null)
                return;

            if (common.NoiseConfig != null)
                _noisePanel.LoadConfig(common.NoiseConfig);

            if (common.TerrainConfig != null)
                _terrainPanel.LoadConfig(common.TerrainConfig);

            if (common.PrefabCatalog != null)
                _catalogPanel.LoadCatalog(common.PrefabCatalog);

            if (common.OverlayConfig != null)
                _overlaysPanel.LoadConfig(common.OverlayConfig);

            if (common.SpawnConfig != null)
                _spawningPanel?.LoadConfig(common.SpawnConfig);

            _terrainPanel.SetCommon(common);
            _spawningPanel.SetCatalog(_catalogPanel.RuntimeCatalog);
            UpdatePreviewForTab(_activeTab);
        }

        private void HandlePreviewDirty()
        {
            UpdatePreviewForTab(_activeTab);
        }

        private void HandleSelectionChanged()
        {
            UpdatePreviewForTab(_activeTab);

           if (_activeTab == LevelEditorTabs.Spawning)
            {
                _spawningPanel.SetCatalog(_catalogPanel.RuntimeCatalog);
            }
        }

        private void UpdatePreviewForTab(LevelEditorTabs tab)
        {
            if (_previewPanel == null || _lastNoiseTexture == null)
                return;

            switch (tab)
            {
                case LevelEditorTabs.Terrain:
                    _previewPanel.UpdatePreview(_terrainPanel.BuildPreviewTexture(_lastNoiseTexture));
                    break;

                case LevelEditorTabs.Prefabs:
                    var terrainTex = _terrainPanel.BuildPreviewTexture(_lastNoiseTexture);
                    var selectedDef = _catalogPanel.SelectedDef;

                    if (selectedDef != null)
                    {
                        _previewPanel.UpdatePreview(
                            _previewPanel.DrawFootprintCircle(
                                terrainTex,
                                selectedDef.Footprint,
                                _common != null ? _common.ChunkWidth : 100f));
                    }
                    else
                    {
                        _previewPanel.UpdatePreview(terrainTex);
                    }

                    break;

                default:
                    _previewPanel.UpdatePreview(_lastNoiseTexture);
                    break;
            }
        }
    }
}
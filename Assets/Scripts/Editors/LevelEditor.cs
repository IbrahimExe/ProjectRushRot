using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using LevelGenerator.Data;

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


        // Add one field per tab as you migrate each sub-window.
        private LevelEditorPreviewPanel _previewPanel;
        private NoiseEditorPanel _noisePanel;
        private Texture2D _lastNoiseTexture;
        private PrefabCatalogPanel _catalogPanel;
        private TerrainEditorPanel _terrainPanel;


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

            _terrainPanel = new TerrainEditorPanel();
            _terrainPanel.SetCommon(_common);
            _terrainPanel.OnRepaintNeeded += Repaint;
            _terrainPanel.OnEnable();
            _terrainPanel.OnPreviewDirty += () => UpdatePreviewForTab(_activeTab);

            _catalogPanel.OnSelectionChanged += () => UpdatePreviewForTab(_activeTab);
        }

        private void OnDisable()
        {
            _noisePanel.OnDisable();
            _noisePanel.OnRepaintNeeded -= Repaint;

            _catalogPanel.OnDisable();
            _catalogPanel.OnRepaintNeeded -= Repaint;

            _terrainPanel.OnDisable();
            _terrainPanel.OnRepaintNeeded -= Repaint;
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

            // Controls drawn at the top of the preview pane
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
            _previewPanel.Insert(0, previewControls); //<-- 0 Insert before the preview image

            _noisePanel.OnPreviewRebuilt += tex =>
            {
                _lastNoiseTexture = tex;
                UpdatePreviewForTab(_activeTab);
            };
        }

        private void DrawTabs()
        {
            EditorGUI.BeginChangeCheck();
            _common = (LevelGeneratorCommon)EditorGUILayout.ObjectField(
                "Project", _common, typeof(LevelGeneratorCommon), false);
            if (EditorGUI.EndChangeCheck() && _common != null)
            {
                if (_common.NoiseConfig != null) _noisePanel.LoadConfig(_common.NoiseConfig);
                if (_common.TerrainConfig != null) _terrainPanel.LoadConfig(_common.TerrainConfig);
                if (_common.PrefabCatalog != null) _catalogPanel.LoadCatalog(_common.PrefabCatalog);
                _terrainPanel.SetCommon(_common);
            }

            GUILayout.Space(6);
            var newTab = (LevelEditorTabs)GUILayout.Toolbar(
                (int)_activeTab,
                System.Enum.GetNames(typeof(LevelEditorTabs))
            );

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
                    _noisePanel.Draw(position.width, _previewPanel.Resolution, _previewPanel.WorldScale);
                    break;
                case LevelEditorTabs.Terrain:
                  _terrainPanel.Draw(position.width);
                    break;
                case LevelEditorTabs.Prefabs:
                    _catalogPanel.Draw(position.width); 
                    break;
                case LevelEditorTabs.Overlays: 
                   
                    break;
                case LevelEditorTabs.Spawning: 
                  
                    break;
            }
        }

        private void DrawProjectSave()
        {
            if (_common == null) return;

            EditorGUILayout.Space(8);
            Rect r = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(r, new Color(0.35f, 0.35f, 0.35f, 0.6f));
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Project As New"))
            {
                
                if (EditorUtility.DisplayDialog("Save Project",
                    "This will save ALL configs as new assets. Continue?",
                    "Save All", "Cancel"))
                {
               
                    SaveAllConfigs(saveAsNew: true);
                }
            }
            if (GUILayout.Button("Update Project"))
            {
                if (EditorUtility.DisplayDialog("Update Project",
                    "This will OVERWRITE all loaded configs (Noise, Terrain, Prefabs). Continue?",
                    "Overwrite All", "Cancel"))
                {
                    SaveAllConfigs(saveAsNew: false);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void SaveAllConfigs(bool saveAsNew)
        {
            if (saveAsNew)
            {
                var path = EditorUtility.SaveFilePanelInProject(
                    "Save Project", "NewLevelProject", "asset", "Choose location");
                if (string.IsNullOrEmpty(path)) { Debug.Log("Path empty - cancelled"); return; }

                string dir = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
                Debug.Log($"Saving to dir: {dir}");
                Debug.Log($"NoisePanel.RuntimeConfig: {_noisePanel.RuntimeConfig}");
                Debug.Log($"TerrainPanel.RuntimeConfig: {_terrainPanel.RuntimeConfig}");
                Debug.Log($"CatalogPanel.RuntimeCatalog: {_catalogPanel.RuntimeCatalog}");

                SaveConfigAsNew(_noisePanel.RuntimeConfig, dir, "NoiseConfig");
                SaveConfigAsNew(_terrainPanel.RuntimeConfig, dir, "TerrainConfig");
                SaveConfigAsNew(_catalogPanel.RuntimeCatalog, dir, "PrefabCatalog");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                var noiseAsset = AssetDatabase.LoadAssetAtPath<NoiseConfig>($"{dir}/NoiseConfig.asset");
                var terrainAsset = AssetDatabase.LoadAssetAtPath<TerrainConfig>($"{dir}/TerrainConfig.asset");
                var catalogAsset = AssetDatabase.LoadAssetAtPath<PrefabCatalog>($"{dir}/PrefabCatalog.asset");

                Debug.Log($"Loaded back - noise:{noiseAsset} terrain:{terrainAsset} catalog:{catalogAsset}");

                var commonCopy = Object.Instantiate(_common);
                commonCopy.NoiseConfig = noiseAsset;
                commonCopy.TerrainConfig = terrainAsset;
                commonCopy.PrefabCatalog = catalogAsset;

                Debug.Log($"CommonCopy refs - noise:{commonCopy.NoiseConfig} terrain:{commonCopy.TerrainConfig} catalog:{commonCopy.PrefabCatalog}");

                AssetDatabase.CreateAsset(commonCopy, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("Save complete");
            }
            else
            {
                if (_common.NoiseConfig != null)
                {
                    JsonUtility.FromJsonOverwrite(
                        JsonUtility.ToJson(_noisePanel.RuntimeConfig), _common.NoiseConfig);
                    EditorUtility.SetDirty(_common.NoiseConfig);
                }
                if (_common.TerrainConfig != null)
                {
                    JsonUtility.FromJsonOverwrite(
                        JsonUtility.ToJson(_terrainPanel.RuntimeConfig), _common.TerrainConfig);
                    EditorUtility.SetDirty(_common.TerrainConfig);
                }
                if (_common.PrefabCatalog != null)
                {
                    JsonUtility.FromJsonOverwrite(
                        JsonUtility.ToJson(_catalogPanel.RuntimeCatalog), _common.PrefabCatalog);
                    EditorUtility.SetDirty(_common.PrefabCatalog);
                }
                EditorUtility.SetDirty(_common);
                AssetDatabase.SaveAssets();
                Debug.Log("Update complete");
            }
        }

        private Object SaveConfigAsNew(Object config, string dir, string name)
        {
            if (config == null) return null;
            var copy = Object.Instantiate(config);
            var path = $"{dir}/{name}.asset";
            AssetDatabase.CreateAsset(copy, path);
            return copy;
        }


        private void UpdatePreviewForTab(LevelEditorTabs tab)
        {
            if (_lastNoiseTexture == null)
            {
                Debug.Log("UpdatePreviewForTab: _lastNoiseTexture is NULL");
                return;
            }

            switch (tab)
            {
                case LevelEditorTabs.Terrain:
                    _previewPanel.UpdatePreview(_terrainPanel.BuildPreviewTexture(_lastNoiseTexture));
                    break;
                case LevelEditorTabs.Prefabs:
                    var terrainTex = _terrainPanel.BuildPreviewTexture(_lastNoiseTexture);
                    var def = _catalogPanel.SelectedDef;
                    if (def != null)
                        _previewPanel.UpdatePreview(_previewPanel.DrawFootprintCircle(
                            terrainTex,
                            def.Footprint,
                            _common?.ChunkWidth ?? 100f));
                    else
                        _previewPanel.UpdatePreview(terrainTex);
                    break;
                default:
                    _previewPanel.UpdatePreview(_lastNoiseTexture);
                    break;
            }
        }

    }
}
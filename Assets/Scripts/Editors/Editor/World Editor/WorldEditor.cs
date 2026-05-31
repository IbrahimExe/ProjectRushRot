#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Level.Editor
{
    public class WorldEditor : EditorWindow
    {
        // ── State ─────────────────────────────────────────────────────────────
        WorldConfig      _config;
        WorldConfig      _rt;
        SerializedObject _so;

        bool _dirty = false;
        int  _tab   = 0;

        static readonly string[] TAB_LABELS = { "World Noise", "Climate", "Biomes" };

        // ── Panels ────────────────────────────────────────────────────────────
        WorldNoisePanel _noisePanel;
        ClimatePanel    _climatePanel;
        BiomesPanel     _biomesPanel;

        // ── Preview image ─────────────────────────────────────────────────────
        Image _previewImage;

        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("Window/World Editor")]
        public static void Open() => GetWindow<WorldEditor>("World Editor");

        void OnEnable()
        {
            _rt      = ScriptableObject.CreateInstance<WorldConfig>();
            _rt.name = "WorldConfig_RT";
            _so      = new SerializedObject(_rt);

            _noisePanel   = new WorldNoisePanel();
            _climatePanel = new ClimatePanel();
            _biomesPanel  = new BiomesPanel();

            _noisePanel.OnRepaintNeeded   += Repaint;
            _climatePanel.OnRepaintNeeded += Repaint;
            _biomesPanel.OnRepaintNeeded  += Repaint;

            _noisePanel.OnEnable();
            _climatePanel.OnEnable();
            _biomesPanel.OnEnable();
        }

        void OnDisable()
        {
            if (_rt != null) DestroyImmediate(_rt);

            if (_noisePanel   != null) { _noisePanel.OnDisable();   _noisePanel.OnRepaintNeeded   -= Repaint; }
            if (_climatePanel != null) { _climatePanel.OnDisable(); _climatePanel.OnRepaintNeeded -= Repaint; }
            if (_biomesPanel  != null) { _biomesPanel.OnDisable();  _biomesPanel.OnRepaintNeeded  -= Repaint; }
        }

        // ── UIElements shell (same pattern as LevelEditor) ────────────────────

        void CreateGUI()
        {
            rootVisualElement.Clear();

            // Header above the split view
            var header = new IMGUIContainer(DrawHeader);
            header.style.flexShrink = 0;
            rootVisualElement.Add(header);

            var splitView = new TwoPaneSplitView(0, 320, TwoPaneSplitViewOrientation.Horizontal);
            splitView.style.flexGrow = 1;
            rootVisualElement.Add(splitView);

            // Left — scrollable settings
            var leftScroll = new ScrollView(ScrollViewMode.Vertical);
            splitView.Add(leftScroll);

            var leftContent = new IMGUIContainer(DrawLeftPanel);
            leftContent.style.flexGrow = 1;
            leftScroll.Add(leftContent);

            // Right — preview
            var rightPane = new VisualElement();
            rightPane.style.flexGrow = 1;
            rightPane.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
            splitView.Add(rightPane);

            _previewImage = new Image { scaleMode = ScaleMode.ScaleToFit };
            _previewImage.style.flexGrow = 1;
            rightPane.Add(_previewImage);

            // Biomes tab needs to draw nodes on top of the preview texture
            // so we overlay an IMGUI container for the graph interaction
            var graphOverlay = new IMGUIContainer(DrawGraphOverlay);
            graphOverlay.style.position = Position.Absolute;
            graphOverlay.style.left     = graphOverlay.style.top = graphOverlay.style.right = graphOverlay.style.bottom = 0;
            rightPane.Add(graphOverlay);
        }

        // ── Header ────────────────────────────────────────────────────────────

        void DrawHeader()
        {
            GuardRuntime();

            EditorGUILayout.Space(2);

            // Row 1 — WorldConfig + Seed on same line
            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            var newCfg = (WorldConfig)EditorGUILayout.ObjectField(
                "World Config", _config, typeof(WorldConfig), false);
            if (EditorGUI.EndChangeCheck() && newCfg != _config)
            {
                bool ok = !_dirty || EditorUtility.DisplayDialog(
                    "Load", "Discard unsaved changes?", "Discard & Load", "Cancel");
                if (ok) LoadConfig(newCfg);
            }

            // Seed — fixed width so it never overflows
            if (_so != null)
            {
                _so.Update();
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(
                    _so.FindProperty("Seed"),
                    new GUIContent("Seed"),
                    GUILayout.Width(120f));
                if (EditorGUI.EndChangeCheck())
                {
                    _so.ApplyModifiedPropertiesWithoutUndo();
                    MarkAllDirty();
                }
            }

            EditorGUILayout.EndHorizontal();

            // Row 2 — Tab toolbar (fixed button width)
            int prev = _tab;
            _tab = GUILayout.Toolbar(_tab, TAB_LABELS, GUILayout.MaxWidth(400f));
            if (_tab != prev) { GUI.FocusControl(null); UpdatePreview(); }

            // Row 3 — Save bar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField(_dirty ? "● Unsaved changes" : "", EditorStyles.miniLabel, GUILayout.Width(130f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Save As New",    EditorStyles.toolbarButton, GUILayout.Width(100f))) SaveAsNew();
            using (new EditorGUI.DisabledGroupScope(_config == null))
                if (GUILayout.Button("Update Loaded", EditorStyles.toolbarButton, GUILayout.Width(100f))) UpdateLoaded();
            EditorGUILayout.EndHorizontal();
        }

        // ── Left panel ────────────────────────────────────────────────────────

        void DrawLeftPanel()
        {
            GuardRuntime();
            if (_so == null) return;

            _so.Update();
            EditorGUI.BeginChangeCheck();

            switch (_tab)
            {
                case 0: _noisePanel.Draw(_rt,   _so); break;
                case 1: _climatePanel.Draw(_rt, _so); break;
                case 2: _biomesPanel.Draw(_rt,  _so); break;
            }

            if (EditorGUI.EndChangeCheck())
            {
                _so.ApplyModifiedPropertiesWithoutUndo();
                _dirty = true;
                UpdatePreview();
            }
        }

        // ── Graph overlay (Tab 3 only — biome node dragging) ─────────────────

        void DrawGraphOverlay()
        {
            if (_tab != 2 || _rt == null) return;

            float w = _previewImage.resolvedStyle.width;
            float h = _previewImage.resolvedStyle.height;
            if (w <= 0 || h <= 0) return;

            _biomesPanel.DrawGraph(_rt, w, h);

            if (Event.current.type != EventType.Layout)
                UpdatePreview();
        }

        // ── Preview update ────────────────────────────────────────────────────

        void UpdatePreview()
        {
            if (_previewImage == null || _rt == null) return;

            Texture2D tex = null;
            switch (_tab)
            {
                case 0: tex = _noisePanel.BuildPreviewTexture(_rt);   break;
                case 1: tex = _climatePanel.BuildPreviewTexture(_rt); break;
                // Tab 2 draws directly via DrawGraph onto the overlay — no texture needed here
            }

            if (tex != null) _previewImage.image = tex;
        }

        // ── Load / Save ───────────────────────────────────────────────────────

        void LoadConfig(WorldConfig cfg)
        {
            _config = cfg;
            if (cfg != null)
            {
                string n = _rt.name;
                EditorUtility.CopySerializedIfDifferent(cfg, _rt);
                _rt.name = n;
            }
            _dirty = false;
            MarkAllDirty();
            UpdatePreview();
            Repaint();
        }

        void SaveAsNew()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Save World Config", "WorldConfig", "asset", "Choose location");
            if (string.IsNullOrEmpty(path)) return;
            _so.ApplyModifiedProperties();
            var copy = Object.Instantiate(_rt);
            copy.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(copy, path);
            AssetDatabase.SaveAssets();
            LoadConfig(copy);
        }

        void UpdateLoaded()
        {
            if (_config == null) return;
            _so.ApplyModifiedProperties();
            string n = _config.name;
            EditorUtility.CopySerializedIfDifferent(_rt, _config);
            _config.name = n;
            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();
            _dirty = false;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        void MarkAllDirty()
        {
            if (_noisePanel   != null) _noisePanel.PreviewDirty   = true;  // expose setter below
            if (_climatePanel != null) _climatePanel.PreviewDirty = true;
            if (_biomesPanel  != null) _biomesPanel.PreviewDirty  = true;
        }

        void GuardRuntime()
        {
            if (_rt == null)
            {
                _rt      = ScriptableObject.CreateInstance<WorldConfig>();
                _rt.name = "WorldConfig_RT";
                _so      = new SerializedObject(_rt);
                MarkAllDirty();
            }
            else if (_so == null || _so.targetObject == null)
                _so = new SerializedObject(_rt);
        }
    }
}
#endif

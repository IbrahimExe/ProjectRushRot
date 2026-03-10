#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace Level.Editor
{
    // Pure drawing logic for a NoiseConfig — no EditorWindow, no lifecycle.
    // 
    // Usage (from any EditorWindow):
    //   1. Create a NoiseEditorPanel in your window's OnEnable.
    //   2. Call panel.OnEnable() / OnDisable() alongside your window's own lifecycle.
    //   3. Call panel.Draw(windowWidth) inside your OnGUI / tab draw method.
    //   4. Forward Repaint callbacks via panel.OnRepaintNeeded += Repaint.
    public class NoiseEditorPanel
    {
        NoiseConfig _runtimeConfig;
        NoiseConfig _config;
        SerializedObject _so;

        Texture2D _preview;
        int _resolution = 128;
        bool _dirty = true;

        const double k_DebounceSeconds = 0.35;
        double _lastChangeTime = 0;
        bool _rebuildScheduled = false;
        bool _hasUnsavedChanges = false;

        bool _foldWood  = false;
        bool _foldQuant = false;
        bool _foldMar   = false;
        bool _foldTurb  = false;
        bool _foldInvert = false;
        bool _foldWarp  = false;

        Vector2 _scroll;

        public event System.Action OnRepaintNeeded;

        static readonly string[] k_NoiseHints =
        {
            "Default — layered fBm over Value noise. Natural cloud/terrain look.",
            "Value — random scalars blended at lattice corners. Blocky, pillowy appearance.",
            "Perlin — gradient noise. Smooth, organic, directional — no visible grid.",
            "Worley — cellular noise. Crystal / stone / cell membrane pattern.",
            "Ridged — sharp mountain ridges, lightning, cracks, veins. Settings below.",
            "Sparse Dot — rare soft dots, irregularly spaced. Rain, pores, spots. Settings below.",
        };

        public void OnEnable()
        {
            _dirty = true;
            _runtimeConfig = ScriptableObject.CreateInstance<NoiseConfig>();
            _so = new SerializedObject(_runtimeConfig);
            EditorApplication.update += OnEditorUpdate;
        }

        public void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            Object.DestroyImmediate(_runtimeConfig);
            DestroyPreview();
        }

        void OnEditorUpdate()
        {
            if (!_rebuildScheduled) return;
            if (EditorApplication.timeSinceStartup - _lastChangeTime < k_DebounceSeconds) return;

            _rebuildScheduled = false;
            RebuildPreview();
            OnRepaintNeeded?.Invoke();
        }

        // Public entry point 
        public void Draw(float windowWidth)
        {
            if (_so == null || _so.targetObject == null)
                _so = new SerializedObject(_runtimeConfig);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.Space(4);

            EditorGUI.BeginChangeCheck();
            var newCfg = (NoiseConfig)EditorGUILayout.ObjectField(
                "Load from Config", _config, typeof(NoiseConfig), false);
            if (EditorGUI.EndChangeCheck() && newCfg != _config)
            {
                bool proceed = !_hasUnsavedChanges || EditorUtility.DisplayDialog(
                    "Load Config",
                    "You have unsaved changes. Loading a new config will discard them.",
                    "Discard & Load", "Cancel");
                if (proceed) LoadConfig(newCfg);
            }

            _so.Update();
            try { DrawBody(windowWidth); }
            catch (System.Exception e) { Debug.LogException(e); }

            EditorGUILayout.EndScrollView();
        }

        //Lets the hub window push a config directly (e.g. from a shared master config).
        public void LoadConfig(NoiseConfig cfg)
        {
            _config = cfg;
            if (cfg != null)
                EditorUtility.CopySerializedIfDifferent(cfg, _runtimeConfig);
            _hasUnsavedChanges = false;
            SchedulePreviewRebuild();
            OnRepaintNeeded?.Invoke();
        }

        public NoiseConfig Config => _config;
        void DrawBody(float windowWidth)
        {
            EditorGUI.BeginChangeCheck();
            //Noise Type
            Separator("Noise Type");
            var noiseProp = Prop("noiseType");
            Changed(() => EditorGUILayout.PropertyField(noiseProp, GUIContent.none));
            int idx = noiseProp.enumValueIndex;
            if ((uint)idx < (uint)k_NoiseHints.Length)
                EditorGUILayout.HelpBox(k_NoiseHints[idx], MessageType.None);

            if (idx == (int)NoiseType.Ridged)
            {
                EditorGUI.indentLevel++;
                var rp = Prop("ridged");
                if (rp != null)
                {
                    Child(rp, "octaves",        "Octaves");
                    Child(rp, "lacunarity",     "Lacunarity");
                    Child(rp, "gain",           "Gain");
                    Child(rp, "squaredRidges",  "Squared Ridges (sharper peaks)");
                    Child(rp, "ridgeInfluence", "Ridge Influence  (0 = plain, 0.7 = mountains)");
                }
                EditorGUI.indentLevel--;
            }

            if (idx == (int)NoiseType.SparseDot)
            {
                EditorGUI.indentLevel++;
                var sp = Prop("sparseDot");
                if (sp != null)
                {
                    Child(sp, "density",    "Density  (0 = none, 1 = every cell)");
                    Child(sp, "maxDotSize", "Max Dot Size  (fraction of cell)");
                }
                EditorGUI.indentLevel--;
            }

            // Space
            Separator("Space");
            var modeProp = Prop("spaceMode");
            Changed(() => EditorGUILayout.PropertyField(modeProp, new GUIContent("Mode")));

            if (modeProp.enumValueIndex == (int)SpaceMode.Tiling)
            {
                var fp = Prop("tilingFrequency");
                Changed(() => fp.floatValue =
                    EditorGUILayout.Slider("Frequency", fp.floatValue, 0.1f, 20f));
            }
            else
            {
                DrawVector3("position", "Position (frequency)");
                DrawVector3("rotation", "Rotation (offset)");
                DrawVector3("scale",    "Scale");
            }

            //Preview
            Separator("Preview");
            EditorGUI.BeginChangeCheck();
            _resolution = EditorGUILayout.IntSlider("Resolution", _resolution, 32, 512);
            if (EditorGUI.EndChangeCheck()) MarkDirty();

            //Domain Warp
            DrawPattern("Domain Warp",
                ref _foldWarp, "warp", DrawWarpBody,
                "Quilez-style fbm domain warping: displaces the sample point by another fbm " +
                "before evaluating base noise. Level 1 = one warp pass. Level 2 = warp-of-warp.");

            // Custom Patterns
            Separator("Custom Patterns");

            DrawPattern("Wood",
                ref _foldWood, "wood", DrawWoodBody,
                "Concentric ring pattern. Multiply noise then take frac() — like tree rings.");

            DrawPattern("Quantization",
                ref _foldQuant, "quantization", DrawQuantBody,
                "Posterise output into N discrete bands.");

            DrawPattern("Marble",
                ref _foldMar, "marble", DrawMarbleBody,
                "Perturb a sine wave's phase with layered noise — vein/marble stripes.");

            DrawPattern("Turbulence",
                ref _foldTurb, "turbulence", DrawTurbBody,
                "Absolute value of signed layered noise — fire, smoke, cloud texture.");

            DrawPattern("Invert",
                ref _foldInvert, "invert", DrawInvertBody,
                "Flips the output: white becomes black, black becomes white.");

            if (EditorGUI.EndChangeCheck())
                MarkDirty();

            //Save Logic
            if (_hasUnsavedChanges)
                EditorGUILayout.HelpBox("Unsaved changes — use Save As New or Update Loaded.", MessageType.Warning);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save As New"))
            {
                var path = EditorUtility.SaveFilePanelInProject(
                    "Save Noise Config", "NoiseConfig", "asset", "Choose location");
                if (!string.IsNullOrEmpty(path))
                {
                    var copy = Object.Instantiate(_runtimeConfig);
                    AssetDatabase.CreateAsset(copy, path);
                    AssetDatabase.SaveAssets();
                    LoadConfig(copy);
                }
            }
            using (new EditorGUI.DisabledGroupScope(_config == null))
            {
                if (GUILayout.Button("Update Loaded"))
                {
                    EditorUtility.SetDirty(_config);
                    AssetDatabase.SaveAssets();
                    _hasUnsavedChanges = false;
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        // Pattern section

        void DrawPattern(string title, ref bool foldout, string propName,
                         System.Action<SerializedProperty> drawBody, string hint)
        {
            var sp      = _so.FindProperty(propName);
            var enabled = sp?.FindPropertyRelative("enabled");
            if (sp == null || enabled == null) return;

            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            try
            {
                var style = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
                foldout = EditorGUILayout.Foldout(foldout, title, true, style);

                GUILayout.FlexibleSpace();

                EditorGUILayout.LabelField("Enabled", GUILayout.Width(52));
                EditorGUI.BeginChangeCheck();
                enabled.boolValue = EditorGUILayout.Toggle(enabled.boolValue, GUILayout.Width(16));
                if (EditorGUI.EndChangeCheck()) MarkDirty();
            }
            finally { EditorGUILayout.EndHorizontal(); }

            var hintStyle = new GUIStyle(EditorStyles.miniLabel)
            { wordWrap = true, normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } };
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField(hint, hintStyle);
            EditorGUI.indentLevel--;

            if (foldout)
            {
                using (new EditorGUI.DisabledGroupScope(!enabled.boolValue))
                {
                    EditorGUI.indentLevel++;
                    try { drawBody(sp); }
                    catch (System.Exception e) { Debug.LogException(e); }
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.Space(2);
            }
        }

        // Pattern field drawers

        void DrawWarpBody(SerializedProperty p)
        {
            var levelProp = p.FindPropertyRelative("level");
            if (levelProp != null)
            {
                EditorGUI.BeginChangeCheck();
                int newLevel = EditorGUILayout.IntSlider("Level", levelProp.intValue, 0, 2);
                if (EditorGUI.EndChangeCheck()) { levelProp.intValue = newLevel; MarkDirty(); }

                string[] levelHints =
                {
                    "Level 0 — warp disabled (same as unchecking Enabled).",
                    "Level 1 — f(p + strength · fbm(p)). Folded, organic terrain.",
                    "Level 2 — f(p + strength · fbm(fbm(p))). Convoluted, turbulent.",
                };
                var hintStyle = new GUIStyle(EditorStyles.miniLabel)
                { wordWrap = true, normal = { textColor = new Color(0.55f, 0.75f, 0.55f) } };
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField(levelHints[Mathf.Clamp(levelProp.intValue, 0, 2)], hintStyle);
                EditorGUI.indentLevel--;
            }

            Child(p, "strength", "Strength  (Quilez default = 4)");

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Decorrelation Offsets", EditorStyles.boldLabel);

            var offsetStyle = new GUIStyle(EditorStyles.miniLabel)
            { wordWrap = true, normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } };
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField(
                "These seed different regions of the fbm so the X and Y components of each " +
                "displacement field are uncorrelated. Quilez's values (the defaults) work well — " +
                "change them for variety without breaking the algorithm.",
                offsetStyle);
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(2);
            Child(p, "offsetQ0", "Q offset X  (Quilez: 0, 0)");
            Child(p, "offsetQ1", "Q offset Y  (Quilez: 5.2, 1.3)");

            var levelVal = p.FindPropertyRelative("level");
            if (levelVal != null && levelVal.intValue >= 2)
            {
                Child(p, "offsetR0", "R offset X  (Quilez: 1.7, 9.2)");
                Child(p, "offsetR1", "R offset Y  (Quilez: 8.3, 2.8)");
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Reset offsets to Quilez defaults"))
            {
                var so = p.serializedObject;
                so.FindProperty("warp.offsetQ0").vector2Value = new Vector2(0.0f, 0.0f);
                so.FindProperty("warp.offsetQ1").vector2Value = new Vector2(5.2f, 1.3f);
                so.FindProperty("warp.offsetR0").vector2Value = new Vector2(1.7f, 9.2f);
                so.FindProperty("warp.offsetR1").vector2Value = new Vector2(8.3f, 2.8f);
                MarkDirty();
            }
        }

        void DrawWoodBody(SerializedProperty p)
        {
            Child(p, "frequency",   "Frequency");
            Child(p, "multiplier",  "Multiplier (ring density)");
            Child(p, "blendWeight", "Blend Weight");
        }

        void DrawQuantBody(SerializedProperty p)  { Child(p, "totalChannels", "Bands"); }

        void DrawMarbleBody(SerializedProperty p)
        {
            Child(p, "frequency",     "Frequency");
            Child(p, "frequencyMult", "Lacunarity");
            Child(p, "amplitudeMult", "Gain");
            Child(p, "numLayers",     "Layers");
            Child(p, "phase",         "Phase (stripe offset, rad)");
            Child(p, "blendWeight",   "Blend Weight");
        }

        void DrawTurbBody(SerializedProperty p)
        {
            Child(p, "frequency",     "Frequency");
            Child(p, "frequencyMult", "Lacunarity");
            Child(p, "amplitudeMult", "Gain");
            Child(p, "numLayers",     "Layers");
            Child(p, "maxNoiseVal",   "Max Noise Val (0 = auto)");
            Child(p, "blendWeight",   "Blend Weight");
        }

        void DrawInvertBody(SerializedProperty p) { Child(p, "blendWeight", "Blend Weight"); }

        // Helpers
        public bool TryWarnUnsaved()
        {
            if (!_hasUnsavedChanges) return true;
            return EditorUtility.DisplayDialog(
                "Unsaved Noise Changes",
                "You have unsaved changes that will be lost. Discard them?",
                "Discard", "Cancel");
        }
        SerializedProperty Prop(string name) => _so.FindProperty(name);

        void SchedulePreviewRebuild()
        {
            _lastChangeTime = EditorApplication.timeSinceStartup;
            _rebuildScheduled = true;
        }

        void MarkDirty()
        {
            _hasUnsavedChanges = true;
            SchedulePreviewRebuild();
        }

        void Changed(System.Action draw)
        {
            EditorGUI.BeginChangeCheck();
            draw();
            if (EditorGUI.EndChangeCheck()) MarkDirty();
        }

        void DrawVector3(string structProp, string label)
        {
            var p = _so.FindProperty(structProp)?.FindPropertyRelative("value");
            if (p == null) return;
            Changed(() => EditorGUILayout.PropertyField(p, new GUIContent(label), true));
        }

        void Child(SerializedProperty parent, string childName, string label)
        {
            var c = parent?.FindPropertyRelative(childName);
            if (c == null) return;
            Changed(() => EditorGUILayout.PropertyField(c, new GUIContent(label), true));
        }

        static void Separator(string label)
        {
            EditorGUILayout.Space(8);
            Rect r = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(r, new Color(0.35f, 0.35f, 0.35f, 0.6f));
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        }

        // Preview

        void RebuildPreview()
        {
            if (_runtimeConfig == null) return;

            int sz = Mathf.Max(_resolution, 1);
            if (_preview == null || _preview.width != sz)
            {
                DestroyPreview();
                _preview = new Texture2D(sz, sz, TextureFormat.RGB24, false)
                { filterMode = FilterMode.Bilinear };
            }

            var pixels = new Color[sz * sz];
            float inv  = 1f / Mathf.Max(sz - 1, 1);

            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    float v = NoiseSampler.Sample(_runtimeConfig, new Vector2(x * inv, y * inv), sz);
                    pixels[y * sz + x] = new Color(v, v, v);
                }

            _preview.SetPixels(pixels);
            _preview.Apply();
            OnPreviewRebuilt?.Invoke(_preview);
        }

        public event System.Action<Texture2D> OnPreviewRebuilt;
        void DestroyPreview()
        {
            if (_preview != null) { Object.DestroyImmediate(_preview); _preview = null; }
        }
    }
}
#endif

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Open via  Window ▶ Noise Generator  or double-click a NoiseConfig asset.
/// </summary>
public class NoiseEditorWindow : EditorWindow
{
    // ─── State ───────────────────────────────────────────────────────────────

    NoiseConfig _config;
    SerializedObject _so;

    Texture2D _preview;
    int _resolution = 128;   // lower default — user can raise it
    bool _dirty = true;

    // Debounce: only rebuild preview this many seconds after the last change
    const double k_DebounceSeconds = 0.35;
    double _lastChangeTime = 0;
    bool _rebuildScheduled = false;

    // Pattern foldouts — default CLOSED so user sees checkbox clearly first
    bool _foldWood = false;
    bool _foldQuant = false;
    bool _foldMar = false;
    bool _foldTurb = false;
    bool _foldInvert = false;

    Vector2 _scroll;

    static readonly string[] k_NoiseHints =
    {
        "Default — layered fBm over Value noise. Natural cloud/terrain look.",
        "Value — random scalars blended at lattice corners. Blocky, pillowy appearance.",
        "Perlin — gradient noise. Smooth, organic, directional — no visible grid.",
        "Worley — cellular noise. Crystal / stone / cell membrane pattern.",
    };

    // ─── Open ─────────────────────────────────────────────────────────────────

    [MenuItem("Window/Noise Generator")]
    public static void Open() => GetWindow<NoiseEditorWindow>("Noise Generator");

    [UnityEditor.Callbacks.OnOpenAsset]
    static bool OnOpen(int instanceID, int _)
    {
        var cfg = EditorUtility.InstanceIDToObject(instanceID) as NoiseConfig;
        if (cfg == null) return false;
        GetWindow<NoiseEditorWindow>("Noise Generator").LoadConfig(cfg);
        return true;
    }

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    void OnEnable()
    {
        _dirty = true;
        EditorApplication.update += OnEditorUpdate;
    }

    void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        DestroyPreview();
    }

    // Runs every editor tick — fires the rebuild once debounce delay has passed
    void OnEditorUpdate()
    {
        if (!_rebuildScheduled) return;
        if (EditorApplication.timeSinceStartup - _lastChangeTime < k_DebounceSeconds) return;

        _rebuildScheduled = false;
        RebuildPreview();
        Repaint();
    }

    // ─── GUI ──────────────────────────────────────────────────────────────────

    void OnGUI()
    {
        // Rebuild SO if domain reload invalidated it
        if (_config != null && (_so == null || _so.targetObject == null))
            _so = new SerializedObject(_config);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.Space(4);

        // Config field
        EditorGUI.BeginChangeCheck();
        var newCfg = (NoiseConfig)EditorGUILayout.ObjectField(
            "Config Asset", _config, typeof(NoiseConfig), false);
        if (EditorGUI.EndChangeCheck()) LoadConfig(newCfg);

        if (_config == null || _so == null)
        {
            EditorGUILayout.HelpBox(
                "Assign a NoiseConfig above, or create one via\n" +
                "Assets ▶ Create ▶ Noise ▶ Noise Config.",
                MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        _so.Update();

        try { DrawBody(); }
        catch (System.Exception e) { Debug.LogException(e); }

        EditorGUILayout.EndScrollView();
    }

    // ─── Main body ────────────────────────────────────────────────────────────

    void DrawBody()
    {
        // ── Noise Type ────────────────────────────────────────────────────────
        Separator("Noise Type");
        var noiseProp = Prop("noiseType");
        Changed(() => EditorGUILayout.PropertyField(noiseProp, GUIContent.none));
        int idx = noiseProp.enumValueIndex;
        if ((uint)idx < (uint)k_NoiseHints.Length)
            EditorGUILayout.HelpBox(k_NoiseHints[idx], MessageType.None);

        // ── Space ─────────────────────────────────────────────────────────────
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
            DrawVector3("scale", "Scale");
        }

        // ── Preview ───────────────────────────────────────────────────────────
        Separator("Preview");
        EditorGUI.BeginChangeCheck();
        _resolution = EditorGUILayout.IntSlider("Resolution", _resolution, 32, 512);
        if (EditorGUI.EndChangeCheck()) MarkDirty();

        // ── Custom Patterns ───────────────────────────────────────────────────
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

        if (_so.ApplyModifiedProperties())
        {
            MarkDirty();
            EditorUtility.SetDirty(_config);
        }

        // ── Preview image ─────────────────────────────────────────────────────
        EditorGUILayout.Space(8);

        // Show a subtle "pending" tint while waiting for debounce
        if (_rebuildScheduled)
        {
            var tintStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField("⏳ Preview updating…", tintStyle);
        }

        // was looking for "sameline" or "inline" to align picture with the top
        // other option is a new "Begin"/"end" for imgui where you add this section to and it will draw in a separate window
        if (_preview != null)
        {
            float sz = Mathf.Min(position.width - 24f, _preview.width);
            Rect rect = GUILayoutUtility.GetRect(sz, sz, GUILayout.ExpandWidth(false));
            EditorGUI.DrawPreviewTexture(rect, _preview, null, ScaleMode.ScaleToFit);
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh")) { RebuildPreview(); Repaint(); }
        if (GUILayout.Button("Save Asset")) AssetDatabase.SaveAssets();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(8);
    }

    // ─── Pattern section ──────────────────────────────────────────────────────
    //
    // Layout:
    //   [▶ Title]           [□ enabled checkbox]
    //   — hint line (always visible) —
    //   (body fields, shown only when foldout open AND enabled)
    //
    // The checkbox is ALWAYS visible in the header row — collapsing the foldout
    // does NOT hide whether the pattern is on or off.

    void DrawPattern(string title, ref bool foldout, string propName,
                     System.Action<SerializedProperty> drawBody, string hint)
    {
        var sp = _so.FindProperty(propName);
        var enabled = sp?.FindPropertyRelative("enabled");
        if (sp == null || enabled == null) return;

        EditorGUILayout.Space(2);

        // ── Header row ────────────────────────────────────────────────────────
        EditorGUILayout.BeginHorizontal();
        try
        {
            // Foldout label
            var style = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
            foldout = EditorGUILayout.Foldout(foldout, title, true, style);

            GUILayout.FlexibleSpace();

            // "Enabled" label + checkbox — always visible regardless of foldout state
            EditorGUILayout.LabelField("Enabled", GUILayout.Width(52));
            EditorGUI.BeginChangeCheck();
            enabled.boolValue = EditorGUILayout.Toggle(enabled.boolValue, GUILayout.Width(16));
            if (EditorGUI.EndChangeCheck()) MarkDirty();
        }
        finally { EditorGUILayout.EndHorizontal(); }

        // ── Hint line (always shown) ──────────────────────────────────────────
        var hintStyle = new GUIStyle(EditorStyles.miniLabel)
        { wordWrap = true, normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } };
        EditorGUI.indentLevel++;
        EditorGUILayout.LabelField(hint, hintStyle);
        EditorGUI.indentLevel--;

        // ── Body fields (foldout open only) ───────────────────────────────────
        if (foldout)
        {
            // If disabled, grey everything out but still show the fields
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

    // ─── Pattern field drawers ────────────────────────────────────────────────

    void DrawWoodBody(SerializedProperty p)
    {
        Child(p, "frequency", "Frequency");        
        Child(p, "multiplier", "Multiplier (ring density)");
        Child(p, "blendWeight", "Blend Weight");     
    }

    void DrawQuantBody(SerializedProperty p)
    {
        Child(p, "totalChannels", "Bands");
    }

    void DrawMarbleBody(SerializedProperty p)
    {
        Child(p, "frequency", "Frequency");
        Child(p, "frequencyMult", "Lacunarity");
        Child(p, "amplitudeMult", "Gain");
        Child(p, "numLayers", "Layers");
        Child(p, "phase", "Phase (stripe offset, rad)");
        Child(p, "blendWeight", "Blend Weight");    
    }

    void DrawTurbBody(SerializedProperty p)
    {
        Child(p, "frequency", "Frequency");
        Child(p, "frequencyMult", "Lacunarity");
        Child(p, "amplitudeMult", "Gain");
        Child(p, "numLayers", "Layers");
        Child(p, "maxNoiseVal", "Max Noise Val (0 = auto)");
        Child(p, "blendWeight", "Blend Weight");    
    }

    void DrawInvertBody(SerializedProperty p)
    {
        Child(p, "blendWeight", "Blend Weight");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    SerializedProperty Prop(string name) => _so.FindProperty(name);

    // Call when any value changes — schedules a debounced preview rebuild
    void MarkDirty()
    {
        _lastChangeTime = EditorApplication.timeSinceStartup;
        _rebuildScheduled = true;
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

    void LoadConfig(NoiseConfig cfg)
    {
        _config = cfg;
        _so = cfg != null ? new SerializedObject(cfg) : null;
        MarkDirty();
        Repaint();
    }

    // ─── Preview ──────────────────────────────────────────────────────────────

    void RebuildPreview()
    {
        if (_config == null) return;

        int sz = Mathf.Max(_resolution, 1);
        if (_preview == null || _preview.width != sz)
        {
            DestroyPreview();
            _preview = new Texture2D(sz, sz, TextureFormat.RGB24, false)
            { filterMode = FilterMode.Bilinear };
        }

        var pixels = new Color[sz * sz];
        float inv = 1f / Mathf.Max(sz - 1, 1);

        for (int y = 0; y < sz; y++)
            for (int x = 0; x < sz; x++)
            {
                float v = NoiseSampler.Sample(_config, new Vector2(x * inv, y * inv), sz);
                pixels[y * sz + x] = new Color(v, v, v);
            }

        _preview.SetPixels(pixels);
        _preview.Apply();
    }

    void DestroyPreview()
    {
        if (_preview != null) { DestroyImmediate(_preview); _preview = null; }
    }
}
#endif
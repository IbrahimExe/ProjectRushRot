#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Level.Editor
{
    public class SpawningPanel
    {
        SpawnConfig      _config;
        SerializedObject _so;
        SpawnConfig      _runtimeConfig;
        PrefabCatalog    _catalog;

        bool       _hasUnsavedChanges = false;
        List<bool> _foldouts          = new List<bool>();

        public event System.Action OnRepaintNeeded;
        public SpawnConfig Config        => _config;
        public SpawnConfig RuntimeConfig => _runtimeConfig;

        public void OnEnable()
        {
            _runtimeConfig      = ScriptableObject.CreateInstance<SpawnConfig>();
            _runtimeConfig.name = "SpawnConfig_Runtime";
            _so                 = new SerializedObject(_runtimeConfig);
        }

        public void OnDisable()
        {
            if (_runtimeConfig != null) Object.DestroyImmediate(_runtimeConfig);
        }

        // Called by LevelEditor when catalog changes
        public void SetCatalog(PrefabCatalog catalog) => _catalog = catalog;

        public void Draw(float windowWidth)
        {
            if (_runtimeConfig == null)
            {
                _runtimeConfig      = ScriptableObject.CreateInstance<SpawnConfig>();
                _runtimeConfig.name = "SpawnConfig_Runtime";
                _so                 = new SerializedObject(_runtimeConfig);
                if (_config != null)
                    EditorUtility.CopySerializedIfDifferent(_config, _runtimeConfig);
            }
            else if (_so == null || _so.targetObject == null)
                _so = new SerializedObject(_runtimeConfig);

            EditorGUILayout.Space(4);

            EditorGUI.BeginChangeCheck();
            var newConfig = (SpawnConfig)EditorGUILayout.ObjectField(
                "Load from Config", _config, typeof(SpawnConfig), false);
            if (EditorGUI.EndChangeCheck() && newConfig != _config)
            {
                bool proceed = !_hasUnsavedChanges || EditorUtility.DisplayDialog(
                    "Load Spawn Config",
                    "You have unsaved changes. Loading will discard them.",
                    "Discard & Load", "Cancel");
                if (proceed) LoadConfig(newConfig);
            }

            EditorGUILayout.Space(6);
            _so.Update();

            EditorGUI.BeginChangeCheck();
            DrawGlobalSettings();
            DrawRulesList();
            if (EditorGUI.EndChangeCheck())
            {
                _hasUnsavedChanges = true;
                _so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (_hasUnsavedChanges)
                EditorGUILayout.HelpBox("Unsaved changes — use Save As New or Update Loaded.", MessageType.Warning);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save As New"))
            {
                var path = EditorUtility.SaveFilePanelInProject(
                    "Save Spawn Config", "SpawnConfig", "asset", "Choose location");
                if (!string.IsNullOrEmpty(path))
                {
                    var copy  = Object.Instantiate(_runtimeConfig);
                    copy.name = System.IO.Path.GetFileNameWithoutExtension(path);
                    AssetDatabase.CreateAsset(copy, path);
                    AssetDatabase.SaveAssets();
                    LoadConfig(copy);
                }
            }
            using (new EditorGUI.DisabledGroupScope(_config == null))
            {
                if (GUILayout.Button("Update Loaded"))
                {
                    _so.ApplyModifiedProperties();
                    string originalName = _config.name;
                    EditorUtility.CopySerializedIfDifferent(_runtimeConfig, _config);
                    _config.name = originalName;
                    EditorUtility.SetDirty(_config);
                    AssetDatabase.SaveAssets();
                    _hasUnsavedChanges = false;
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        void DrawGlobalSettings()
        {
            Separator("Global Settings");
            _so.Update();

            EditorGUILayout.PropertyField(_so.FindProperty("Density"),          new GUIContent("Base Density"));
            EditorGUILayout.PropertyField(_so.FindProperty("FullDistance"),      new GUIContent("Full Distance"));
            EditorGUILayout.PropertyField(_so.FindProperty("SimDistance"),       new GUIContent("Sim Distance"));
            EditorGUILayout.PropertyField(_so.FindProperty("StandInDistance"),   new GUIContent("Stand-In Distance"));
        }

        void DrawRulesList()
        {
            var rulesProp = _so.FindProperty("Rules");
            if (rulesProp == null) return;

            Separator("Spawn Rules");

            // Build ID list from catalog for dropdown
            string[] ids      = BuildIDList();
            string[] idLabels = ids.Length > 0 ? ids : new[] { "— No catalog loaded —" };

            while (_foldouts.Count < rulesProp.arraySize) _foldouts.Add(false);
            while (_foldouts.Count > rulesProp.arraySize) _foldouts.RemoveAt(_foldouts.Count - 1);

            for (int i = 0; i < rulesProp.arraySize; i++)
            {
                var rule             = rulesProp.GetArrayElementAtIndex(i);
                var prefabDefIDProp  = rule.FindPropertyRelative("PrefabDefID");
                var placementProp    = rule.FindPropertyRelative("PlacementMode");
                var densityMultProp  = rule.FindPropertyRelative("DensityMultiplier");
                var minSpacingProp   = rule.FindPropertyRelative("MinSpacing");
                var heightMinProp    = rule.FindPropertyRelative("HeightMin");
                var heightMaxProp    = rule.FindPropertyRelative("HeightMax");
                var maxSlopeProp     = rule.FindPropertyRelative("MaxSlope");
                var blockedByProp    = rule.FindPropertyRelative("BlockedBy");

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                // Color dot by placement mode
                var iconRect = GUILayoutUtility.GetRect(20, 20, GUILayout.Width(20), GUILayout.Height(20));
                EditorGUI.DrawRect(iconRect, PlacementColor((PlacementMode)placementProp.enumValueIndex));

                string label = string.IsNullOrEmpty(prefabDefIDProp.stringValue)
                    ? $"Rule {i}" : prefabDefIDProp.stringValue;
                _foldouts[i] = EditorGUILayout.Foldout(_foldouts[i], label, true,
                    new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold });

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    rulesProp.DeleteArrayElementAtIndex(i);
                    _foldouts.RemoveAt(i);
                    _so.ApplyModifiedPropertiesWithoutUndo();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                if (_foldouts[i])
                {
                    EditorGUI.indentLevel++;

                    // Dropdown for PrefabDefID
                    if (ids.Length > 0)
                    {
                        int currentIndex = System.Array.IndexOf(ids, prefabDefIDProp.stringValue);
                        if (currentIndex < 0) currentIndex = 0;
                        EditorGUI.BeginChangeCheck();
                        int newIndex = EditorGUILayout.Popup("Prefab Def", currentIndex, idLabels);
                        if (EditorGUI.EndChangeCheck())
                            prefabDefIDProp.stringValue = ids[newIndex];
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Load a PrefabCatalog in the Prefabs tab first.", MessageType.Warning);
                    }

                    EditorGUILayout.PropertyField(placementProp,   new GUIContent("Placement Mode"));
                    EditorGUILayout.PropertyField(densityMultProp, new GUIContent("Density Multiplier"));
                    EditorGUILayout.PropertyField(minSpacingProp,  new GUIContent("Min Spacing"));
                    EditorGUILayout.PropertyField(heightMinProp,   new GUIContent("Height Min"));
                    EditorGUILayout.PropertyField(heightMaxProp,   new GUIContent("Height Max"));
                    EditorGUILayout.PropertyField(maxSlopeProp,    new GUIContent("Max Slope"));
                    EditorGUILayout.PropertyField(blockedByProp,   new GUIContent("Blocked By"), true);

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            if (GUILayout.Button("+ Add Rule"))
            {
                rulesProp.InsertArrayElementAtIndex(rulesProp.arraySize);
                _foldouts.Add(true);
                _so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        string[] BuildIDList()
        {
            if (_catalog == null || _catalog.Definitions == null) return new string[0];
            var ids = new List<string>();
            foreach (var def in _catalog.Definitions)
                if (!string.IsNullOrEmpty(def.ID)) ids.Add(def.ID);
            return ids.ToArray();
        }

        public void LoadConfig(SpawnConfig config)
        {
            _config = config;
            if (config != null)
            {
                string originalName = _runtimeConfig.name;
                EditorUtility.CopySerializedIfDifferent(config, _runtimeConfig);
                _runtimeConfig.name = originalName;
            }
            _hasUnsavedChanges = false;
            OnRepaintNeeded?.Invoke();
        }

        static void Separator(string label)
        {
            EditorGUILayout.Space(8);
            Rect r = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(r, new Color(0.35f, 0.35f, 0.35f, 0.6f));
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        }

        static Color PlacementColor(PlacementMode mode)
        {
            switch (mode)
            {
                case PlacementMode.PoissonDisk: return new Color(0.2f, 0.7f, 0.4f);
                case PlacementMode.GridJitter:  return new Color(0.7f, 0.5f, 0.2f);
                default:                        return Color.gray;
            }
        }
    }
}
#endif

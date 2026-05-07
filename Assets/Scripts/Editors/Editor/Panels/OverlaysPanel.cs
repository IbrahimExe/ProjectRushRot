#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Level.Editor
{
    public class OverlaysPanel
    {
        OverlayConfig _config;
        SerializedObject _so;
        OverlayConfig _runtimeConfig;

        bool _hasUnsavedChanges = false;
        List<bool> _foldouts = new List<bool>();

        public event System.Action OnRepaintNeeded;
        public OverlayConfig Config => _config;
        public OverlayConfig RuntimeConfig => _runtimeConfig;

        public void OnEnable()
        {
            _runtimeConfig = ScriptableObject.CreateInstance<OverlayConfig>();
            _runtimeConfig.name = "OverlayConfig_Runtime";
            _so = new SerializedObject(_runtimeConfig);
        }

        public void OnDisable()
        {
            if (_runtimeConfig != null) Object.DestroyImmediate(_runtimeConfig);
        }

        public void Draw(float windowWidth)
        {
            if (_runtimeConfig == null)
            {
                _runtimeConfig = ScriptableObject.CreateInstance<OverlayConfig>();
                _runtimeConfig.name = "OverlayConfig_Runtime";
                _so = new SerializedObject(_runtimeConfig);
                if (_config != null)
                    EditorUtility.CopySerializedIfDifferent(_config, _runtimeConfig);
            }
            else if (_so == null || _so.targetObject == null)
            {
                _so = new SerializedObject(_runtimeConfig);
            }

            EditorGUILayout.Space(4);

            EditorGUI.BeginChangeCheck();
            var newConfig = (OverlayConfig)EditorGUILayout.ObjectField(
                "Load from Config", _config, typeof(OverlayConfig), false);
            if (EditorGUI.EndChangeCheck() && newConfig != _config)
            {
                bool proceed = !_hasUnsavedChanges || EditorUtility.DisplayDialog(
                    "Load Overlay Config",
                    "You have unsaved changes. Loading will discard them.",
                    "Discard & Load", "Cancel");
                if (proceed) LoadConfig(newConfig);
            }

            EditorGUILayout.Space(6);

            _so.Update();

            EditorGUI.BeginChangeCheck();
            DrawOverlayList();
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
                    "Save Overlay Config", "OverlayConfig", "asset", "Choose location");
                if (!string.IsNullOrEmpty(path))
                {
                    var copy = Object.Instantiate(_runtimeConfig);
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

        void DrawOverlayList()
        {
            var overlaysProp = _so.FindProperty("Overlays");
            if (overlaysProp == null) return;

            Separator("Overlays");

            while (_foldouts.Count < overlaysProp.arraySize) _foldouts.Add(false);
            while (_foldouts.Count > overlaysProp.arraySize) _foldouts.RemoveAt(_foldouts.Count - 1);

            for (int i = 0; i < overlaysProp.arraySize; i++)
            {
                var entry = overlaysProp.GetArrayElementAtIndex(i);
                var nameProp = entry.FindPropertyRelative("Name");
                var enabledProp = entry.FindPropertyRelative("Enabled");
                var typeProp = entry.FindPropertyRelative("Type");
                var strengthProp = entry.FindPropertyRelative("Strength");
                var curveProp = entry.FindPropertyRelative("FalloffCurve");

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Header row
                EditorGUILayout.BeginHorizontal();

                // Type color indicator
                var iconRect = GUILayoutUtility.GetRect(20, 20, GUILayout.Width(20), GUILayout.Height(20));
                EditorGUI.DrawRect(iconRect, TypeColor((OverlayType)typeProp.enumValueIndex));

                _foldouts[i] = EditorGUILayout.Foldout(_foldouts[i],
                    string.IsNullOrEmpty(nameProp.stringValue) ? $"Overlay {i}" : nameProp.stringValue,
                    true, new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold });


                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(
                    ((OverlayType)typeProp.enumValueIndex).ToString(),
                    EditorStyles.miniLabel, GUILayout.Width(60));
                EditorGUILayout.LabelField("On", GUILayout.Width(20));
                enabledProp.boolValue = EditorGUILayout.Toggle(enabledProp.boolValue, GUILayout.Width(16));

                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    overlaysProp.DeleteArrayElementAtIndex(i);
                    _foldouts.RemoveAt(i);
                    _so.ApplyModifiedPropertiesWithoutUndo();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                if (_foldouts[i])
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(nameProp, new GUIContent("Name"));
                    EditorGUILayout.PropertyField(typeProp, new GUIContent("Type"));
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("GenInvert"), new GUIContent("Gen Invert"));
                    EditorGUILayout.PropertyField(curveProp, new GUIContent("Falloff Curve"));

                    var type = (OverlayType)typeProp.enumValueIndex;
                    if (type == OverlayType.Island)
                    {
                        EditorGUILayout.PropertyField(entry.FindPropertyRelative("CentreX"), new GUIContent("Centre X"));
                        EditorGUILayout.PropertyField(entry.FindPropertyRelative("CentreZ"), new GUIContent("Centre Z"));
                        EditorGUILayout.PropertyField(entry.FindPropertyRelative("Scale"), new GUIContent("Radius"));
                        EditorGUILayout.PropertyField(entry.FindPropertyRelative("FloorValue"), new GUIContent("Floor value"));
                    }
                    else if (type == OverlayType.Equator || type == OverlayType.Meridian)
                    {
                        EditorGUILayout.PropertyField(entry.FindPropertyRelative("WorldOffset"), new GUIContent("World Offset"));
                        EditorGUILayout.PropertyField(strengthProp, new GUIContent("Strength"));
                        EditorGUILayout.PropertyField(entry.FindPropertyRelative("Scale"), new GUIContent("Scale"));
                        EditorGUILayout.PropertyField(entry.FindPropertyRelative("FloorValue"), new GUIContent("Floor Value"));
                    }
                    EditorGUI.indentLevel--;


                }


                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);

            }

                if (GUILayout.Button("+ Add Overlay"))
                {
                    overlaysProp.InsertArrayElementAtIndex(overlaysProp.arraySize);
                    _foldouts.Add(true);
                    _so.ApplyModifiedPropertiesWithoutUndo();
                }
            
        }

        public void LoadConfig(OverlayConfig config)
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

        static Color TypeColor(OverlayType type)
        {
            switch (type)
            {
                case OverlayType.Island: return new Color(0.2f, 0.6f, 0.9f);
                case OverlayType.Equator: return new Color(0.9f, 0.7f, 0.1f);
                case OverlayType.Meridian: return new Color(0.8f, 0.3f, 0.6f);
                default: return Color.gray;
            }
        }
    }
}
#endif
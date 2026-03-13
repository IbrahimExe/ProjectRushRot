#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using LevelGenerator.Data;

namespace Level.Editor
{
    public class PrefabCatalogPanel
    {
        NewPrefabCatalog _catalog;
        SerializedObject _so;
        NewPrefabCatalog _runtimeCatalog;

        bool _hasUnsavedChanges = false;
        Vector2 _scroll;
        List<bool> _foldouts = new List<bool>();

        public event System.Action OnRepaintNeeded;

        public void OnEnable()
        {
            _runtimeCatalog = ScriptableObject.CreateInstance<NewPrefabCatalog>();
            _so = new SerializedObject(_runtimeCatalog);
        }

        public void OnDisable()
        {
            if (_runtimeCatalog != null) Object.DestroyImmediate(_runtimeCatalog);
        }

        public void Draw(float windowWidth)
        {
            if (_so == null || _so.targetObject == null)
                _so = new SerializedObject(_runtimeCatalog);

            // No BeginScrollView here — LevelEditor's left pane handles scrolling

            EditorGUILayout.Space(4);

            EditorGUI.BeginChangeCheck();
            var newCatalog = (NewPrefabCatalog)EditorGUILayout.ObjectField(
                "Load from Catalog", _catalog, typeof(NewPrefabCatalog), false);
            if (EditorGUI.EndChangeCheck() && newCatalog != _catalog)
            {
                bool proceed = !_hasUnsavedChanges || EditorUtility.DisplayDialog(
                    "Load Catalog",
                    "You have unsaved changes. Loading will discard them.",
                    "Discard & Load", "Cancel");
                if (proceed) LoadCatalog(newCatalog);
            }

            EditorGUILayout.Space(6);

            _so.Update();

            EditorGUI.BeginChangeCheck();
            DrawDefinitionsList();
            bool changed = EditorGUI.EndChangeCheck();
            _so.ApplyModifiedPropertiesWithoutUndo();
            if (changed) _hasUnsavedChanges = true;

            if (_hasUnsavedChanges)
                EditorGUILayout.HelpBox("Unsaved changes — use Save As New or Update Loaded.", MessageType.Warning);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save As New"))
            {
                var path = EditorUtility.SaveFilePanelInProject(
                    "Save Prefab Catalog", "NewPrefabCatalog", "asset", "Choose location");
                if (!string.IsNullOrEmpty(path))
                {
                    var copy = Object.Instantiate(_runtimeCatalog);
                    AssetDatabase.CreateAsset(copy, path);
                    AssetDatabase.SaveAssets();
                    LoadCatalog(copy);
                }
            }
            using (new EditorGUI.DisabledGroupScope(_catalog == null))
            {
                if (GUILayout.Button("Update Loaded"))
                {
                    EditorUtility.CopySerializedIfDifferent(_runtimeCatalog, _catalog);
                    EditorUtility.SetDirty(_catalog);
                    AssetDatabase.SaveAssets();
                    _hasUnsavedChanges = false;
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        void DrawDefinitionsList()
        {
            var defsProp = _so.FindProperty("Definitions");
            if (defsProp == null) return;

            Separator("Prefab Definitions");

            while (_foldouts.Count < defsProp.arraySize) _foldouts.Add(false);
            while (_foldouts.Count > defsProp.arraySize) _foldouts.RemoveAt(_foldouts.Count - 1);

            for (int i = 0; i < defsProp.arraySize; i++)
            {
                var entry = defsProp.GetArrayElementAtIndex(i);
                var nameProp = entry.FindPropertyRelative("Name");
                var idProp = entry.FindPropertyRelative("ID");
                var categoryProp = entry.FindPropertyRelative("Category");
                var variantsProp = entry.FindPropertyRelative("Variants");
                var footprintProp = entry.FindPropertyRelative("Footprint");

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Header — foldout + category tag + remove button
                EditorGUILayout.BeginHorizontal();
                _foldouts[i] = EditorGUILayout.Foldout(_foldouts[i],
                    string.IsNullOrEmpty(nameProp.stringValue) ? $"Entry {i}" : nameProp.stringValue,
                    true, new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold });
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(
                    ((PrefabCategory)categoryProp.enumValueIndex).ToString(),
                    EditorStyles.miniLabel, GUILayout.Width(70));
                if (GUILayout.Button("✕", GUILayout.Width(20)))
                {
                    defsProp.DeleteArrayElementAtIndex(i);
                    _foldouts.RemoveAt(i);
                    _so.ApplyModifiedPropertiesWithoutUndo();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                if (_foldouts[i])
                {
                    EditorGUI.indentLevel++;

                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(nameProp, new GUIContent("Name"));
                    if (EditorGUI.EndChangeCheck() && !string.IsNullOrEmpty(nameProp.stringValue))
                        idProp.stringValue = nameProp.stringValue.ToUpperInvariant().Replace(" ", "_");

                    using (new EditorGUI.DisabledGroupScope(true))
                        EditorGUILayout.PropertyField(idProp, new GUIContent("ID"));

                    EditorGUILayout.PropertyField(categoryProp, new GUIContent("Category"));
                    EditorGUILayout.PropertyField(footprintProp, new GUIContent("Footprint"));
                    EditorGUILayout.PropertyField(variantsProp, new GUIContent("Variants"), true);

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            if (GUILayout.Button("+ Add Entry"))
            {
                defsProp.InsertArrayElementAtIndex(defsProp.arraySize);
                _foldouts.Add(true);
                _so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        public void LoadCatalog(NewPrefabCatalog catalog)
        {
            _catalog = catalog;
            if (catalog != null)
                EditorUtility.CopySerializedIfDifferent(catalog, _runtimeCatalog);
            _hasUnsavedChanges = false;
            OnRepaintNeeded?.Invoke();
        }

        public bool TryWarnUnsaved()
        {
            if (!_hasUnsavedChanges) return true;
            _hasUnsavedChanges = false; // avoid multiple prompts if they click through
            return EditorUtility.DisplayDialog(
                "Unsaved Catalog Changes",
                "You have unsaved changes that will be lost. Discard them?",
                "Discard", "Cancel");
        }

        static void Separator(string label)
        {
            EditorGUILayout.Space(8);
            Rect r = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(r, new Color(0.35f, 0.35f, 0.35f, 0.6f));
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        }

        public NewPrefabCatalog RuntimeCatalog => _runtimeCatalog;
    }
}
#endif
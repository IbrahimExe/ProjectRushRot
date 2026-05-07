#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;


namespace Level.Editor
{
    public class PrefabCatalogPanel
    {
        PrefabCatalog _catalog;
        SerializedObject _so;
        PrefabCatalog _runtimeCatalog;

        bool _hasUnsavedChanges = false;
        List<bool> _foldouts = new List<bool>();

        int _selectedIndex = -1;
        public event System.Action OnSelectionChanged;

        public void ApplyChanges() => _so.ApplyModifiedProperties();
        public PrefabCatalog LoadedCatalog => _catalog as PrefabCatalog;
        public PrefabDef SelectedDef
        {
            get
            {
                if (_selectedIndex < 0 || _so == null) return null;
                var defsProp = _so.FindProperty("Definitions");
                if (defsProp == null || _selectedIndex >= defsProp.arraySize) return null;
                var entry = defsProp.GetArrayElementAtIndex(_selectedIndex);
                var footprintProp = entry.FindPropertyRelative("Footprint");
                var nameProp = entry.FindPropertyRelative("Name");
                return new PrefabDef
                {
                    Name = nameProp.stringValue,
                    Footprint = footprintProp.floatValue
                };
            }
        }

        public event System.Action OnRepaintNeeded;
        public int SelectedIndex => _selectedIndex;

        public void OnEnable()
        {
            _runtimeCatalog = ScriptableObject.CreateInstance<PrefabCatalog>();
            _so = new SerializedObject(_runtimeCatalog);
        }

        public void OnDisable()
        {
            if (_runtimeCatalog != null) Object.DestroyImmediate(_runtimeCatalog);
        }

        public void Draw(float windowWidth)
        {
            if (_runtimeCatalog == null)
            {
                _runtimeCatalog = ScriptableObject.CreateInstance<PrefabCatalog>();
                _runtimeCatalog.name = "PrefabCatalog_Runtime";
                _so = new SerializedObject(_runtimeCatalog);
                if (_catalog != null)
                    EditorUtility.CopySerializedIfDifferent(_catalog, _runtimeCatalog);
            }
            else if (_so == null || _so.targetObject == null)
            {
                _so = new SerializedObject(_runtimeCatalog);
            }

            // No BeginScrollView here — LevelEditor's left pane handles scrolling

            EditorGUILayout.Space(4);

            EditorGUI.BeginChangeCheck();
            var newCatalog = (PrefabCatalog)EditorGUILayout.ObjectField(
                "Load from Catalog", _catalog, typeof(PrefabCatalog), false);
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
            if (changed)
            {
                _hasUnsavedChanges = true;
                if (_selectedIndex >= 0)
                    OnSelectionChanged?.Invoke();
            }
            if (_hasUnsavedChanges)
                EditorGUILayout.HelpBox("Unsaved changes — use Save As New or Update Loaded.", MessageType.Warning);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save As New"))
            {
                var path = EditorUtility.SaveFilePanelInProject(
                    "Save Prefab Catalog", "PrefabCatalog", "asset", "Choose location");
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

                // Thumbnail icon — asset preview or category color fallback
                var variantsPropForThumb = entry.FindPropertyRelative("Variants");
                Texture2D thumb = null;
                if (variantsPropForThumb.arraySize > 0)
                {
                    var firstVariant = variantsPropForThumb.GetArrayElementAtIndex(0);
                    var go = firstVariant.objectReferenceValue as GameObject;
                    if (go != null) thumb = AssetPreview.GetAssetPreview(go);
                }

                var iconRect = GUILayoutUtility.GetRect(20, 20, GUILayout.Width(20), GUILayout.Height(20));
                if (thumb != null)
                    GUI.DrawTexture(iconRect, thumb, ScaleMode.ScaleToFit);
                else
                    EditorGUI.DrawRect(iconRect, CategoryColor((PrefabCategory)categoryProp.enumValueIndex));

                bool wasOpen = _foldouts[i];
                _foldouts[i] = EditorGUILayout.Foldout(_foldouts[i],
                    string.IsNullOrEmpty(nameProp.stringValue) ? $"Entry {i}" : nameProp.stringValue,
                    true, new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold });

                // selecting an entry updates the preview
                if (_foldouts[i])
                {
                    if (_foldouts[i] != wasOpen || _selectedIndex != i)
                    {
                        _selectedIndex = i;
                        OnSelectionChanged?.Invoke();
                    }
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(
                    ((PrefabCategory)categoryProp.enumValueIndex).ToString(),
                    EditorStyles.miniLabel, GUILayout.Width(70));
                if (GUILayout.Button("✕", GUILayout.Width(20)))
                {
                    defsProp.DeleteArrayElementAtIndex(i);
                    _foldouts.RemoveAt(i);
                    if (_selectedIndex == i) { _selectedIndex = -1; OnSelectionChanged?.Invoke(); }
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
            if (AssetPreview.IsLoadingAssetPreviews())
                OnRepaintNeeded?.Invoke();
        }

        public void LoadCatalog(PrefabCatalog catalog)
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

        public PrefabCatalog RuntimeCatalog => _runtimeCatalog;

    static Texture2D GetEntryThumbnail(PrefabDef def)
        {
            if (def?.Variants == null || def.Variants.Count == 0 || def.Variants[0] == null)
                return null;
            return AssetPreview.GetAssetPreview(def.Variants[0]);
        }

        static Color CategoryColor(PrefabCategory cat)
        {
            switch (cat)
            {
                case PrefabCategory.Obstacle: return new Color(0.8f, 0.2f, 0.2f);
                case PrefabCategory.Enemy: return new Color(0.9f, 0.4f, 0.1f);
                case PrefabCategory.Collectable: return new Color(0.2f, 0.8f, 0.2f);
                case PrefabCategory.Decoration: return new Color(0.3f, 0.5f, 0.9f);
                case PrefabCategory.Wall: return new Color(0.5f, 0.5f, 0.5f);
                default: return Color.gray;
            }
        }
    }
}
#endif
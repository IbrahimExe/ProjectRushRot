using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using LevelGenerator.Data;

[CustomEditor(typeof(NeighborRulesConfig))]
public class NeighborRulesConfigEditor : Editor
{
    private NeighborRulesConfig _target;

    // Surface dropdown data
    private string[] _surfaceDisplayNames;
    private string[] _surfaceIDs;

    // Occupant dropdown data
    private string[] _occupantDisplayNames;
    private string[] _occupantIDs;

    private bool _cacheDirty = true;
    private bool _showSurfaces = true;
    private bool _showOccupants = true;

    // Section header styles
    private GUIStyle _sectionStyle;
    private GUIStyle _headerBoxStyle;

    private void OnEnable()
    {
        _target = (NeighborRulesConfig)target;
        _cacheDirty = true;
    }

    private void EnsureStyles()
    {
        if (_sectionStyle == null)
        {
            _sectionStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft
            };
        }
        if (_headerBoxStyle == null)
        {
            _headerBoxStyle = new GUIStyle("box")
            {
                padding = new RectOffset(8, 8, 6, 6)
            };
        }
    }

    // ─── Cache ────────────────────────────────────────────────────────────────

    private void UpdateCache()
    {
        if (_target.catalog == null)
        {
            _surfaceDisplayNames = new string[0];
            _surfaceIDs = new string[0];
            _occupantDisplayNames = new string[0];
            _occupantIDs = new string[0];
            _cacheDirty = false;
            return;
        }

        var surfaces = _target.catalog.Definitions
            .Where(d => d.Layer == ObjectLayer.Surface && !string.IsNullOrEmpty(d.ID))
            .OrderBy(d => d.Name)
            .ToList();

        _surfaceDisplayNames = surfaces.Select(d => $"{d.Name}  [{d.ID}]").ToArray();
        _surfaceIDs = surfaces.Select(d => d.ID).ToArray();

        var occupants = _target.catalog.Definitions
            .Where(d => d.Layer == ObjectLayer.Occupant && !string.IsNullOrEmpty(d.ID))
            .OrderBy(d => d.Name)
            .ToList();

        _occupantDisplayNames = occupants.Select(d => $"{d.Name}  [{d.ID}]").ToArray();
        _occupantIDs = occupants.Select(d => d.ID).ToArray();

        _cacheDirty = false;
    }

    // ─── Inspector ────────────────────────────────────────────────────────────

    public override void OnInspectorGUI()
    {
        if (_target == null) return;
        EnsureStyles();
        serializedObject.Update();

        // Catalog field
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("catalog"));
        if (EditorGUI.EndChangeCheck()) _cacheDirty = true;

        if (_target.catalog == null)
        {
            EditorGUILayout.HelpBox("Assign a PrefabCatalog to enable editing.", MessageType.Error);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        if (_cacheDirty) UpdateCache();

        EditorGUILayout.Space(6);

        // ── Surface Rules section ─────────────────────────────────────────────
        DrawSectionHeader("Surface Adjacency Rules", Color.cyan,
            "Used by the safe-path WFC blend pass.\n" +
            "Controls which surface tiles may appear next to each other in the blend zone.\n" +
            "Rules are allow / deny only. Use Auto-Fix Symmetry to keep rules consistent.");

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Sync from Catalog")) SyncRules(_target.surfaceRules, ObjectLayer.Surface);
        if (GUILayout.Button("Auto-Fix Symmetry")) FixSymmetry(_target.surfaceRules);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);

        _showSurfaces = EditorGUILayout.Foldout(_showSurfaces, $"Surface Rules  ({_target.surfaceRules.Count})", true, EditorStyles.foldoutHeader);
        if (_showSurfaces)
        {
            EditorGUI.indentLevel++;
            DrawRuleList(
                serializedObject.FindProperty("surfaceRules"),
                _surfaceIDs, _surfaceDisplayNames,
                labelSelf: "Surface:",
                showWeight: false);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(10);

        // ── Occupant Rules section ────────────────────────────────────────────
        DrawSectionHeader("Occupant Neighbor Rules", Color.yellow,
            "Controls which occupants may appear next to each other.\n" +
            "If an explicit allow list exists for a direction, it becomes exclusive.\n" +
            "Denied rules always take priority.");

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Sync from Catalog")) SyncRules(_target.occupantRules, ObjectLayer.Occupant);
        if (GUILayout.Button("Auto-Fix Symmetry")) FixSymmetry(_target.occupantRules);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);

        _showOccupants = EditorGUILayout.Foldout(_showOccupants, $"Occupant Rules  ({_target.occupantRules.Count})", true, EditorStyles.foldoutHeader);
        if (_showOccupants)
        {
            EditorGUI.indentLevel++;
            DrawRuleList(
                serializedObject.FindProperty("occupantRules"),
                _occupantIDs, _occupantDisplayNames,
                labelSelf: "Occupant:",
                showWeight: false);
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }

    // ─── Section header ───────────────────────────────────────────────────────

    private void DrawSectionHeader(string title, Color accentColor, string tooltip)
    {
        var rect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        var labelRect = GUILayoutUtility.GetRect(GUIContent.none, _sectionStyle, GUILayout.ExpandWidth(true));

        // Accent bar on the left
        EditorGUI.DrawRect(new Rect(labelRect.x - 2, labelRect.y, 3, labelRect.height), accentColor);
        EditorGUI.LabelField(labelRect, new GUIContent(title, tooltip), _sectionStyle);
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2);
    }

    // ─── Rule list ────────────────────────────────────────────────────────────

    private void DrawRuleList(
        SerializedProperty list,
        string[] ids, string[] displayNames,
        string labelSelf, bool showWeight)
    {
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            SerializedProperty selfProp = entry.FindPropertyRelative("selfID");

            string currentId = selfProp.stringValue;
            int currentIndex = System.Array.IndexOf(ids, currentId);
            string entryLabel = currentIndex >= 0
                ? displayNames[currentIndex]
                : (string.IsNullOrEmpty(currentId) ? "[New Entry — select below]" : $"⚠ {currentId} (not in catalog)");

            // Row foldout
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            entry.isExpanded = EditorGUILayout.Foldout(entry.isExpanded, entryLabel, true);
            if (GUILayout.Button("✕", GUILayout.Width(22)))
            {
                list.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                i--;
                continue;
            }
            EditorGUILayout.EndHorizontal();

            if (entry.isExpanded)
            {
                EditorGUI.indentLevel++;

                // Self ID picker
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(labelSelf);
                int newIndex = EditorGUILayout.Popup(currentIndex, displayNames);
                if (newIndex >= 0 && newIndex < ids.Length)
                    selfProp.stringValue = ids[newIndex];
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(3);

                // Allowed
                DrawConstraints(
                    entry.FindPropertyRelative("allowed"),
                    ids, displayNames,
                    "Allowed Neighbors", isAllowed: true, showWeight: showWeight);

                EditorGUILayout.Space(2);

                // Denied
                DrawConstraints(
                    entry.FindPropertyRelative("denied"),
                    ids, displayNames,
                    "Denied Neighbors", isAllowed: false, showWeight: false);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        EditorGUILayout.Space(2);
        if (GUILayout.Button($"+ Add Entry"))
        {
            list.InsertArrayElementAtIndex(list.arraySize);
            var newEntry = list.GetArrayElementAtIndex(list.arraySize - 1);
            newEntry.FindPropertyRelative("selfID").stringValue = "";
            newEntry.FindPropertyRelative("allowed").ClearArray();
            newEntry.FindPropertyRelative("denied").ClearArray();
            newEntry.isExpanded = true;
        }
    }

    // ─── Constraints (allowed / denied rows) ─────────────────────────────────

    private void DrawConstraints(
        SerializedProperty list,
        string[] ids, string[] displayNames,
        string label, bool isAllowed, bool showWeight)
    {
        Color headerColor = isAllowed
            ? new Color(0.6f, 1f, 0.6f, 0.15f)
            : new Color(1f, 0.5f, 0.5f, 0.15f);

        list.isExpanded = EditorGUILayout.Foldout(
            list.isExpanded,
            $"{label}  ({list.arraySize})",
            true);

        if (!list.isExpanded) return;

        EditorGUI.indentLevel++;

        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty item = list.GetArrayElementAtIndex(i);
            SerializedProperty neighborID = item.FindPropertyRelative("neighborID");
            SerializedProperty directions = item.FindPropertyRelative("directions");

            // Tinted row box
            var bgRect = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(bgRect, headerColor);

            EditorGUILayout.BeginHorizontal();

            // Neighbor dropdown
            string currentID = neighborID.stringValue;
            int curIndex = System.Array.IndexOf(ids, currentID);
            int newIndex = EditorGUILayout.Popup(curIndex, displayNames, GUILayout.MinWidth(160));
            if (newIndex >= 0 && newIndex < ids.Length)
                neighborID.stringValue = ids[newIndex];
            else if (curIndex < 0 && !string.IsNullOrEmpty(currentID))
                EditorGUILayout.LabelField($"⚠ {currentID}", GUILayout.Width(120));

            // Direction flags inline
            directions.enumValueFlag = (int)(NeighborRulesConfig.DirectionMask)
                EditorGUILayout.EnumFlagsField(
                    (NeighborRulesConfig.DirectionMask)directions.enumValueFlag,
                    GUILayout.MinWidth(130));

            if (GUILayout.Button("✕", GUILayout.Width(22)))
            {
                list.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                i--;
                continue;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(1);
        }

        EditorGUILayout.Space(2);
        if (GUILayout.Button($"+ {(isAllowed ? "Allow" : "Deny")}"))
        {
            list.InsertArrayElementAtIndex(list.arraySize);
            var newItem = list.GetArrayElementAtIndex(list.arraySize - 1);
            newItem.FindPropertyRelative("neighborID").stringValue = "";
            newItem.FindPropertyRelative("directions").enumValueFlag =
                (int)NeighborRulesConfig.DirectionMask.All;
        }

        EditorGUI.indentLevel--;
    }

    // ─── Sync ─────────────────────────────────────────────────────────────────

    private void SyncRules(List<NeighborRulesConfig.NeighborEntry> list, ObjectLayer layer)
    {
        if (_target.catalog == null) return;

        Undo.RecordObject(_target, "Sync Rules from Catalog");

        int added = 0;
        foreach (var def in _target.catalog.Definitions)
        {
            if (string.IsNullOrEmpty(def.ID)) continue;
            if (def.Layer != layer) continue;
            if (list.Any(e => e.selfID == def.ID)) continue;

            list.Add(new NeighborRulesConfig.NeighborEntry { selfID = def.ID });
            added++;
        }

        EditorUtility.SetDirty(_target);
        _cacheDirty = true;
        Debug.Log($"[NeighborRules] Synced {layer} rules — added {added} new entr{(added == 1 ? "y" : "ies")}.");
    }

    // ─── Symmetry fix ─────────────────────────────────────────────────────────

    private void FixSymmetry(List<NeighborRulesConfig.NeighborEntry> list)
    {
        Undo.RecordObject(_target, "Auto-Fix Symmetry");

        bool changed = false;
        FixSymmetryForList(list, ref changed);

        if (changed) EditorUtility.SetDirty(_target);
        Debug.Log(changed
            ? "[NeighborRules] Symmetry fixed — see Console for details."
            : "[NeighborRules] Symmetry check complete — no changes needed.");
    }

    private void FixSymmetryForList(
        List<NeighborRulesConfig.NeighborEntry> list, ref bool changed)
    {
        var snapshot = new List<NeighborRulesConfig.NeighborEntry>(list);

        foreach (var entryA in snapshot)
        {
            // Reflect allowed
            foreach (var c in new List<NeighborRulesConfig.NeighborConstraint>(entryA.allowed))
            {
                if (string.IsNullOrEmpty(c.neighborID)) continue;
                var entryB = GetOrCreate(list, c.neighborID, ref changed);
                var oppositeDir = GetOppositeMask(c.directions);
                var existing = entryB.allowed.FirstOrDefault(x => x.neighborID == entryA.selfID);
                if (existing == null)
                {
                    entryB.allowed.Add(new NeighborRulesConfig.NeighborConstraint
                    { neighborID = entryA.selfID, directions = oppositeDir });
                    changed = true;
                    Debug.Log($"[Symmetry] {entryB.selfID} now allows {entryA.selfID}");
                }
                else if ((existing.directions & oppositeDir) != oppositeDir)
                {
                    existing.directions |= oppositeDir;
                    changed = true;
                    Debug.Log($"[Symmetry] {entryB.selfID} allow {entryA.selfID} directions expanded");
                }
            }

            // Reflect denied
            foreach (var d in new List<NeighborRulesConfig.NeighborDenial>(entryA.denied))
            {
                if (string.IsNullOrEmpty(d.neighborID)) continue;
                var entryB = GetOrCreate(list, d.neighborID, ref changed);
                var oppositeDir = GetOppositeMask(d.directions);
                var existing = entryB.denied.FirstOrDefault(x => x.neighborID == entryA.selfID);
                if (existing == null)
                {
                    entryB.denied.Add(new NeighborRulesConfig.NeighborDenial
                    { neighborID = entryA.selfID, directions = oppositeDir });
                    changed = true;
                    Debug.Log($"[Symmetry] {entryB.selfID} now denies {entryA.selfID}");
                }
                else if ((existing.directions & oppositeDir) != oppositeDir)
                {
                    existing.directions |= oppositeDir;
                    changed = true;
                    Debug.Log($"[Symmetry] {entryB.selfID} deny {entryA.selfID} directions expanded");
                }
            }
        }
    }

    private NeighborRulesConfig.NeighborEntry GetOrCreate(
        List<NeighborRulesConfig.NeighborEntry> list, string id, ref bool changed)
    {
        var entry = list.FirstOrDefault(e => e.selfID == id);
        if (entry != null) return entry;
        entry = new NeighborRulesConfig.NeighborEntry { selfID = id };
        list.Add(entry);
        changed = true;
        Debug.Log($"[Symmetry] Created new entry for '{id}'");
        return entry;
    }

    // ─── Direction helpers ────────────────────────────────────────────────────

    private NeighborRulesConfig.DirectionMask GetOppositeMask(NeighborRulesConfig.DirectionMask mask)
    {
        var result = NeighborRulesConfig.DirectionMask.None;
        if ((mask & NeighborRulesConfig.DirectionMask.Forward) != 0) result |= NeighborRulesConfig.DirectionMask.Backward;
        if ((mask & NeighborRulesConfig.DirectionMask.Backward) != 0) result |= NeighborRulesConfig.DirectionMask.Forward;
        if ((mask & NeighborRulesConfig.DirectionMask.Left) != 0) result |= NeighborRulesConfig.DirectionMask.Right;
        if ((mask & NeighborRulesConfig.DirectionMask.Right) != 0) result |= NeighborRulesConfig.DirectionMask.Left;
        if ((mask & NeighborRulesConfig.DirectionMask.Corners) != 0) result |= NeighborRulesConfig.DirectionMask.Corners;
        return result;
    }
}
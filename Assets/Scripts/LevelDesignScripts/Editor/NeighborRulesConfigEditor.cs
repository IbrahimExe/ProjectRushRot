using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using LevelGenerator.Data;
using System.Linq;

[CustomEditor(typeof(NeighborRulesConfig))]
public class NeighborRulesConfigEditor : Editor
{
    private NeighborRulesConfig _target;
    
    // UI Cache
    private string[] _surfaceDisplayNames;
    private string[] _surfaceIDs;
    private bool _cacheDirty = true;

    private void OnEnable()
    {
        _target = (NeighborRulesConfig)target;
        _cacheDirty = true;
    }

    private void UpdateCache()
    {
        if (_target.catalog == null)
        {
            _surfaceDisplayNames = new string[0];
            _surfaceIDs = new string[0];
            return;
        }

        var surfaces = _target.catalog.Definitions
            .Where(d => d.Layer == ObjectLayer.Surface && !string.IsNullOrEmpty(d.ID))
            .ToList();

        _surfaceDisplayNames = surfaces.Select(d => $"{d.Name} ({d.ID})").ToArray();
        _surfaceIDs = surfaces.Select(d => d.ID).ToArray();
        _cacheDirty = false;
    }

    public override void OnInspectorGUI()
    {
        if (_target == null) return;
        
        serializedObject.Update();

        // 1. Catalog Field
        EditorGUILayout.PropertyField(serializedObject.FindProperty("catalog"));
        
        if (_target.catalog == null)
        {
            EditorGUILayout.HelpBox("Assign a PrefabCatalog to enable editing features.", MessageType.Error);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        if (_cacheDirty) UpdateCache();

        // 2. Buttons
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Sync from Catalog")) SyncFromCatalog();
        if (GUILayout.Button("Auto-Fix Symmetry")) ValidateAndFixSymmetry();
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Validate Constraints")) ValidateConstraints();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Constraints", EditorStyles.boldLabel);

        // 3. Manual List Drawing
        SerializedProperty entries = serializedObject.FindProperty("entries");

        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            SerializedProperty selfIDProp = entry.FindPropertyRelative("selfID");
            
            // Header
            string currentId = selfIDProp.stringValue;
            string label = string.IsNullOrEmpty(currentId) ? "[New Entry]" : GetDisplayName(currentId);
            
            entry.isExpanded = EditorGUILayout.Foldout(entry.isExpanded, label, true);
            
            if (entry.isExpanded)
            {
                EditorGUI.indentLevel++;
                
                // Self ID Dropdown
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Surface:");
                int currentIndex = System.Array.IndexOf(_surfaceIDs, currentId);
                int newIndex = EditorGUILayout.Popup(currentIndex, _surfaceDisplayNames);
                if (newIndex >= 0 && newIndex < _surfaceIDs.Length)
                {
                    selfIDProp.stringValue = _surfaceIDs[newIndex];
                }
                
                if (GUILayout.Button("Remove Entry", GUILayout.Width(100)))
                {
                    entries.DeleteArrayElementAtIndex(i);
                    i--;
                    EditorGUI.indentLevel--;
                    continue;
                }
                EditorGUILayout.EndHorizontal();

                // Allowed List
                DrawConstraintList(entry.FindPropertyRelative("allowed"), "Allowed Neighbors");
                
                // Denied List
                DrawConstraintList(entry.FindPropertyRelative("denied"), "Denied Neighbors");

                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }
        }

        if (GUILayout.Button("Add New Entry"))
        {
            entries.InsertArrayElementAtIndex(entries.arraySize);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawConstraintList(SerializedProperty list, string label)
    {
        list.isExpanded = EditorGUILayout.Foldout(list.isExpanded, $"{label} ({list.arraySize})");
        if (!list.isExpanded) return;

        EditorGUI.indentLevel++;
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty constraint = list.GetArrayElementAtIndex(i);
            SerializedProperty idProp = constraint.FindPropertyRelative("neighborID");
            SerializedProperty dirProp = constraint.FindPropertyRelative("directions");
            SerializedProperty weightProp = constraint.FindPropertyRelative("weight");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Neighbor Dropdown
            EditorGUILayout.BeginHorizontal();
            int curIndex = System.Array.IndexOf(_surfaceIDs, idProp.stringValue);
            int newIndex = EditorGUILayout.Popup(curIndex, _surfaceDisplayNames, GUILayout.MinWidth(150));
            if (newIndex >= 0) idProp.stringValue = _surfaceIDs[newIndex];
            else if (curIndex == -1) // If manual string or invalid
            {
                // Fallback text field for custom/error values
                idProp.stringValue = EditorGUILayout.TextField(idProp.stringValue);
            }

            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                list.DeleteArrayElementAtIndex(i);
                i--;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                continue;
            }
            EditorGUILayout.EndHorizontal();

            // Directions & Weight
            EditorGUILayout.PropertyField(dirProp, new GUIContent("Directions"));
            if (weightProp != null) // Denied list might reuse class but ignore weight visually? No, reuse class.
            {
                EditorGUILayout.PropertyField(weightProp);
            }

            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button("+ Add Neighbor"))
        {
            list.InsertArrayElementAtIndex(list.arraySize);
            var newElem = list.GetArrayElementAtIndex(list.arraySize - 1);
            newElem.FindPropertyRelative("weight").floatValue = 1.0f;
            newElem.FindPropertyRelative("directions").intValue = 0; // None
            newElem.FindPropertyRelative("neighborID").stringValue = "";
        }
        EditorGUI.indentLevel--;
    }

    private string GetDisplayName(string id)
    {
        if (_target.catalog == null) return id;
        var def = _target.catalog.GetByID(id);
        return def != null ? def.Name : id;
    }

    // --- LOGIC ---

    private void SyncFromCatalog()
    {
        Undo.RecordObject(_target, "Sync Neighbor Rules");
        bool changed = false;
        var surfaces = _target.catalog.Definitions.Where(d => d.Layer == ObjectLayer.Surface).ToList();

        foreach (var def in surfaces)
        {
            if (string.IsNullOrEmpty(def.ID)) continue;
            if (!_target.entries.Any(e => e.selfID == def.ID))
            {
                _target.entries.Add(new NeighborRulesConfig.NeighborEntry { selfID = def.ID });
                changed = true;
            }
        }
        _cacheDirty = true;
        if (changed) Debug.Log("Sync Complete: Added missing entries.");
        else Debug.Log("Sync Complete: No changes.");
    }

    private void ValidateAndFixSymmetry()
    {
        Undo.RecordObject(_target, "Auto-Fix Symmetry");
        bool changed = false;
        var entryMap = _target.entries.Where(e => !string.IsNullOrEmpty(e.selfID)).ToDictionary(e => e.selfID);

        // Helper to ensure B's entry exists
        NeighborRulesConfig.NeighborEntry GetOrCreateEntry(string id)
        {
            if (entryMap.TryGetValue(id, out var e)) return e;
            var newE = new NeighborRulesConfig.NeighborEntry { selfID = id };
            _target.entries.Add(newE);
            entryMap[id] = newE;
            changed = true;
            return newE;
        }

        foreach (var entryA in _target.entries)
        {
            if (string.IsNullOrEmpty(entryA.selfID)) continue;

            // ALLOWED
            foreach (var constA in entryA.allowed)
            {
                if (string.IsNullOrEmpty(constA.neighborID) || constA.directions == NeighborRulesConfig.DirectionMask.None) continue;

                var entryB = GetOrCreateEntry(constA.neighborID);
                var requiredMaskForB = GetOppositeMask(constA.directions);

                // Check if B already covers these directions for A
                // We must ensure for EVERY bit in requiredMaskForB, B->A allows it.
                // We iterate existing constraints on B->A
                
                var existingConstraints = entryB.allowed.Where(c => c.neighborID == entryA.selfID).ToList();
                var coveredMask = NeighborRulesConfig.DirectionMask.None;
                foreach(var ec in existingConstraints) coveredMask |= ec.directions;

                // What is missing?
                var missingMask = requiredMaskForB & ~coveredMask; // bits in required but not covered

                if (missingMask != NeighborRulesConfig.DirectionMask.None)
                {
                    // Add new constraint or append to existing?
                    // Simpler to add new specific constraint or append to first match
                    var match = existingConstraints.FirstOrDefault();
                    if (match != null && Mathf.Abs(match.weight - constA.weight) < 0.001f)
                    {
                        // Merge constraints
                        match.directions |= missingMask;
                        changed = true;
                        Debug.Log($"Merged directions for {entryB.selfID}->{entryA.selfID}");
                    }
                    else
                    {
                        // Add new
                        entryB.allowed.Add(new NeighborRulesConfig.NeighborConstraint
                        {
                            neighborID = entryA.selfID,
                            directions = missingMask,
                            weight = constA.weight
                        });
                        changed = true;
                        Debug.Log($"Added missing symmetry: {entryB.selfID} allows {entryA.selfID} ({missingMask})");
                    }
                }
            }

            // DENIED (Logic same as allowed but weight 0)
            foreach (var constA in entryA.denied)
            {
                if (string.IsNullOrEmpty(constA.neighborID) || constA.directions == NeighborRulesConfig.DirectionMask.None) continue;

                var entryB = GetOrCreateEntry(constA.neighborID);
                var requiredMaskForB = GetOppositeMask(constA.directions);

                var existingConstraints = entryB.denied.Where(c => c.neighborID == entryA.selfID).ToList();
                var coveredMask = NeighborRulesConfig.DirectionMask.None;
                foreach(var ec in existingConstraints) coveredMask |= ec.directions;

                var missingMask = requiredMaskForB & ~coveredMask;

                if (missingMask != NeighborRulesConfig.DirectionMask.None)
                {
                    var match = existingConstraints.FirstOrDefault();
                    if (match != null)
                    {
                        match.directions |= missingMask;
                        changed = true;
                    }
                    else
                    {
                        entryB.denied.Add(new NeighborRulesConfig.NeighborConstraint
                        {
                            neighborID = entryA.selfID,
                            directions = missingMask,
                            weight = 0
                        });
                        changed = true;
                        Debug.Log($"Added missing denial: {entryB.selfID} denies {entryA.selfID}");
                    }
                }
            }
        }

        if (changed) Debug.Log("Symmetry Auto-Fix Applied.");
        else Debug.Log("Symmetry Verified: Detailed checks passed.");
    }

    private void ValidateConstraints()
    {
        foreach (var entry in _target.entries)
        {
            // Simple conflict check: Is same neighbor allowed AND denied for same direction bit?
            
            // Build map of Denied Directions per Neighbor
            var deniedMap = new Dictionary<string, NeighborRulesConfig.DirectionMask>();
            foreach(var d in entry.denied)
            {
                if(string.IsNullOrEmpty(d.neighborID)) continue;
                if(!deniedMap.ContainsKey(d.neighborID)) deniedMap[d.neighborID] = NeighborRulesConfig.DirectionMask.None;
                deniedMap[d.neighborID] |= d.directions;
            }

            foreach(var a in entry.allowed)
            {
                if(string.IsNullOrEmpty(a.neighborID)) continue;
                if(deniedMap.TryGetValue(a.neighborID, out var deniedMask))
                {
                    // Intersection?
                    var conflict = a.directions & deniedMask;
                    if (conflict != NeighborRulesConfig.DirectionMask.None)
                    {
                        Debug.LogError($"Conflict in {GetDisplayName(entry.selfID)}: {GetDisplayName(a.neighborID)} is Allowed & Denied for ({conflict})");
                    }
                }

                if(a.weight < 0) Debug.LogError($"Negative weight in {entry.selfID}->{a.neighborID}");
            }
        }
        Debug.Log("Validation Check Complete.");
    }

    private NeighborRulesConfig.DirectionMask GetOppositeMask(NeighborRulesConfig.DirectionMask mask)
    {
        NeighborRulesConfig.DirectionMask result = NeighborRulesConfig.DirectionMask.None;
        if ((mask & NeighborRulesConfig.DirectionMask.Forward) != 0) result |= NeighborRulesConfig.DirectionMask.Backward;
        if ((mask & NeighborRulesConfig.DirectionMask.Backward) != 0) result |= NeighborRulesConfig.DirectionMask.Forward;
        if ((mask & NeighborRulesConfig.DirectionMask.Left) != 0) result |= NeighborRulesConfig.DirectionMask.Right;
        if ((mask & NeighborRulesConfig.DirectionMask.Right) != 0) result |= NeighborRulesConfig.DirectionMask.Left;
        return result;
    }
}

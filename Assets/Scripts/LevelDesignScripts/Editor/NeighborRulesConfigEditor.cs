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
    
    // Occupants are now handled via "Allowed Surfaces" in PrefabDef, 
    // but if we keep looking at neighbor rules for surfaces, we need the surface cache.
    // The user requested removing neighbor rules for occupants.

    private bool _cacheDirty = true;
    private bool _showSurfaces = true;

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

        // 2. Tools
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Sync Surface Rules")) SyncSurfaceRules(_target);
        if (GUILayout.Button("Auto-Fix Symmetry")) ValidateAndFixSymmetry(_target);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        
        // 3. Surface Rules ONLY (Occupants use Budget/AllowedSurfaces now)
        _showSurfaces = EditorGUILayout.Foldout(_showSurfaces, "Surface Neighbor Rules", true);
        if (_showSurfaces)
        {
            DrawSurfaceRules(serializedObject.FindProperty("surfaceRules"));
        }

        // Occupant rules are deprecated/removed from this view as per user request
        // "occupants should only check the surface their on not their neighboors" -> This is done in PrefabDef now.

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSurfaceRules(SerializedProperty list)
    {
        EditorGUI.indentLevel++;
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            SerializedProperty selfIDProp = entry.FindPropertyRelative("selfID");
            
            // Allow selecting the Self ID via Dropdown (if new entry) or just Label
            string currentId = selfIDProp.stringValue;
            int currentIndex = System.Array.IndexOf(_surfaceIDs, currentId);
            string entryLabel = (currentIndex >= 0 && currentIndex < _surfaceDisplayNames.Length) 
                ? _surfaceDisplayNames[currentIndex] 
                : (string.IsNullOrEmpty(currentId) ? "[New Entry]" : currentId);

            entry.isExpanded = EditorGUILayout.Foldout(entry.isExpanded, entryLabel, true);
            
            if (entry.isExpanded)
            {
                EditorGUI.indentLevel++;
                
                // Self ID Selection
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Surface:");
                int newIndex = EditorGUILayout.Popup(currentIndex, _surfaceDisplayNames);
                if (newIndex >= 0 && newIndex < _surfaceIDs.Length)
                {
                    selfIDProp.stringValue = _surfaceIDs[newIndex];
                }
                
                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    list.DeleteArrayElementAtIndex(i);
                    i--;
                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndHorizontal();
                    continue;
                }
                EditorGUILayout.EndHorizontal();

                // Allowed List
                DrawConstraints(entry.FindPropertyRelative("allowed"), "Allowed Neighbors", isAllowed: true);
                
                // Denied List
                DrawConstraints(entry.FindPropertyRelative("denied"), "Denied Neighbors", isAllowed: false);

                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }
        }

        if (GUILayout.Button("Add New Surface Rule"))
        {
            list.InsertArrayElementAtIndex(list.arraySize);
        }
        EditorGUI.indentLevel--;
    }

    private void DrawConstraints(SerializedProperty list, string label, bool isAllowed)
    {
        list.isExpanded = EditorGUILayout.Foldout(list.isExpanded, $"{label} ({list.arraySize})");
        if (!list.isExpanded) return;

        EditorGUI.indentLevel++;
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty item = list.GetArrayElementAtIndex(i);
            SerializedProperty neighborID = item.FindPropertyRelative("neighborID");
            SerializedProperty directions = item.FindPropertyRelative("directions");
            
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            
            // Neighbor ID Dropdown
            string currentID = neighborID.stringValue;
            int curIndex = System.Array.IndexOf(_surfaceIDs, currentID);
            int newIndex = EditorGUILayout.Popup(curIndex, _surfaceDisplayNames, GUILayout.MinWidth(150));
            if (newIndex >= 0 && newIndex < _surfaceIDs.Length)
            {
                neighborID.stringValue = _surfaceIDs[newIndex];
            }
            else if (curIndex == -1 && !string.IsNullOrEmpty(currentID))
            {
                // Keep existing ID if not in list (missing reference?)
                EditorGUILayout.LabelField(currentID, GUILayout.Width(100));
            }
            
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                list.DeleteArrayElementAtIndex(i);
                i--;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                continue;
            }
            EditorGUILayout.EndHorizontal();

            // Directions
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Directions:");
            directions.enumValueFlag = (int)(NeighborRulesConfig.DirectionMask)EditorGUILayout.EnumFlagsField((NeighborRulesConfig.DirectionMask)directions.enumValueFlag);
            EditorGUILayout.EndHorizontal();

            // Weight (Only for Allowed)
            if (isAllowed)
            {
                SerializedProperty weight = item.FindPropertyRelative("weight");
                if (weight != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel("Weight:");
                    weight.floatValue = EditorGUILayout.Slider(weight.floatValue, 0f, 100f);
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        if (GUILayout.Button("Add Neighbor"))
        {
            list.InsertArrayElementAtIndex(list.arraySize);
            var newItem = list.GetArrayElementAtIndex(list.arraySize - 1);
            if (isAllowed) 
            { 
                newItem.FindPropertyRelative("weight").floatValue = 10f; 
            }
            newItem.FindPropertyRelative("directions").enumValueFlag = (int)NeighborRulesConfig.DirectionMask.Forward;
        }
        EditorGUI.indentLevel--;
    }

    // --- LOGIC ---

    private void SyncSurfaceRules(NeighborRulesConfig config)
    {
        if (config.catalog == null) return;

        int added = 0;
        foreach (var def in config.catalog.Definitions)
        {
            if (string.IsNullOrEmpty(def.ID)) continue;
            if (def.Layer != ObjectLayer.Surface) continue;

            if (!config.surfaceRules.Any(e => e.selfID == def.ID))
            {
                config.surfaceRules.Add(new NeighborRulesConfig.NeighborEntry { selfID = def.ID });
                added++;
            }
        }
        Debug.Log($"Synced surface rules. Added {added} entries.");
        _cacheDirty = true;
    }

    private void ValidateAndFixSymmetry(NeighborRulesConfig config)
    {
        bool changed = false;
        FixSymmetryForList(config.surfaceRules, config.surfaceRules, ref changed);
        if (changed) EditorUtility.SetDirty(config);
        Debug.Log("Symmetry check complete.");
    }

    private void FixSymmetryForList(List<NeighborRulesConfig.NeighborEntry> list, List<NeighborRulesConfig.NeighborEntry> ruleSet, ref bool changed)
    {
        // Work on a snapshot of the list to allow adding new entries safely if needed
        var snapshot = new List<NeighborRulesConfig.NeighborEntry>(list);

        foreach (var entryA in snapshot)
        {
            // 1. REFLECT ALLOWED
            var allowedSnapshot = new List<NeighborRulesConfig.NeighborConstraint>(entryA.allowed);
            foreach (var constraint in allowedSnapshot)
            {
                if (string.IsNullOrEmpty(constraint.neighborID)) continue;

                // Find or Create Entry B
                var entryB = ruleSet.FirstOrDefault(e => e.selfID == constraint.neighborID);
                if (entryB == null)
                {
                     entryB = new NeighborRulesConfig.NeighborEntry { selfID = constraint.neighborID };
                     ruleSet.Add(entryB);
                     changed = true;
                     Debug.Log($"Auto-Fixed: Created new rule entry for {constraint.neighborID} (Symmetry)");
                }

                var requiredDirFromB = GetOppositeMask(constraint.directions);
                
                // Does B allow A in the required direction?
                var matchingConstraint = entryB.allowed.FirstOrDefault(c => c.neighborID == entryA.selfID);
                
                if (matchingConstraint == null)
                {
                    // Case 1: No rule at all -> Add it
                    entryB.allowed.Add(new NeighborRulesConfig.NeighborConstraint 
                    { 
                        neighborID = entryA.selfID, 
                        directions = requiredDirFromB,
                        weight = constraint.weight 
                    });
                    changed = true;
                    Debug.Log($"Auto-Fixed: {entryB.selfID} now allows {entryA.selfID} (Symmetry)");
                }
                else
                {
                    // Case 2: Exists, but maybe missing directions?
                    if ((matchingConstraint.directions & requiredDirFromB) != requiredDirFromB)
                    {
                        matchingConstraint.directions |= requiredDirFromB;
                        changed = true;
                        Debug.Log($"Auto-Fixed: Updated {entryB.selfID} to allow {entryA.selfID} in more directions (Symmetry)");
                    }
                }
            }

            // 2. REFLECT DENIED
            var deniedSnapshot = new List<NeighborRulesConfig.NeighborDenial>(entryA.denied);
            foreach (var denial in deniedSnapshot)
            {
                if (string.IsNullOrEmpty(denial.neighborID)) continue;

                var entryB = ruleSet.FirstOrDefault(e => e.selfID == denial.neighborID);
                if (entryB == null)
                {
                     entryB = new NeighborRulesConfig.NeighborEntry { selfID = denial.neighborID };
                     ruleSet.Add(entryB);
                     changed = true;
                     Debug.Log($"Auto-Fixed: Created new rule entry for {denial.neighborID} (Symmetry from Denial)");
                }

                var requiredDirFromB = GetOppositeMask(denial.directions);

                var matchingDenial = entryB.denied.FirstOrDefault(d => d.neighborID == entryA.selfID);
                
                if (matchingDenial == null)
                {
                     entryB.denied.Add(new NeighborRulesConfig.NeighborDenial 
                     { 
                         neighborID = entryA.selfID, 
                         directions = requiredDirFromB 
                     });
                     changed = true;
                     Debug.Log($"Auto-Fixed: {entryB.selfID} now denies {entryA.selfID} (Symmetry)");
                }
                else
                {
                    if ((matchingDenial.directions & requiredDirFromB) != requiredDirFromB)
                    {
                        matchingDenial.directions |= requiredDirFromB;
                        changed = true;
                        Debug.Log($"Auto-Fixed: Updated {entryB.selfID} to deny {entryA.selfID} in more directions (Symmetry)");
                    }
                }
            }
        }
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

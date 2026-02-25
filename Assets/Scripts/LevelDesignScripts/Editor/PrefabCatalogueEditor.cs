using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using LevelGenerator.Data;

/// <summary>
/// Custom property drawer for PrefabDef that adds a dropdown for AllowedSurfaceIDs
/// Place this file in an "Editor" folder in your project
/// </summary>
[CustomPropertyDrawer(typeof(PrefabDef))]
public class PrefabDefDrawer : PropertyDrawer
{
    private bool showAllowedSurfaces = false;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // Use default property drawer height
        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Draw the default property
        EditorGUI.PropertyField(position, property, label, true);
    }
}

/// <summary>
/// Custom editor for PrefabCatalog that adds UI for managing AllowedSurfaceIDs
/// Place this file in an "Editor" folder in your project
/// </summary>
[CustomEditor(typeof(PrefabCatalog))]
public class PrefabCatalogEditor : Editor
{
    private Dictionary<PrefabDef, bool> foldouts = new Dictionary<PrefabDef, bool>();
    private Dictionary<PrefabDef, int> selectedSurfaceIndex = new Dictionary<PrefabDef, int>();

    public override void OnInspectorGUI()
    {
        PrefabCatalog catalog = (PrefabCatalog)target;

        // Draw default inspector first
        DrawDefaultInspector();

        // Only show surface ID manager for occupants
        var occupants = catalog.Definitions?.Where(d => d != null && d.Layer == ObjectLayer.Occupant).ToList();
        if (occupants == null || occupants.Count == 0) return;

        // Collect all surface IDs
        var surfaceIDs = catalog.Definitions?
            .Where(d => d != null && d.Layer == ObjectLayer.Surface && !string.IsNullOrEmpty(d.ID))
            .Select(d => d.ID)
            .ToList();

        if (surfaceIDs == null || surfaceIDs.Count == 0)
        {
            EditorGUILayout.HelpBox("No surface definitions found in catalog. Create surfaces first.", MessageType.Warning);
            return;
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Allowed Surface IDs Manager", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Manage which surfaces each occupant can spawn on. Leave empty to allow any surface.", MessageType.Info);

        foreach (var occupant in occupants)
        {
            if (occupant == null) continue;

            EditorGUILayout.Space(5);

            // Foldout for this occupant
            if (!foldouts.ContainsKey(occupant)) foldouts[occupant] = false;

            foldouts[occupant] = EditorGUILayout.Foldout(
                foldouts[occupant],
                $"{occupant.Name} ({occupant.ID})",
                true
            );

            if (foldouts[occupant])
            {
                EditorGUI.indentLevel++;

                // Initialize AllowedSurfaceIDs if null
                if (occupant.AllowedSurfaceIDs == null)
                {
                    occupant.AllowedSurfaceIDs = new List<string>();
                }

                // Show current allowed surfaces
                if (occupant.AllowedSurfaceIDs.Count > 0)
                {
                    EditorGUILayout.LabelField("Allowed Surfaces:", EditorStyles.miniBoldLabel);
                    for (int i = 0; i < occupant.AllowedSurfaceIDs.Count; i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"  • {occupant.AllowedSurfaceIDs[i]}", GUILayout.Width(200));

                        // Remove button
                        if (GUILayout.Button("Remove", GUILayout.Width(70)))
                        {
                            occupant.AllowedSurfaceIDs.RemoveAt(i);
                            EditorUtility.SetDirty(catalog);
                            break;
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("No restrictions (can spawn on any surface)", EditorStyles.miniLabel);
                }

                EditorGUILayout.Space(3);

                // Dropdown to add new surface
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Add Surface:", GUILayout.Width(90));

                if (!selectedSurfaceIndex.ContainsKey(occupant))
                {
                    selectedSurfaceIndex[occupant] = 0;
                }

                // Create dropdown with available surfaces (not already added)
                var availableSurfaces = surfaceIDs
                    .Where(id => !occupant.AllowedSurfaceIDs.Contains(id))
                    .ToList();

                if (availableSurfaces.Count > 0)
                {
                    // Ensure index is valid
                    if (selectedSurfaceIndex[occupant] >= availableSurfaces.Count)
                    {
                        selectedSurfaceIndex[occupant] = 0;
                    }

                    selectedSurfaceIndex[occupant] = EditorGUILayout.Popup(
                        selectedSurfaceIndex[occupant],
                        availableSurfaces.ToArray(),
                        GUILayout.Width(200)
                    );

                    // Add button
                    if (GUILayout.Button("Add", GUILayout.Width(60)))
                    {
                        string selectedID = availableSurfaces[selectedSurfaceIndex[occupant]];
                        occupant.AllowedSurfaceIDs.Add(selectedID);
                        selectedSurfaceIndex[occupant] = 0;
                        EditorUtility.SetDirty(catalog);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("(All surfaces already added)", EditorStyles.miniLabel);
                }

                EditorGUILayout.EndHorizontal();

                // Clear all button
                if (occupant.AllowedSurfaceIDs.Count > 0)
                {
                    EditorGUILayout.Space(3);
                    if (GUILayout.Button("Clear All Restrictions", GUILayout.Width(150)))
                    {
                        occupant.AllowedSurfaceIDs.Clear();
                        EditorUtility.SetDirty(catalog);
                    }
                }

                EditorGUI.indentLevel--;
            }
        }

        EditorGUILayout.Space(10);

        // Quick reference
        if (GUILayout.Button("Show All Surface IDs (Copy Reference)"))
        {
            string allIDs = string.Join("\n", surfaceIDs);
            EditorGUILayout.TextArea(allIDs, GUILayout.Height(100));
            GUIUtility.systemCopyBuffer = allIDs;
            Debug.Log($"Surface IDs copied to clipboard:\n{allIDs}");
        }
    }
}
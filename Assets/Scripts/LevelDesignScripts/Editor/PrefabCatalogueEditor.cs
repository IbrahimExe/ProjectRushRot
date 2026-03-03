// Editor/PrefabCatalogEditor.cs
// Place this file inside any Editor/ folder in your project.
// It hides the rightWallPrefabs list in the Inspector when
// mirrorRightFromLeft is enabled, preventing confusion.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PrefabCatalog))]
public class PrefabCatalogEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Draw everything up to (but not including) mirrorRightFromLeft manually
        // so we can conditionally show/hide rightWallPrefabs.
        DrawPropertiesExcluding(serializedObject,
            "mirrorRightFromLeft", "rightWallPrefabs");

        EditorGUILayout.Space(4);

        // Mirror toggle
        var mirrorProp = serializedObject.FindProperty("mirrorRightFromLeft");
        EditorGUILayout.PropertyField(mirrorProp);

        if (!mirrorProp.boolValue)
        {
            // Only show rightWallPrefabs when mirror is OFF
            var rightProp = serializedObject.FindProperty("rightWallPrefabs");
            EditorGUILayout.PropertyField(rightProp, includeChildren: true);
        }
        else
        {
            // Show a read-only info box instead
            EditorGUILayout.HelpBox(
                "Mirror Right From Left is ON — the right wall uses the same PrefabDefs " +
                "as the left wall, instantiated with a 180° Y rotation. " +
                "The Right Wall Prefabs list is ignored.",
                MessageType.Info);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
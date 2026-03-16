using UnityEngine;
using UnityEditor;
using LevelGenerator;

[CustomEditor(typeof(MapDisplay))]
public class MapDisplayEditor : Editor
{
    public override void OnInspectorGUI()
    {
        MapDisplay mapDisplay = (MapDisplay)target;

        if (DrawDefaultInspector())
        {
            if (mapDisplay.AutoUpdate)
                mapDisplay.GenerateChunk();
        }

        if (GUILayout.Button("Generate Chunk"))
            mapDisplay.GenerateChunk();
    }
}

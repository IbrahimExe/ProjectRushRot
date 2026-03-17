using UnityEditor;
using UnityEngine;
using LevelGenerator;

[CustomEditor(typeof(MapGenerator))]
public class MapGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        MapGenerator mapGen = (MapGenerator)target;

        if (DrawDefaultInspector() && mapGen.autoUpdate)
            mapGen.GenerateMap();

        if (GUILayout.Button("Generate", GUILayout.Height(28)))
            mapGen.GenerateMap();
    }
}
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
            mapGen.DrawMapInEditor();

        if (GUILayout.Button("Generate", GUILayout.Height(28)))
        {
            if (mapGen.useRandomSeed)
            {
                mapGen.seedString = WorldConfig.GenerateRandomSeedString();
            }
            mapGen.InitRandomSeed();
            mapGen.DrawMapInEditor();
        }
    }
}
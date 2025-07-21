using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelGenerator))]
public class LevelEditor : Editor
{
    private LevelGenerator generator;
    
    void OnEnable()
    {
        generator = (LevelGenerator)target;
    }
    
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        EditorGUILayout.Space(10);
        
        EditorGUILayout.LabelField("Generating Controls", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Generate New Level", GUILayout.Height(30)))
        {
            generator.GenerateLevel();
            SceneView.RepaintAll();
        }
        
        if (GUILayout.Button("Clear Level", GUILayout.Height(30)))
        {
            ClearDungeon();
            SceneView.RepaintAll();
        }
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        
        EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Small Level"))
        {
            generator.lvlWidth = 30;
            generator.lvlHeight = 30;
            generator.minRooms = 5;
            generator.maxRooms = 8;
            generator.GenerateLevel();
        }
        
        if (GUILayout.Button("Medium Level"))
        {
            generator.lvlWidth = 50;
            generator.lvlHeight = 50;
            generator.minRooms = 8;
            generator.maxRooms = 12;
            generator.GenerateLevel();
        }
        
        if (GUILayout.Button("Large Level"))
        {
            generator.lvlWidth = 80;
            generator.lvlHeight = 80;
            generator.minRooms = 12;
            generator.maxRooms = 20;
            generator.GenerateLevel();
        }
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(10);
        
        if (Application.isPlaying || generator.dungeonParent != null)
        {
            EditorGUILayout.LabelField("Level Info", EditorStyles.boldLabel);
            
            if (generator.dungeonParent != null)
            {
                int childCount = generator.dungeonParent.childCount;
                EditorGUILayout.LabelField($"Generated Objects: {childCount}");
            }
        }
        
        if (GUI.changed)
        {
            EditorUtility.SetDirty(generator);
        }
    }
    
    private void ClearDungeon()
    {
        if (generator.dungeonParent != null)
        {
            while (generator.dungeonParent.childCount > 0)
            {
                DestroyImmediate(generator.dungeonParent.GetChild(0).gameObject);
            }
        }
    }
}

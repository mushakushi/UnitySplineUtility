using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SplineCombinator))]
public class SplineCombinatorEditor : Editor
{
    private SplineCombinator script;

    private void OnEnable()
    {
        script = (SplineCombinator)target;
    }

    public override void OnInspectorGUI() 
    {
        serializedObject.Update();
        DrawDefaultInspector();
        if (GUILayout.Button("Generate Combined Spline"))
        {
            script.GenerateCombinedSpline();
        }
        serializedObject.ApplyModifiedProperties();
    }
}
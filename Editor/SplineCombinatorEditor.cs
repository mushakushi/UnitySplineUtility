using Mushakushi.Splines.Runtime;
using UnityEditor;
using UnityEngine;

namespace Mushakushi.Splines.Editor
{
    [CustomEditor(typeof(SplineCombinator))]
    public class SplineCombinatorEditor : UnityEditor.Editor
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
            // ReSharper disable once Unity.PerformanceCriticalCodeInvocation
            // this is just a button
            if (GUILayout.Button("Generate Combined Spline"))
            {
                script.GenerateCombinedSpline();
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
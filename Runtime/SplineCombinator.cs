using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace Mushakushi.Splines.Runtime
{
    /// <summary>
    /// Component that combines splines.
    /// </summary>
    public class SplineCombinator: MonoBehaviour
    {
        /// <summary>
        /// The <see cref="SplineContainer"/> place the combined splines within. 
        /// </summary>
        [SerializeField, Tooltip("The Spline Container to generate.")] 
        public SplineContainer sourceSplineContainer; 
        
        /// <summary>
        /// The scale that should be applied to the <see cref="sourceSplineContainer"/> while generating this Spline. 
        /// </summary>
        /// <remarks>
        /// See https://forum.unity.com/threads/splines-spline-animates-speed-movement-method.1392508/
        /// </remarks>
        [SerializeField, Tooltip("The scale that should be applied to the target while generating this Spline.")] 
        public Vector3 scale = new(1, 1, 1);

        /// <summary>
        /// The <see cref="TangentMode"/> to use. 
        /// </summary>
        [SerializeField, Tooltip("The Tangent Mode to use.")]
        public TangentMode tangentMode = TangentMode.Broken;
        
        /// <summary>
        /// The maximum distance over which knots are merged <see cref="GenerateCombinedSpline"/>. Not affected by <see cref="scale"/>. 
        /// </summary>
        [SerializeField, Tooltip("The maximum distance over which knots are merged. Not affected by scale.")] 
        public float mergeDistance = 1f;
        
        /// <summary>
        /// Whether or not to reverse the spline flow after generating.
        /// </summary>
        [SerializeField, Tooltip("Whether or not to reverse the spline flow after generating.")] 
        private bool reversed;
        
        /// <summary>
        /// Reverses the flow of a Spline.
        /// </summary>
        /// <param name="spline">The Spline.</param>
        /// <returns>The reversed Spline.</returns>
        public static Spline ReverseFlow(Spline spline)
        {
            var knots = new BezierKnot[spline.Count];
            for (var i = 0; i < knots.Length; ++i)
            {
                knots[i] = RotateKnot(spline[^(i + 1)], 180f);
            }
            spline.Knots = knots; 
            spline.SetTangentMode(new SplineRange(0, knots.Length), TangentMode.Continuous);

            return spline;
        }

        public void GenerateCombinedSpline()
        {
            if (sourceSplineContainer.Spline == null)
            {
#if UNITY_EDITOR ||  DEVELOPMENT_BUILD || UNITY_ASSERTIONS
                Debug.LogError($"Spline Container '{sourceSplineContainer}' does not have a main spline. No splines will be combined.");
#endif
                return;
            }
            var sourceTransform = sourceSplineContainer.transform;
            var childCount = sourceTransform.childCount;
            if (childCount == 0)
            {
#if UNITY_EDITOR ||  DEVELOPMENT_BUILD || UNITY_ASSERTIONS
                Debug.Log($"Spline Container '{sourceSplineContainer}' contains no splines. No splines will be combined.");
#endif
                return;
            }

            sourceSplineContainer.Spline.Clear();
            sourceSplineContainer.transform.localScale = scale;
            var sourcePosition = (float3)sourceTransform.position;

            for (var i = 0; i < childCount; i++)
            {
                if (!sourceTransform.GetChild(reversed ? childCount - 1 - i : i).TryGetComponent<SplineContainer>(out var childSpline)) continue;
                
                var childTransform = childSpline.transform;
                var childPosition = ((float3)childTransform.position - sourcePosition) / scale;
                var childRotation = childTransform.rotation;
                var childScale = Vector3.Scale(childTransform.lossyScale, new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.x)); 
                
                for (var j = 0; j < childSpline.Spline.Count; j++)
                {
                    var index = reversed ? childSpline.Spline.Count - 1 - j : j;
                    var knot = new BezierKnot(
                        childPosition + (float3)(childRotation * childSpline.Spline[index].Position) * childScale,
                        childSpline.Spline[index].TangentIn * childScale,
                        childSpline.Spline[index].TangentOut * childScale,
                        childRotation * childSpline.Spline[index].Rotation
                    );
                    if (reversed) knot = RotateKnot(knot, 180); 
                    AddKnot(knot);
                }
            }
            
#if UNITY_EDITOR
            // dirty changes to prefab 
            if (PrefabUtility.IsPartOfAnyPrefab(sourceSplineContainer)) PrefabUtility.RecordPrefabInstancePropertyModifications(sourceSplineContainer);
            if (PrefabUtility.IsPartOfAnyPrefab(gameObject)) PrefabUtility.RecordPrefabInstancePropertyModifications(gameObject);
#endif
        }

        /// <summary>
        /// Adds a <see cref="BezierKnot"/> to the <see cref="sourceSplineContainer"/>, and merges the current knot with the previous
        /// one if they are within the <see cref="mergeDistance"/>.  
        /// </summary>
        /// <param name="knot">The <see cref="BezierKnot"/> to add.</param>
        private void AddKnot(BezierKnot knot)
        {
            if (sourceSplineContainer.Spline.Count > 1 
                && Vector3.Distance(
                    sourceSplineContainer.Spline[^1].Transform(sourceSplineContainer.transform.localToWorldMatrix).Position, 
                    knot.Transform(sourceSplineContainer.transform.localToWorldMatrix).Position)
                < mergeDistance)
            {
                knot.Position = (knot.Position + sourceSplineContainer.Spline[^1].Position) / 2;
                knot.TangentIn = sourceSplineContainer.Spline[^1].TangentIn; 
                sourceSplineContainer.Spline.RemoveAt(sourceSplineContainer.Spline.Count - 1);
            }
            sourceSplineContainer.Spline.Add(knot, tangentMode);
        }

        /// <summary>
        /// Rotates the direction of a knot along its current normal.
        /// </summary>
        /// <param name="knot">The knot.</param>
        /// <param name="degrees">The amount of degrees to rotate by.</param>
        /// <returns>The rotated knot.</returns>
        private static BezierKnot RotateKnot(BezierKnot knot, float degrees)
        {
            knot.Rotation = math.mul(quaternion.AxisAngle(math.mul(knot.Rotation, math.up()), math.radians(degrees)), knot.Rotation);
            return knot; 
        }
        
        /// <summary>
        /// Returns the subdivided positions of the spline.
        /// </summary>
        /// <param name="spline">The spline to interpolate.</param>
        /// <param name="subdivisions">The amount of subdivisions to preform on each knot of the spline.</param>
        /// <returns>A list of positions along the spline.</returns>
        public static IEnumerable<Vector3> GetPositions(ISpline spline, int subdivisions)
        {
            var positions = new Vector3[subdivisions];
            for (var i = 0; i < subdivisions; i++) positions[i] = spline.EvaluatePosition((float)i / subdivisions);
            return positions; 
        }
    }
}
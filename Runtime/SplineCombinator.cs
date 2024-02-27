using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// Component that combines splines.
/// </summary>
public class SplineCombinator: MonoBehaviour
{
    /// <summary>
    /// The <see cref="SplineContainer"/> to place the combined splines within. 
    /// </summary>
    [SerializeField, Tooltip("The Spline Container to place the combined splines within.")] 
    private ContainedSpline source;

    /// <summary>
    /// The Splines to combine. 
    /// </summary>
    [SerializeField, Tooltip("The Splines to combine.")]
    private List<ContainedSpline> splinesToCombine;
    
    /// <summary>
    /// The scale that should be applied to the <see cref="source"/> while generating this Spline. 
    /// </summary>
    [SerializeField, Tooltip("The scale that should be applied to the target while generating this Spline.")] 
    private Vector3 scale = new(1, 1, 1);
    
    /// <summary>
    /// The maximum distance over which knots are merged <see cref="GenerateCombinedSpline"/>. Not affected by <see cref="scale"/>. 
    /// </summary>
    [SerializeField, Tooltip("The maximum distance over which knots are merged. Not affected by scale.")] 
    private float mergeDistance = 1f;

    /// <summary>
    /// 
    /// The length of the out tangent vector between two <see cref="Spline"/>s. 
    /// </summary>
    [SerializeField, Tooltip("The length of the out tangent vector between two Splines ")]
    private float tension = SplineUtility.DefaultTension; 

    public void GenerateCombinedSpline()
    {
        source.Spline.Clear();
        CombineSplines(source, splinesToCombine.ConvertAll(x => (SplineInfo)x), scale, mergeDistance, tension);
#if UNITY_EDITOR
        RecordPrefabInstancePropertyModification(gameObject);
        RecordPrefabInstancePropertyModification(source.container);
#endif
    }

    /// <summary>
    /// Dirty changes to prefab if <paramref name="target"/> is part of a prefab. 
    /// </summary>
    /// <param name="target">The component or gameObject that is part of a prefab.</param>
    private static void RecordPrefabInstancePropertyModification(Object target)
    {
        if (PrefabUtility.IsPartOfAnyPrefab(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
    
    /// <summary>
    /// Combines the the <see cref="Spline"/>s within <paramref name="splineInfos"/>, then adding them to <paramref name="target"/>. 
    /// </summary>
    /// <param name="target">The <see cref="SplineInfo"/> that the combine splines will be added to.</param>
    /// <param name="splineInfos">The <see cref="SplineInfo"/>s to combine.</param>
    /// <param name="scale">
    /// Changes the scale of the <paramref name="target"/> transform, while keeping the proportions of the
    /// combined spline the same. 
    /// </param>
    /// <param name="mergeDistance">
    /// Continuous <see cref="BezierKnot"/> within this world-space distance are merged. <see cref="AddKnot"/>.
    /// </param>
    /// <param name="tension">
    /// The length of the out tangent vector between two <see cref="Spline"/>s. 
    /// </param>
    /// <remarks>
    /// See https://forum.unity.com/threads/splines-spline-animates-speed-movement-method.1392508/
    /// for why the scale parameter is necessary. 
    /// </remarks>
    public static void CombineSplines(SplineInfo target, IReadOnlyCollection<SplineInfo> splineInfos, 
        Vector3 scale, float mergeDistance = 0f, float tension = SplineUtility.CatmullRomTension)
    {
        var containerPosition = (float3)target.Transform.position;
        target.Transform.localScale = scale;

        var splineInfoCount = splineInfos.Count;
        for (var i = 0; i < splineInfoCount; i++)
        {
            var splineInfo = splineInfos.ElementAt(i); 
            var splineTransform = splineInfo.Transform; 
            var splinePosition = ((float3)splineTransform.position - containerPosition) / scale;
            var splineRotation = splineTransform.rotation;
            var splineScale = Vector3.Scale(
                splineTransform.lossyScale, 
                new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.x)); 
            
            var spline = splineInfo.Spline;
            var knotCount = spline.Count;   
            for (var j = 0; j < knotCount; j++)
            {
                TangentMode tangentMode; 
                float3 tangentIn;
                
                if (j == 0 && i > 0)
                {
                    var targetSpline = target.Spline;
                    var totalKnots = targetSpline.Count - 1; 
                    var previousKnot = targetSpline[totalKnots]; 
                    
                    // smooth the out tangent of the final knot in every non-final spline 
                    previousKnot.TangentOut = SplineUtility.GetAutoSmoothKnot(
                        previousKnot.Position,
                        targetSpline.Previous(totalKnots).Position,
                        previousKnot.Position + spline[j].Position,
                        math.mul(previousKnot.Rotation, math.up()), 
                        tension
                    ).TangentOut; 
                    target.Spline.SetTangentModeNoNotify(totalKnots, TangentMode.Broken);
                    target.Spline.SetKnotNoNotify(totalKnots, previousKnot);
                    
                    // Note: this seems to be the same thing but doesn't produce the same results for setting the prev. tangent...
                    // previousKnot.TangentOut = SplineUtility.GetAutoSmoothTangent(
                    //     targetSpline.Previous(totalKnots).Position,
                    //     previousKnot.Position,
                    //     previousKnot.Position + spline[j].Position, 
                    //     tension);

                    // smooth the out tangent of the first knot in every non-first spline 
                    tangentIn = SplineUtility.GetAutoSmoothKnot(
                        previousKnot.Position + spline[j].Position,
                        targetSpline.Previous(totalKnots).Position,
                        previousKnot.Position,
                        math.mul(spline[j].Rotation, math.up()),
                        tension
                    ).TangentIn; 
                    tangentMode = TangentMode.Broken;
                }
                else
                {
                    tangentIn = spline[j].TangentIn * splineScale; 
                    tangentMode = spline.GetTangentMode(j);
                }
                
                var knot = new BezierKnot(
                    splinePosition + (float3)(splineRotation * spline[j].Position) * splineScale,
                    tangentIn,
                    spline[j].TangentOut * splineScale,
                    splineRotation * spline[j].Rotation);
                AddKnot(target, knot, tangentMode, mergeDistance);
            }
        }
    }

    /// <summary>
    /// Adds a <see cref="BezierKnot"/> to the <paramref name="splineInfo"/>, and merges the current knot with the previous (final)
    /// one if they are within the <see cref="mergeDistance"/>.  
    /// </summary>
    /// <param name="splineInfo">The <see cref="SplineInfo"/> to add the <paramref name="knot"/> to.</param>
    /// <param name="knot">The <see cref="BezierKnot"/> to add.</param>
    /// <param name="tangentMode">The <see cref="TangentMode"/> of th knot to add.</param>
    /// <param name="mergeDistance">
    /// If the spline at <paramref name="splineInfo"/> has other knots, and the knot added is
    /// within this world-space distance of the final knot, both knots will be removed, and new
    /// knot with the average position and tangent in will be created. 
    /// </param>
    public static void AddKnot(SplineInfo splineInfo, BezierKnot knot, TangentMode tangentMode, float mergeDistance)
    {
        var spline = splineInfo.Spline; 
        if (spline.Count > 1 && mergeDistance != 0 
            && Vector3.Distance(
                spline[^1].Transform(splineInfo.Transform.localToWorldMatrix).Position, 
                knot.Transform(splineInfo.Transform.localToWorldMatrix).Position)
            < mergeDistance)
        {
            knot.Position = (knot.Position + spline[^1].Position) / 2;
            knot.TangentIn = spline[^1].TangentIn; 
            spline.RemoveAt(spline.Count - 1);
        }
        splineInfo.Spline.Add(knot, tangentMode);
    }

    /// <summary>
    /// Returns from a local-space knot a world-space using the transform matrix of the <paramref name="splineInfo"/>'s container. 
    /// </summary>
    public static BezierKnot ConvertLocalToWorldKnot(SplineInfo splineInfo, int knotIndex)
    {
        return splineInfo.Spline[knotIndex].Transform(splineInfo.Transform.localToWorldMatrix); 
    }

    /// <summary>
    /// Calculates a rotation rotated a certain amount of degrees about a certain axis. 
    /// </summary>
    /// <param name="rotation">The rotation.</param>
    /// <param name="axis">The axis to rotate along</param>
    /// <param name="degrees">The amount of degrees to rotate by.</param>
    /// <returns>The new rotation.</returns>
    public static quaternion RotateAround(quaternion rotation, float3 axis, float degrees)
    {
        return math.mul(
            math.normalizesafe(quaternion.AxisAngle(axis, math.radians(degrees))), 
            rotation
        );
    }
    
    /// <summary>
    /// Calculates a rotation rotated a certain amount of degrees about its current normal. 
    /// </summary>
    /// <param name="rotation">The current rotation.</param>
    /// <param name="degrees">The amount of degrees to rotate by.</param>
    /// <returns>The new rotation.</returns>
    public static quaternion RotateAround(quaternion rotation, float degrees)
    {
        return RotateAround(rotation, math.mul(rotation, math.up()), degrees);
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

    /// <summary>
    /// Returns the rotation where it is facing towards the <paramref name="spline"/>'s tangent at <paramref name="t"/>. 
    /// </summary>
    /// <param name="spline">The <see cref="ISpline"/>.</param>
    /// <param name="rotation">The current rotation.</param>
    /// <param name="t">A value between 0 and 1 representing a percentage of entire spline.</param>
    /// <param name="maxDegreesDelta">
    /// The angular step between the current rotation
    /// and the rotation which will align with the current tangent. 
    /// </param>
    /// <returns>The new rotation, or the current rotation</returns>
    /// <seealso cref="SplineUtility.EvaluateTangent{T}"/>
    public static Quaternion EvaluateRotation(ISpline spline, Quaternion rotation, float t, float maxDegreesDelta)
    {
        var targetRotation = Quaternion.LookRotation(spline.EvaluateTangent(t));
        return Quaternion.RotateTowards(rotation, targetRotation, maxDegreesDelta);
    }
}

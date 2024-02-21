// see: https://docs.unity3d.com/Packages/com.unity.splines@2.2/api/index.html
using System.Collections.Generic;
using MEC;
using UnityEngine;
using UnityEngine.Splines;

namespace Mushakushi.Splines.Runtime
{
    /// <summary>
    /// Moves along a spline. 
    /// </summary>
    /// <remarks>
    /// Only the main spline it utilized. 
    /// </remarks>
    public class SplineWalker: MonoBehaviour
    {
        /// <summary>
        /// The <see cref="SplineContainer"/> to follow. 
        /// </summary>
        public SplineContainer Container { get; protected set; }

        /// <summary>
        /// Whether or not the position and or rotation are being modified externally. 
        /// </summary>
        /// <seealso cref="_SetPositionAndRotation"/>
        protected bool isAnimating; 

        /// <summary>
        /// Updates the position along the <see cref="Container"/>. 
        /// </summary>
        /// <param name="t">A value between 0 and 1 representing a percentage of entire spline.</param>
        protected void UpdatePosition(float t)
        {
            if (IsNullOrEmptyContainer(Container)) return;
            transform.position = Container.EvaluatePosition(t);
        }
        
        /// <summary>
        /// Determines whether or not the <see cref="Container"/> is valid. 
        /// </summary>
        /// <returns><see cref="bool"/> Whether or not the <see cref="Container"/> is valid.</returns>
        private static bool IsNullOrEmptyContainer(SplineContainer splineContainer)
        {
            return !splineContainer || splineContainer.Splines.Count == 0;
        }

        /// <summary>
        /// Rotates along the current <see cref="Container"/>
        /// </summary>
        /// <param name="t">A value between 0 and 1 representing a percentage of entire spline.</param>
        /// <param name="maxDegreesDelta">
        /// The angular step between the current rotation
        /// and the rotation which will align with the current tangent. 
        /// </param>
        /// <seealso cref="SplineUtility.EvaluateTangent{T}"/>
        protected void AlignToSpline(float t, float maxDegreesDelta)
        {
            if (IsNullOrEmptyContainer(Container)) return;
            var targetRotation = Quaternion.LookRotation(Container.Spline.EvaluateTangent(t));
            transform.rotation = RotateTowards(transform.rotation, targetRotation, maxDegreesDelta);
        }

        /// <summary>
        /// Rotates from a rotation to a target rotation. Defines how rotation works for the entirety of the <see cref="SplineWalker"/>.  
        /// </summary>
        /// <param name="rotation">The rotation to rotate from.</param>
        /// <param name="targetRotation">The rotation to rotate to.</param>
        /// <param name="maxDegreesDelta">The angular step to rotate between the two rotations.</param>
        /// <returns><see cref="Quaternion"/> a rotation from two rotations by the <see cref="maxDegreesDelta"/>.</returns>
        protected virtual Quaternion RotateTowards(Quaternion rotation, Quaternion targetRotation, float maxDegreesDelta)
        {
            return Quaternion.RotateTowards(transform.rotation, targetRotation, maxDegreesDelta);
        }

        /// <summary>
        /// Changes the <see cref="SplineAnimate.Container"/> over time while maintaining the state of <see cref="SplineAnimate.IsPlaying"/>.
        /// </summary>
        /// <param name="splineContainer"></param>
        /// <param name="steps">The amount of steps over which to change splines.</param>
        /// <param name="t">A value between 0 and 1 representing a percentage of entire spline.</param>
        protected void ChangeSpline(SplineContainer splineContainer, int steps, int t)
        {
            if (IsNullOrEmptyContainer(splineContainer)) return;
            Timing.RunCoroutine(_ChangeSpline(splineContainer, steps, t), Segment.LateUpdate);
        }

        private IEnumerator<float> _ChangeSpline(SplineContainer splineContainer, int steps, int t)
        {
            var targetPosition = splineContainer.EvaluatePosition(t);
            var targetRotation = Quaternion.LookRotation(splineContainer.EvaluateTangent(t));
            
            yield return Timing.WaitUntilDone(Timing.RunCoroutine(_SetPositionAndRotation(targetPosition, targetRotation, steps), 
                Segment.LateUpdate));
            
            Container = splineContainer;
        }
        
        /// <summary>
        /// Moves to the point on a spline nearest a point over a fixed step interval. 
        /// </summary>
        /// <param name="point">The input point to compare.</param>
        /// <param name="steps">The amount of steps over which to move.</param>
        /// <param name="resolution">How many segments to split a spline into when calculating the nearest point.</param>
        /// <param name="iterations">
        /// The nearest point is calculated by finding the nearest point on the entire length of the spline
        /// divided into equally spaced line segments. Successive iterations will then subdivide further the nearest segment,
        /// producing more accurate results.
        /// </param>
        /// <returns><see cref="float"/> The normalized interpolation ratio corresponding to the nearest point.</returns>
        // xml partial credit: https://docs.unity3d.com/Packages/com.unity.splines@1.0/api/UnityEngine.Splines.SplineUtility.html
        public float MoveToNearestPoint(Vector3 point, int steps, int resolution = SplineUtility.PickResolutionDefault, int iterations = 2)
        {
            SplineUtility.GetNearestPoint(Container.Spline, point, out var targetPosition, out var t, resolution, iterations);
            var targetRotation = Quaternion.LookRotation(Container.EvaluateTangent(t));
            Timing.RunCoroutine(_SetPositionAndRotation(targetPosition, targetRotation, steps), Segment.LateUpdate);
            return t;
        }

        /// <summary>
        /// Sets the position and rotation over a fixed step interval. 
        /// </summary>
        private IEnumerator<float> _SetPositionAndRotation(Vector3 position, Quaternion rotation, int steps)
        {
            isAnimating = true;
            var angleBetween = Quaternion.Angle(transform.rotation, rotation); 
            for (var i = 1; i <= steps; i++)
            {
                var t = (float)i / steps;
                var walker = transform;
                walker.position = Vector3.Lerp(walker.position, position, t);
                walker.rotation = RotateTowards(walker.rotation, rotation, angleBetween / t);
                yield return Timing.WaitForOneFrame;
            }
            isAnimating = false;
        }
    }
}
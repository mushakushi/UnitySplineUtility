using UnityEngine.Splines;

/// <summary>
/// A struct that holds a <see cref="SplineContainer"/> and the index of a <see cref="Spline"/> within that container. 
/// </summary>
/// <remarks>
/// Differs from <see cref="SplineInfo"/> in the typing of the container and spline in order to be compatible
/// with the Unity editor. 
/// </remarks>
[System.Serializable]
public struct ContainedSpline
{
    /// <summary>
    /// The <see cref="SplineContainer"/>. 
    /// </summary>
    public SplineContainer container;

    /// <summary>
    /// The index of the <see cref="Spline"/> in the <see cref="container"/> that this references.
    /// </summary>
    public int splineIndex; 
    
    /// <summary>
    /// A reference to the <see cref="Spline"/>. This may be null if the container or index are
    /// invalid.
    /// </summary>
    public Spline Spline => container != null && splineIndex > -1 && splineIndex < container.Splines.Count
        ? container.Splines[splineIndex]
        : null;
    
    public static implicit operator SplineInfo(ContainedSpline spline)
    {
        return new SplineInfo(spline.container, spline.splineIndex);
    }

    public ContainedSpline(SplineContainer container, int splineIndex)
    {
        this.container = container;
        this.splineIndex = splineIndex; 
    }
}
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GaussianBlurVolume  : VolumeComponent, IPostProcessComponent
{
    [Range(0f, 100f), Tooltip("Blur Radius Intensity")]
    public FloatParameter BlurRadius = new FloatParameter(4f);

    [Range(0, 10), Tooltip("Blur Iteration Quality")]
    public IntParameter Iteration = new IntParameter(5);

    [Range(1, 10), Tooltip("Blur Depth")]
    public FloatParameter downSample = new FloatParameter(1f);

    public bool IsActive() => downSample.value > 0f;

    public bool IsTileCompatible() => false;
}
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

// TODO: Add parameters for controlling Dual Kawase Blur
[System.Serializable, VolumeComponentMenu("Custom PostProcessing Volumes/Dual Kawase Volume/Dual Kawase Render")]
public class DualBlurCustomVolume : VolumeComponent, IPostProcessComponent
{
    [Range(0f, 100f), Tooltip("Blur Radius Intensity")]
    public FloatParameter BlurRadius = new ClampedFloatParameter(3f, 0f, 10f);

    [Range(0, 10), Tooltip("Blur Iteration Quality")]
    public IntParameter Iteration = new ClampedIntParameter(2, 1, 10);

    [Range(1, 10), Tooltip("Render Target Downscaling for Blur Depth")]
    public FloatParameter downSample = new ClampedFloatParameter(1f, 0f, 10f);

    public bool IsActive() => downSample.value > 0f;

    public bool IsTileCompatible() => false;
}

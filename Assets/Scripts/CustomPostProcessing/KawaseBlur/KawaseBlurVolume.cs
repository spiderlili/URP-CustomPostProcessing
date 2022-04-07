using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[System.Serializable, VolumeComponentMenu("Custom PostProcessing Volumes/Kawase Blur Volume/Kawase Blur Render")]
public class KawaseBlurVolume : VolumeComponent, IPostProcessComponent
{
    [Range(0f, 100f), Tooltip("Blur Radius Intensity")]
    public FloatParameter BlurRadius = new ClampedFloatParameter(3f, 0f, 10f);

    [Range(0, 10), Tooltip("Blur Iteration Quality")]
    public IntParameter Iteration = new ClampedIntParameter(5, 0, 10);

    [Range(1, 10), Tooltip("Render Target Downscaling for Blur Depth")]
    public FloatParameter downSample = new ClampedFloatParameter(1f, 0f, 10f);

    public bool IsActive() => downSample.value > 0f;

    public bool IsTileCompatible() => false;
}

using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

// TODO: Add parameters for controlling Dual Kawase Blur, configure dual blur for the volume framework to override scene properties
[System.Serializable, VolumeComponentMenu("Custom PostProcessing Volumes/Dual Kawase Volume/Dual Kawase Render")]
public class DualBlurCustomVolume : VolumeComponent, IPostProcessComponent
{
    [Range(0f, 100f), Tooltip("Larger Blur Size per iteration leads to higher blurriness, this does not affect the number of samples but may affect stability.")]
    public FloatParameter BlurRadius = new ClampedFloatParameter(3f, 0f, 10f);

    [Range(0, 10), Tooltip("Blur Iteration controls blurriness quality. This affects performance but has the best quality and stability.")]
    public IntParameter Iteration = new ClampedIntParameter(2, 1, 10);

    [Range(1, 10), Tooltip("Render Target Downscaling for Blur Depth. Larger DownSample value leads to better performance due to less pixels to process, but may make the image pixelated.")]
    public FloatParameter downSample = new ClampedFloatParameter(1f, 1f, 10f);

    public bool IsActive() => downSample.value > 0f;

    public bool IsTileCompatible() => false;
}

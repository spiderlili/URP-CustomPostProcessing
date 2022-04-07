using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

// TODO: Add parameters for controlling Dual Kawase Blur
[System.Serializable, VolumeComponentMenu("Custom PostProcessing Volumes/Dual Kawase Blur Volume/Dual Kawase Blur Render")]
public class DualKawaseBlurCustomVolume : VolumeComponent, IPostProcessComponent
{
    [Tooltip("The strength of the Dual Kawase Blur filter. ")]
    public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);
    public bool IsActive() => intensity.value > 0f;
    public bool IsTileCompatible() => false;
}

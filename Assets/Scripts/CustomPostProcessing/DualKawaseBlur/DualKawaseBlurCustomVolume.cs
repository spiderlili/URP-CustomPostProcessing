using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

[System.Serializable, VolumeComponentMenu("Custom Volume/Dual Kawase Blur Volume/Dual Kawase Blur Render")]
public class DualKawaseBlurCustomVolume : VolumeComponent, IPostProcessComponent
{
    [Tooltip("Enable Effect")]
    public BoolParameter enableEffect = new BoolParameter(true);
    public bool IsActive() => enableEffect==true;
    public bool IsTileCompatible() => false;
}

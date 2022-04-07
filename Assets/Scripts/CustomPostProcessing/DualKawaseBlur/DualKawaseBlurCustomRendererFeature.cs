using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering.Universal;
using UnityEngine;

public class DualKawaseBlurCustomRendererFeature : ScriptableRendererFeature
{
    DualKawaseBlurCustomRenderPass customRenderPass;
    [System.Serializable]
    public class BlurSetting
    {
        public RenderPassEvent Event = RenderPassEvent.AfterRenderingTransparents;
        public Material material = null;

    }

    public BlurSetting setting = new BlurSetting();

    public override void Create()
    {
        customRenderPass = new DualKawaseBlurCustomRenderPass("dualKawaseBlur");
        customRenderPass.passMat = setting.material;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        var src = renderer.cameraColorTarget;
        
        // renderer.cameraColorTarget is the pipeline rendered image => pass it to pass
        customRenderPass.Setup(src);
        renderer.EnqueuePass(customRenderPass);
    }
}

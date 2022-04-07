// Add "using System.Collections" and "using System.Collections.Generic" to make sure it shows up in settings
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering.Universal;
using UnityEngine;

public class DualKawaseBlurCustomRendererFeature : ScriptableRendererFeature
{
    // Dual Kawase Blur Shader
    public Shader dualKawaseBlurShader;
    
    // 持有屏幕缓冲RT的句柄
    private RenderTargetHandle m_CameraColorAttachment;
    
    // 持有拷贝目标RT的句柄
    private RenderTargetHandle m_CameraTransparentColorAttachment;

    // Screen copy Pass: before executing rendering logic of Dual Kawase Blur, need to copy the current screen
    private DualKawaseBlurCustomRenderPass m_dualKawaseBlurRenderPass;
    
    [System.Serializable]
    public class BlurSetting
    {
        public RenderPassEvent Event = RenderPassEvent.AfterRenderingTransparents;
        public Material material = null;

    }

    public BlurSetting setting = new BlurSetting();

    public override void Create()
    {
        // Initialize screen copy Pass: use passed renderPassEvent to determine when this pass should get executed
        m_dualKawaseBlurRenderPass = new DualKawaseBlurCustomRenderPass(RenderPassEvent.AfterRenderingTransparents);
        // m_dualKawaseBlurRenderPass.passMat = setting.material;
        
        // 映射到显存中的RT
        m_CameraColorAttachment.Init("_CameraColorTexture");
        m_CameraTransparentColorAttachment.Init("_CameraTransparentColorTexture");

    }

    // Injects one or multiple ScriptableRenderPass in the renderer
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        var src = renderer.cameraColorTarget;
        
        // renderer.cameraColorTarget is the pipeline rendered image => pass it to pass, set up parameters required by copy screen pass
        m_dualKawaseBlurRenderPass.Setup(m_CameraColorAttachment.Identifier(), m_CameraTransparentColorAttachment);
        
        // Add screen copy Pass to the queue to be executed
        renderer.EnqueuePass(m_dualKawaseBlurRenderPass);
    }
}

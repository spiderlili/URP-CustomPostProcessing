using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine;

public class DualKawaseBlurCustomRenderPass : ScriptableRenderPass
{
    public Material passMat = null;
    private RenderTargetIdentifier passSource { get; set; }

    string passTag;

    public DualKawaseBlurCustomRenderPass(string tag)
    {
        passTag = tag;
    }

    public void Setup(RenderTargetIdentifier sour)
    {
        this.passSource = sour;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get(passTag);
        cmd.Blit(passSource, passSource, passMat);

        // Find the DualKawaseBlurCustomVolume component, return if it's not enabled or not found 
        var stack = VolumeManager.instance.stack;
        DualKawaseBlurCustomVolume customVolume1 = stack.GetComponent<DualKawaseBlurCustomVolume>();
        if (customVolume1 == null){return;}
        if (!customVolume1.IsActive())return;

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }
}
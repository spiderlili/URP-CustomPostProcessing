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
        
        // Add passMat to passSource, then output to passSource
        cmd.Blit(passSource, passSource, passMat);
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }
}
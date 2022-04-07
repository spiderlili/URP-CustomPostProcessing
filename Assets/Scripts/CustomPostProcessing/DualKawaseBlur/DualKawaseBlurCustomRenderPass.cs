using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine;

public class DualKawaseBlurCustomRenderPass : ScriptableRenderPass
{
    // Display this name in FrameDebugger & other Profilers
    private const string m_ProfilerTag = "Dual Kawase Blur";
    
    private RenderTargetIdentifier m_passSource { get; set; }
    private RenderTargetHandle m_Destination;
    
    // public Material passMat = null;
    
    public DualKawaseBlurCustomRenderPass(RenderPassEvent evt) 
    { 
        // Set up Pass's render order
        renderPassEvent = evt; 
    } 

    public void Setup(RenderTargetIdentifier source, RenderTargetHandle destination)
    {
        m_passSource = source;
        m_Destination = destination;
    }
    
    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        // 给拷贝目标分配实际显存
        var descriptor = cameraTextureDescriptor;
        descriptor.depthBufferBits = 0;
        cmd.GetTemporaryRT(m_Destination.id, descriptor, FilterMode.Point);
    }
    
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
        cmd.Blit(m_passSource, m_Destination.Identifier());

        // Find the DualKawaseBlurCustomVolume component, return if it's not enabled or not found 
        var stack = VolumeManager.instance.stack;
        DualKawaseBlurCustomVolume customVolume1 = stack.GetComponent<DualKawaseBlurCustomVolume>();
        if (customVolume1 == null){return;}
        if (!customVolume1.IsActive())return;

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }
    
    public override void FrameCleanup(CommandBuffer cmd)
    {
        if (m_Destination != RenderTargetHandle.CameraTarget)
        {
            // Release copied target
            cmd.ReleaseTemporaryRT(m_Destination.id);
            m_Destination = RenderTargetHandle.CameraTarget;
        }
    }
}
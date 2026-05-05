using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

internal class BlitRenderPass : ScriptableRenderPass
{
    private readonly Material materialToBlit;
    private readonly string profilerTag;

    // This class holds the data the Render Graph needs during the execution phase
    private class PassData
    {
        public TextureHandle src;
        public TextureHandle dest;
        public Material material;
    }

    public BlitRenderPass(string profilerTag, RenderPassEvent renderPassEvent, Material materialToBlit)
    {
        this.profilerTag = profilerTag;
        this.renderPassEvent = renderPassEvent;
        this.materialToBlit = materialToBlit;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

        // Ensure we have a valid target and a material
        if (resourceData.isActiveTargetBackBuffer || materialToBlit == null)
            return;

        // In Render Graph, we use the active color texture from resourceData
        TextureHandle cameraColorTarget = resourceData.activeColorTexture;

        // Create a description for our temporary texture based on the camera's setup
        RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
        desc.depthBufferBits = 0; // We only need color for blitting

        // Create a temporary texture via the Render Graph (handled automatically)
        TextureHandle tempTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_TempBlitTexture", false);

        // --- FIRST PASS: Camera -> Temp (with material) ---
        using (var builder = renderGraph.AddRasterRenderPass<PassData>(profilerTag + "_Horizontal", out var passData))
        {
            passData.src = cameraColorTarget;
            passData.dest = tempTexture;
            passData.material = materialToBlit;

            // Define dependencies: what are we reading and what are we writing?
            builder.UseTexture(passData.src, AccessFlags.Read);
            builder.SetRenderAttachment(passData.dest, 0, AccessFlags.Write);

            builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
            {
                // Use Blitter for Unity 6 compatibility
                Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), data.material, 0);
            });
        }

        // --- SECOND PASS: Temp -> Camera (back to screen) ---
        using (var builder = renderGraph.AddRasterRenderPass<PassData>(profilerTag + "_Vertical", out var passData))
        {
            passData.src = tempTexture;
            passData.dest = cameraColorTarget;
            passData.material = materialToBlit;

            builder.UseTexture(passData.src, AccessFlags.Read);
            builder.SetRenderAttachment(passData.dest, 0, AccessFlags.Write);

            builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
            {
                // Simple blit back (no material logic needed usually, or use second pass)
                Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), 0, false);
            });
        }
    }
}
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class KawaseBlurRendererFeature : ScriptableRendererFeature
{
    [SerializeField] string _renderTargetName = "_RenderMetaballsRT";
    [SerializeField] Material _blurMaterial;
    [SerializeField, Range(1, 16)] int _blurPasses = 1;
    [SerializeField] RenderPassEvent _event = RenderPassEvent.AfterRenderingTransparents;

    KawaseBlurRenderPass _blurPass;

    public override void Create()
    {
        // Use the string name directly; Render Graph handles ID conversion
        _blurPass = new KawaseBlurRenderPass("KawaseBlur", _renderTargetName)
        {
            renderPassEvent = _event,
            Passes = _blurPasses,
            BlurMaterial = _blurMaterial
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_blurMaterial == null) return;
        renderer.EnqueuePass(_blurPass);
    }
}

public class KawaseBlurRenderPass : ScriptableRenderPass
{
    public Material BlurMaterial;
    public int Passes;
    private readonly string _sourceTextureName;
    private readonly int _offsetId = Shader.PropertyToID("_offset");

    // Data passed to the Render Graph execution lambdas
    private class PassData
    {
        public TextureHandle src;
        public Material material;
        public float offset;
    }

    public KawaseBlurRenderPass(string profilerTag, string sourceTextureName)
    {
        _sourceTextureName = sourceTextureName;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (BlurMaterial == null) return;

        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

        // 1. Get the source texture handle
        // We look for the texture global ID defined by your previous metaball pass
        // TextureHandle sourceTex = renderGraph.ImportTexture(Shader.PropertyToID(_sourceTextureName));
        TextureHandle sourceTex = resourceData.activeColorTexture;
        if (!sourceTex.IsValid()) return;

        // 2. Create temporary textures for ping-ponging
        RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
        desc.depthBufferBits = 0;
        desc.msaaSamples = 1;

        TextureHandle tmpRT1 = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "KawaseTemp1", false);
        TextureHandle tmpRT2 = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "KawaseTemp2", false);

        TextureHandle currentSrc = sourceTex;
        TextureHandle currentDst = tmpRT1;

        // 3. Loop through passes
        for (int i = 0; i < Passes; i++)
        {
            float offset = (i == 0) ? 1.5f : 0.5f + i;

            using (var builder = renderGraph.AddRasterRenderPass<PassData>($"KawasePass_{i}", out var passData))
            {
                passData.src = currentSrc;
                passData.material = BlurMaterial;
                passData.offset = offset;

                // Tell Render Graph what we are reading and writing
                builder.UseTexture(passData.src, AccessFlags.Read);
                builder.SetRenderAttachment(currentDst, 0, AccessFlags.Write);
                
                // Set the execution logic
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    data.material.SetFloat("_offset", data.offset);
                    // Blitter is the Unity 6 replacement for cmd.Blit
                    Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), data.material, 0);
                });

                // Ping-pong targets for the next iteration
                currentSrc = currentDst;
                currentDst = (currentDst == tmpRT1) ? tmpRT2 : tmpRT1;
            }
        }
        
        // Final result is now in 'currentSrc' (the last texture written to)
        // Optionally: use another pass to blit 'currentSrc' back to the camera target if needed
    }
}
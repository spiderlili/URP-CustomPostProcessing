using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class KawaseBlurRendererFeature : ScriptableRendererFeature
{
    #region Renderer Feature
    
    KawaseBlurRenderPass _blurPass;

    const string PassTag = "RenderScreenSpaceMetaballs";
    [SerializeField] string _renderTargetId = "_RenderMetaballsRT";
    [SerializeField] LayerMask _layerMask;
    [SerializeField] Material _blurMaterial;
    [SerializeField, Range(1, 16)] int _blurPasses = 1;

    public override void Create()
    {
        int renderTargetId = Shader.PropertyToID(_renderTargetId);

        _blurPass = new KawaseBlurRenderPass("KawaseBlur", renderTargetId)
        {
            Downsample = 1,
            Passes = _blurPasses,
            BlurMaterial = _blurMaterial
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(_blurPass);
    }

    #endregion
}

#region Kawase Blur

public class KawaseBlurRenderPass : ScriptableRenderPass
{
    public Material BlurMaterial;
    public int Passes;
    public int Downsample;

    int _tmpId1;
    int _tmpId2;

    RenderTargetIdentifier _tmpRT1;
    RenderTargetIdentifier _tmpRT2;

    readonly int _blurSourceId;
    RenderTargetIdentifier _blurSourceIdentifier;

    readonly ProfilingSampler _profilingSampler;

    public KawaseBlurRenderPass(string profilerTag, int blurSourceId)
    {
        _profilingSampler = new ProfilingSampler(profilerTag);
        _blurSourceId = blurSourceId;
    }

    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        _blurSourceIdentifier = new RenderTargetIdentifier(_blurSourceId);
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        var width = cameraTextureDescriptor.width / Downsample;
        var height = cameraTextureDescriptor.height / Downsample;

        _tmpId1 = Shader.PropertyToID("tmpBlurRT1");
        _tmpId2 = Shader.PropertyToID("tmpBlurRT2");
        cmd.GetTemporaryRT(_tmpId1, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.ARGB32);
        cmd.GetTemporaryRT(_tmpId2, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.ARGB32);

        _tmpRT1 = new RenderTargetIdentifier(_tmpId1);
        _tmpRT2 = new RenderTargetIdentifier(_tmpId2);

        ConfigureTarget(_tmpRT1);
        ConfigureTarget(_tmpRT2);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        RenderTextureDescriptor opaqueDesc = renderingData.cameraData.cameraTargetDescriptor;
        opaqueDesc.depthBufferBits = 0;

        CommandBuffer cmd = CommandBufferPool.Get();
        using (new ProfilingScope(cmd, _profilingSampler))
        {
            // first pass
            cmd.SetGlobalFloat("_offset", 1.5f);
            cmd.Blit(_blurSourceIdentifier, _tmpRT1, BlurMaterial);

            for (var i = 1; i < Passes - 1; i++)
            {
                cmd.SetGlobalFloat("_offset", 0.5f + i);
                cmd.Blit(_tmpRT1, _tmpRT2, BlurMaterial);

                // pingpong
                var rttmp = _tmpRT1;
                _tmpRT1 = _tmpRT2;
                _tmpRT2 = rttmp;
            }

            // final pass
            cmd.SetGlobalFloat("_offset", 0.5f + Passes - 1f);
        }

        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        CommandBufferPool.Release(cmd);
    }

    public override void OnCameraCleanup(CommandBuffer cmd)
    {
        cmd.ReleaseTemporaryRT(_tmpId1);
        cmd.ReleaseTemporaryRT(_tmpId2);
    }
}

#endregion
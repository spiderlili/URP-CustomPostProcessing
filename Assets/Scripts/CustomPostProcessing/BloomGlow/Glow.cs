using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable]
public class GlowSettings
{
    public int RenderingLayer = 1;
    
    [Range(0, 10)]
    public float Intensity = 2f;
    
    [Range(0, 4)]
    public int BlurPasses = 1;
    
    [Range(0, 5)]
    public int ExtraBlur = 2;

    [Range(0.5f, 20f)]
    public float BlurRadius = 0.5f;

    public Texture2D LensDirtTexture;

    public BlendingMode BlendingMode = BlendingMode.Add;
}

public class Glow : ScriptableRendererFeature
{
    private class GlowPass : ScriptableRenderPass
    {
        private readonly int blurPasses;
        
        private RenderTargetIdentifier cameraColorTargetIdent;
        private FilteringSettings filteringSettings;
        private readonly ShaderTagId shaderTagId = new ShaderTagId("Glow");
        private readonly RenderTargetIdentifier glowMap;
        private RenderTargetIdentifier tempRT1;
        private RenderTargetIdentifier tempRT2;

        private Material blurMaterial;
        private Material blendMaterial;

        private BlendingMode blendingMode;
        private readonly float intensity;
        private readonly float blurRadius;
        private readonly float extraBlur;
        
        private static readonly int offsetId = Shader.PropertyToID("_Offset");
        private static readonly int blurRadiusId = Shader.PropertyToID("_BlurRadius");
        private static readonly int lensDirtId = Shader.PropertyToID("_LensDirtTex");
        private static readonly int extraBlurId = Shader.PropertyToID("_ExtraBlur");
        private static readonly int glowIntensity = Shader.PropertyToID("_Intensity");
        private static readonly int glowMapId = Shader.PropertyToID("_GlowMap");
        private static readonly int tempRT1Id = Shader.PropertyToID("GlowRT1");
        private static readonly int tempRT2Id = Shader.PropertyToID("GlowRT2");
        
        private static readonly int sourceBlendId = Shader.PropertyToID("_SourceBlendMode");
        private static readonly int destinationBlendId = Shader.PropertyToID("_DestinationBlendMode");

        public GlowPass(GlowSettings settings)
        {
            blurPasses = settings.BlurPasses;
            blurRadius = settings.BlurRadius;
            extraBlur = settings.ExtraBlur;
            intensity = settings.Intensity;
            blendingMode = settings.BlendingMode;
            
            uint renderingLayerMask = (uint)1 << settings.RenderingLayer;
            LayerMask everything = -1;
            filteringSettings = new FilteringSettings(RenderQueueRange.all, everything, renderingLayerMask);

            blurMaterial = new Material(Shader.Find("PostProcessing/Kawase Blur"));
            blurMaterial.SetFloat(blurRadiusId, settings.BlurRadius / blurPasses);
            blurMaterial.SetFloat(extraBlurId, settings.ExtraBlur);
            
            blendMaterial = new Material(Shader.Find("PostProcessing/Blend Glow"));
            blendMaterial.SetFloat(glowIntensity, settings.Intensity);
            RenderingUtils.SetMaterialBlendMode(settings.BlendingMode, blendMaterial, sourceBlendId, destinationBlendId);

            glowMap = new RenderTargetIdentifier(glowMapId);
            tempRT1 = new RenderTargetIdentifier(tempRT1Id);
            tempRT2 = new RenderTargetIdentifier(tempRT2Id);

            if (settings.LensDirtTexture != null) {
                blendMaterial.EnableKeyword("_LENSDIRT");
                blendMaterial.SetTexture(lensDirtId, settings.LensDirtTexture);
            } else {
                blendMaterial.DisableKeyword("_LENSDIRT");
                blendMaterial.SetTexture(lensDirtId, null);
            }
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            base.Configure(cmd, cameraTextureDescriptor);
            
            if (blurMaterial != null && blendMaterial != null && blurMaterial.HasProperty(blurRadiusId)) {
                return;
            }

            blurMaterial = new Material(Shader.Find("PostProcessing/Kawase Blur"));
            blurMaterial.SetFloat(blurRadiusId, blurRadius / blurPasses);
            blurMaterial.SetFloat(extraBlurId, extraBlur);
            
            blendMaterial = new Material(Shader.Find("PostProcessing/Blend Glow"));
            blendMaterial.SetFloat(glowIntensity, intensity);
            RenderingUtils.SetMaterialBlendMode(blendingMode, blendMaterial, sourceBlendId, destinationBlendId);
        }

        public void SetCameraColorTarget(RenderTargetIdentifier cameraColorTargetIdent)
        {
            this.cameraColorTargetIdent = cameraColorTargetIdent;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor cameraTextureDescriptor = renderingData.cameraData.cameraTargetDescriptor;
            cameraTextureDescriptor.depthBufferBits = 0;
            cameraTextureDescriptor.useMipMap = true;
            cameraTextureDescriptor.mipCount = 5;

            cmd.GetTemporaryRT(glowMapId, cameraTextureDescriptor, FilterMode.Bilinear);
            cmd.GetTemporaryRT(tempRT1Id, cameraTextureDescriptor, FilterMode.Bilinear);
            cmd.GetTemporaryRT(tempRT2Id, cameraTextureDescriptor, FilterMode.Bilinear);

            ConfigureTarget(glowMapId);
            cmd.SetRenderTarget(glowMapId);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!blurMaterial || !blendMaterial) {
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get();

            using (new ProfilingScope(cmd, new ProfilingSampler("Glow"))) {

                cmd.ClearRenderTarget(true, true, Color.clear);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                DrawingSettings drawSettings =
                    CreateDrawingSettings(shaderTagId, ref renderingData, SortingCriteria.BackToFront);
                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filteringSettings);
                
                cmd.Blit(glowMap, tempRT1);

                for (int i = 0; i < blurPasses * 2; i++) {
                    blurMaterial.SetFloat(offsetId, (i + 0.5f) / 2f);
                    cmd.Blit(tempRT1, tempRT2, blurMaterial);

                    (tempRT1, tempRT2) = (tempRT2, tempRT1);
                }

                cmd.Blit(tempRT1, cameraColorTargetIdent, blendMaterial);
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(tempRT1Id);
            cmd.ReleaseTemporaryRT(tempRT2Id);
        }
    }

    public GlowSettings settings = new GlowSettings();
    private GlowPass glowPass;

    public override void Create()
    {
        glowPass = new GlowPass(settings) {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(glowPass);
        glowPass.SetCameraColorTarget(renderer.cameraColorTarget);
    }
}

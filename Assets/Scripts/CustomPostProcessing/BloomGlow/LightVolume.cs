using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable]
public class LightVolumeSettings
{
    [Range(0, 4)]
    public int BlurPasses = 1;
    
    [Range(0, 5)]
    public int ExtraBlur = 2;

    [Range(0.5f, 20f)]
    public float BlurRadius = 0.5f;

    public Texture2D LensDirtTexture;

    public BlendingMode BlendingMode = BlendingMode.Add;
}

public class LightVolume : ScriptableRendererFeature
{
    private class LightVolumePass : ScriptableRenderPass
    {
        private readonly int blurPasses;
        
        private RenderTargetIdentifier cameraColorTargetIdent;
        private FilteringSettings filteringSettings;
        private readonly ShaderTagId shaderTagId = new ShaderTagId("LightVolume");
        private RenderTargetIdentifier tempRT1;
        private RenderTargetIdentifier tempRT2;

        private Material blurMaterial;
        private Material blendMaterial;

        private readonly BlendingMode blendingMode;
        private readonly float blurRadius;
        private readonly float extraBlur;

        private static readonly int offsetId = Shader.PropertyToID("_Offset");
        private static readonly int blurRadiusId = Shader.PropertyToID("_BlurRadius");
        private static readonly int lensDirtId = Shader.PropertyToID("_LensDirtTex");
        private static readonly int extraBlurId = Shader.PropertyToID("_ExtraBlur");
        private static readonly int tempRT1Id = Shader.PropertyToID("LightVolumeRT1");
        private static readonly int tempRT2Id = Shader.PropertyToID("LightVolumeRT2");
        
        private static readonly int sourceBlendId = Shader.PropertyToID("_SourceBlendMode");
        private static readonly int destinationBlendId = Shader.PropertyToID("_DestinationBlendMode");

        public LightVolumePass(LightVolumeSettings settings)
        {
            blurPasses = settings.BlurPasses;
            blurRadius = settings.BlurRadius;
            extraBlur = settings.ExtraBlur;
            blendingMode = settings.BlendingMode;
            
            filteringSettings = FilteringSettings.defaultValue;
            
            blurMaterial = new Material(Shader.Find("PostProcessing/Kawase Blur"));
            blurMaterial.SetFloat(blurRadiusId, blurRadius / blurPasses);
            blurMaterial.SetFloat(extraBlurId, extraBlur);

            blendMaterial = new Material(Shader.Find("PostProcessing/Blend"));
            RenderingUtils.SetMaterialBlendMode(settings.BlendingMode, blendMaterial, sourceBlendId, destinationBlendId);

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
            
            blendMaterial = new Material(Shader.Find("PostProcessing/Blend"));
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

            cmd.GetTemporaryRT(tempRT1Id, cameraTextureDescriptor, FilterMode.Bilinear);
            cmd.GetTemporaryRT(tempRT2Id, cameraTextureDescriptor, FilterMode.Bilinear);

            ConfigureTarget(tempRT1Id);
            cmd.SetRenderTarget(tempRT1Id);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!blurMaterial || !blendMaterial) {
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get();

            using (new ProfilingScope(cmd, new ProfilingSampler("LightVolume"))) {

                cmd.ClearRenderTarget(true, true, Color.clear);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                DrawingSettings drawSettings =
                    CreateDrawingSettings(shaderTagId, ref renderingData, SortingCriteria.BackToFront);
                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filteringSettings);

                for (int i = 0; i < blurPasses * 2; i++) {
                    blurMaterial.SetFloat(offsetId, i + 0.5f);
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

    public LightVolumeSettings settings = new LightVolumeSettings();
    private LightVolumePass lightVolumePass;

    public override void Create()
    {
        lightVolumePass = new LightVolumePass(settings) {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(lightVolumePass);
        lightVolumePass.SetCameraColorTarget(renderer.cameraColorTarget);
    }
}

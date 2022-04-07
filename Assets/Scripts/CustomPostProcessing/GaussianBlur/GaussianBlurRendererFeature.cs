using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GaussianBlurRendererFeature : ScriptableRendererFeature
{
    GaussianBlurPass _gaussianBlurPass;

    public override void Create()
    {
        _gaussianBlurPass = new GaussianBlurPass(RenderPassEvent.BeforeRenderingPostProcessing);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        _gaussianBlurPass.Setup(renderer.cameraColorTarget);
        renderer.EnqueuePass(_gaussianBlurPass);
    }
}

public class GaussianBlurPass : ScriptableRenderPass
{
    static readonly string ProfilerRenderTag = "Gaussian Blur";
    static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    static readonly int TempTargetId = Shader.PropertyToID("_TempTargetTestBlur");
    static readonly int FocusPowerId = Shader.PropertyToID("_FocusPower");
    static readonly int FocusDetailId = Shader.PropertyToID("_FocusDetail");
    static readonly int FocusScreenPositionId = Shader.PropertyToID("_FocusScreenPosition");
    static readonly int ReferenceResolutionXId = Shader.PropertyToID("_ReferenceResolutionX");
    GaussianBlurVolume GaussianBlur;
    Material GaussianBlurMaterial;
    RenderTargetIdentifier currentTarget;

    public GaussianBlurPass(RenderPassEvent evt)
    {
        renderPassEvent = evt;
        var shader = Shader.Find("PostProcessing/Gaussian Blur");
        if (shader == null)
        {
            Debug.LogError("Gaussian Blur Shader not found.");
            return;
        }
        GaussianBlurMaterial = CoreUtils.CreateEngineMaterial(shader);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (GaussianBlurMaterial == null)
        {
            Debug.LogError("Gaussian Blur Material not created.");
            return;
        }

        if (!renderingData.cameraData.postProcessEnabled) return;

        var stack = VolumeManager.instance.stack;
        GaussianBlur = stack.GetComponent<GaussianBlurVolume>();
        if (GaussianBlur == null) { return; }
        if (!GaussianBlur.IsActive()) { return; }

        var cmd = CommandBufferPool.Get(ProfilerRenderTag);
        Render(cmd, ref renderingData);
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public void Setup(in RenderTargetIdentifier currentTarget)
    {
        this.currentTarget = currentTarget;
    }

    void Render(CommandBuffer cmd, ref RenderingData renderingData)
    {
        // Pass in render target
        ref var cameraData = ref renderingData.cameraData;
        var source = currentTarget;
        int destination = TempTargetId;

        // Use int to create Pixelated Box Blur effect similar to the one in MineCraft
        int w = (int)(cameraData.camera.scaledPixelWidth / GaussianBlur.downSample.value);
        int h = (int)(cameraData.camera.scaledPixelHeight / GaussianBlur.downSample.value);
        GaussianBlurMaterial.SetFloat(FocusPowerId, GaussianBlur.BlurRadius.value);

        int shaderPass = 0;
        cmd.SetGlobalTexture(MainTexId, source);
        
        cmd.GetTemporaryRT(destination, w, h, 0, FilterMode.Point, RenderTextureFormat.Default);

        cmd.Blit(source, destination);
        
        for (int i = 0; i < GaussianBlur.Iteration.value; i++)
        {
            cmd.GetTemporaryRT(destination, w / 2, h / 2, 0, FilterMode.Point, RenderTextureFormat.Default);
            cmd.Blit(destination, source, GaussianBlurMaterial, shaderPass);
            cmd.Blit(source, destination);
            cmd.Blit(destination, source, GaussianBlurMaterial, shaderPass + 1);
            cmd.Blit(source, destination);
        }
        
        for (int i = 0; i < GaussianBlur.Iteration.value; i++)
        {
            cmd.GetTemporaryRT(destination, w * 2, h * 2, 0, FilterMode.Point, RenderTextureFormat.Default);
            cmd.GetTemporaryRT(destination, w / 2, h / 2, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
            cmd.Blit(destination, source, GaussianBlurMaterial, shaderPass);
            cmd.Blit(source, destination);
            cmd.Blit(destination, source, GaussianBlurMaterial, shaderPass + 1);
            cmd.Blit(source, destination);
        }

        cmd.Blit(destination, destination, GaussianBlurMaterial, 0);
    }
}
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DualKawaseBlurCustomRendererFeature : ScriptableRendererFeature
{
    DualKawaseBlurPass _dualKawaseBlurPass;

    public override void Create()
    {
        _dualKawaseBlurPass = new DualKawaseBlurPass(RenderPassEvent.BeforeRenderingPostProcessing);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        _dualKawaseBlurPass.Setup(renderer.cameraColorTarget);
        renderer.EnqueuePass(_dualKawaseBlurPass);
    }
}

public class DualKawaseBlurPass : ScriptableRenderPass
{
    static readonly string ProfilerRenderTag = "Dual Kawase Blur";
    static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    static readonly int TempTargetId = Shader.PropertyToID("_TempTargetTestBlur");
    static readonly int FocusPowerId = Shader.PropertyToID("_FocusPower");
    static readonly int FocusDetailId = Shader.PropertyToID("_FocusDetail");
    static readonly int FocusScreenPositionId = Shader.PropertyToID("_FocusScreenPosition");
    static readonly int ReferenceResolutionXId = Shader.PropertyToID("_ReferenceResolutionX");
    DualKawaseBlurCustomVolume DualKawaseBlur;
    Material DualKawaseBlurMaterial;
    RenderTargetIdentifier currentTarget;

    public DualKawaseBlurPass(RenderPassEvent evt)
    {
        renderPassEvent = evt;
        var shader = Shader.Find("PostProcessing/Dual Kawase Blur");
        if (shader == null)
        {
            Debug.LogError("Dual Kawase Blur Shader not found.");
            return;
        }
        DualKawaseBlurMaterial = CoreUtils.CreateEngineMaterial(shader);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (DualKawaseBlurMaterial == null)
        {
            Debug.LogError("Dual Kawase Blur Material not created.");
            return;
        }

        if (!renderingData.cameraData.postProcessEnabled) return;

        var stack = VolumeManager.instance.stack;
        DualKawaseBlur = stack.GetComponent<DualKawaseBlurCustomVolume>();
        if (DualKawaseBlur == null) { return; }
        if (!DualKawaseBlur.IsActive()) { return; }

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
        int w = (int)(cameraData.camera.scaledPixelWidth / DualKawaseBlur.downSample.value);
        int h = (int)(cameraData.camera.scaledPixelHeight / DualKawaseBlur.downSample.value);
        DualKawaseBlurMaterial.SetFloat(FocusPowerId, DualKawaseBlur.BlurRadius.value);

        int shaderPass = 0;
        cmd.SetGlobalTexture(MainTexId, source);
        
        cmd.GetTemporaryRT(destination, w, h, 0, FilterMode.Point, RenderTextureFormat.Default);

        cmd.Blit(source, destination);
        
        for (int i = 0; i < DualKawaseBlur.Iteration.value; i++)
        {
            cmd.GetTemporaryRT(destination, w / 2, h / 2, 0, FilterMode.Point, RenderTextureFormat.Default);
            cmd.Blit(destination, source, DualKawaseBlurMaterial, shaderPass);
            cmd.Blit(source, destination);
            cmd.Blit(destination, source, DualKawaseBlurMaterial, shaderPass + 1);
            cmd.Blit(source, destination);
        }
        
        for (int i = 0; i < DualKawaseBlur.Iteration.value; i++)
        {
            cmd.GetTemporaryRT(destination, w * 2, h * 2, 0, FilterMode.Point, RenderTextureFormat.Default);
            cmd.GetTemporaryRT(destination, w / 2, h / 2, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
            cmd.Blit(destination, source, DualKawaseBlurMaterial, shaderPass);
            cmd.Blit(source, destination);
            cmd.Blit(destination, source, DualKawaseBlurMaterial, shaderPass + 1);
            cmd.Blit(source, destination);
        }

        cmd.Blit(destination, destination, DualKawaseBlurMaterial, 0);
    }
}
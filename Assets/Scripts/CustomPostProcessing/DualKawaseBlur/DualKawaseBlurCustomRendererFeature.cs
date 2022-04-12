using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Add a ScriptableRendererFeature to the ScriptableRenderer. Use this scriptable renderer feature to inject render passes into the renderer.
public class DualKawaseBlurCustomRendererFeature : ScriptableRendererFeature
{
    DualKawaseBlurPass _dualKawaseBlurPass;

    // Gets called when: serialization happens, enable / disable the render feature, a property is changed in the inspector of the render feature.
    public override void Create()
    {
        _dualKawaseBlurPass = new DualKawaseBlurPass(RenderPassEvent.BeforeRenderingPostProcessing);
    }

    // Injects >= 1 render passes in the renderer. Gets called when setting up the renderer & every frame (once per-camera). Will NOT be called if the renderer feature is disabled in the renderer inspector.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        _dualKawaseBlurPass.Setup(renderer.cameraColorTarget);
        
        // Here you can queue up multiple passes after each other.
        renderer.EnqueuePass(_dualKawaseBlurPass);
    }
}

// Implement a logical rendering pass that can be used to extend URP renderer.
public class DualKawaseBlurPass : ScriptableRenderPass
{
    // The profiler tag that will show up in the frame debugger.
    static readonly string ProfilerRenderTag = "Dual Kawase Blur";
    static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    static readonly int TempTargetId = Shader.PropertyToID("_TempTargetDualkawaseBlur");
    static readonly int OffsetId = Shader.PropertyToID("_Offset");
    static readonly int FocusDetailId = Shader.PropertyToID("_FocusDetail");
    static readonly int FocusScreenPositionId = Shader.PropertyToID("_FocusScreenPosition");
    static readonly int ReferenceResolutionXId = Shader.PropertyToID("_ReferenceResolutionX");
    DualKawaseBlurCustomVolume DualKawaseBlur;
    Material DualKawaseBlurMaterial;
    private RenderTargetIdentifier currentTarget{ get; set; }

    // [down,up]
    Level[] m_Pyramid;
    const int k_MaxPyramidSize = 16;
    struct Level
    {
        internal int down;
        internal int up;
    }
    
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
        DualKawaseBlurMaterial.SetFloat(OffsetId, DualKawaseBlur.BlurRadius.value);

        int shaderPass = 0;
        cmd.SetGlobalTexture(MainTexId, source);
        
        cmd.GetTemporaryRT(destination, w, h, 0, FilterMode.Bilinear, RenderTextureFormat.Default);

        cmd.Blit(source, destination);
        
        // Downsample
        /*
        RenderTargetIdentifier lastDown = currentTarget;
        for (int i = 0; i < DualKawaseBlur.Iteration.value; i++)
        {
            int mipDown = m_Pyramid[i].down;
            int mipUp = m_Pyramid[i].up;
            cmd.GetTemporaryRT(mipDown, w, h,0, FilterMode.Bilinear, RenderTextureFormat.Default);
            cmd.GetTemporaryRT(mipUp, w, h,0, FilterMode.Bilinear,  RenderTextureFormat.Default);
            cmd.Blit(lastDown, mipDown);

            lastDown = mipDown;
            w = Mathf.Max(w / 2, 1);
            h = Mathf.Max(h / 2, 1);
        }
        */
        
        for (int i = 0; i < DualKawaseBlur.Iteration.value; i++)
        {
            cmd.GetTemporaryRT(destination, w / 2, h / 2, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
            cmd.Blit(destination, source, DualKawaseBlurMaterial, shaderPass);
            cmd.Blit(source, destination);
            cmd.Blit(destination, source, DualKawaseBlurMaterial, shaderPass + 1);
            cmd.Blit(source, destination);
        }
        
        for (int i = 0; i < DualKawaseBlur.Iteration.value; i++)
        {
            cmd.GetTemporaryRT(destination, w * 2, h * 2, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
            cmd.GetTemporaryRT(destination, w / 2, h / 2, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
            cmd.Blit(destination, source, DualKawaseBlurMaterial, shaderPass);
            cmd.Blit(source, destination);
            cmd.Blit(destination, source, DualKawaseBlurMaterial, shaderPass + 1);
            cmd.Blit(source, destination);
        }
        

        cmd.Blit(destination, destination, DualKawaseBlurMaterial, 0);
    }
}
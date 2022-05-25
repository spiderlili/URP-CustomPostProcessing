using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Add a ScriptableRendererFeature to the ScriptableRenderer. Use this scriptable renderer feature to inject render passes into the renderer.
public class DualBlurRendererFeature : ScriptableRendererFeature
{
    private DualBlurRenderPass m_dualBlurRenderPass;
    
    // Initialization. Gets called when: serialization happens, enable / disable the render feature, a property is changed in the inspector of the render feature.
    public override void Create()
    {
        m_dualBlurRenderPass = new DualBlurRenderPass(RenderPassEvent.BeforeRenderingPostProcessing);
    }
    
    // Injects >= 1 render passes in the renderer. Gets called when setting up the renderer & every frame (once per-camera). Will NOT be called if the renderer feature is disabled in the renderer inspector.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        m_dualBlurRenderPass.Setup(renderer.cameraColorTarget);
        renderer.EnqueuePass(m_dualBlurRenderPass);
    }
    
    // Implement a logical rendering pass that can be used to extend URP renderer.
    class DualBlurRenderPass : ScriptableRenderPass
    {
        static readonly string ProfilerRenderTag = "Dual Blur"; // The profiler tag that will show up in the frame debugger.
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int TempTargetId = Shader.PropertyToID("_TempTargetDualkawaseBlur");
        static readonly int OffsetId = Shader.PropertyToID("_BlurOffset");

        RenderTargetIdentifier buffer1;//RTa1's ID
        RenderTargetIdentifier buffer2;//RTa2's ID
        string RenderFetureName;

        private DualBlurCustomVolume dualBlurVolume;
        Material DualKawaseBlurMaterial;
        private RenderTargetIdentifier currentRenderTarget{get;set;}
        
        struct LEVEL
        {
            public int down;
            public int up;
        };

        LEVEL[] my_level;
        private int maxLevel = 16;

        public DualBlurRenderPass(RenderPassEvent evt)
        {
            renderPassEvent = evt;
            var shader = Shader.Find("PostProcessing/DualBlur(Kawase)");
            if (shader == null)
            {
                Debug.LogError("Dual Kawase Blur Shader not found.");
                return;
            }
            DualKawaseBlurMaterial = CoreUtils.CreateEngineMaterial(shader);
        }

        public void Setup(RenderTargetIdentifier currentTarget)//init, receive img passed from render feature
        {
            this.currentRenderTarget = currentTarget;
            my_level = new LEVEL[maxLevel];
            for (int t = 0; t < maxLevel; t++)//32 ID: up & down = 16 each, use id instead of temp RT
            {
                my_level[t] = new LEVEL
                {
                    down = Shader.PropertyToID("_BlurMipDown"+t),
                    up = Shader.PropertyToID("_BlurMipUp"+t)
                };
            }
        }

        // The actual execution of the pass. This is where custom rendering occurs.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // Debug warning
            if (DualKawaseBlurMaterial == null)
            {
                Debug.LogError("Dual Kawase Blur Material not created.");
                return;
            }
            if (!renderingData.cameraData.postProcessEnabled) return;
            
            // Get volume
            var stack = VolumeManager.instance.stack;
            dualBlurVolume = stack.GetComponent<DualBlurCustomVolume>();
            if (dualBlurVolume == null) { return; }
            if (!dualBlurVolume.IsActive()) { return; }
            
            // Grab a command buffer. We put the actual execution of the pass inside of a profiling scope.
            CommandBuffer cmd = CommandBufferPool.Get(ProfilerRenderTag);
            DualKawaseBlurMaterial.SetFloat("_BlurRadiusOffset",dualBlurVolume.BlurRadius.value);
            //cmd.SetGlobalFloat("_BlurRadiusOffset",dualBlurVolume.BlurRadius.value);// better performance using global - will affect other shaders
            RenderTextureDescriptor opaquedesc = renderingData.cameraData.cameraTargetDescriptor;
            int width = opaquedesc.width/(int)dualBlurVolume.downSample.value;
            int height = opaquedesc.height/(int)dualBlurVolume.downSample.value;
            opaquedesc.depthBufferBits = 0;

            // Downsampling
            RenderTargetIdentifier LastDown = currentRenderTarget;// init RT

            for(int t = 0; t < dualBlurVolume.Iteration.value; t++)
            {
                int midDown = my_level[t].down;//middle down
                int midUp = my_level[t].up; //middle Up
                cmd.GetTemporaryRT(midDown,width,height,0,FilterMode.Bilinear,RenderTextureFormat.ARGB32);// each iteration's RT will be half the original size
                cmd.GetTemporaryRT(midUp,width,height,0,FilterMode.Bilinear,RenderTextureFormat.ARGB32);// in UP iteration: does not need to request RT 
                cmd.Blit(LastDown,midDown,DualKawaseBlurMaterial,0);//down CALCULATION pass
                LastDown = midDown;
                width = Mathf.Max(width/2,1);// Reduce size for each iteration
                height = Mathf.Max(height/2,1);
            }

            // UpSampling
            int lastUp = my_level[dualBlurVolume.Iteration.value-1].down; //use down's last img as up's 1st img
            for(int j = dualBlurVolume.Iteration.value-2; j >= 0; j--) // -2: 1st time -1, but 1st time is a copy, need to add up after iteration 
            {
                int midUp = my_level[j].up;
                cmd.Blit(lastUp,midUp,DualKawaseBlurMaterial,1);
                lastUp = midUp;
            }
            
            cmd.Blit(lastUp,currentRenderTarget,DualKawaseBlurMaterial,1);
            
            // Execute the command buffer and release it
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
        
        
        // Called when the camera has finished rendering.
        // Here we release/cleanup any allocated resources that were created by this pass.
        // Gets called for all cameras in a camera stack.
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            if (cmd == null) throw new ArgumentNullException("cmd");
        
            // Since we created a temporary render texture in OnCameraSetup, we need to release the memory here to avoid a leak.
            for(int k = 0;k<dualBlurVolume.Iteration.value;k++)// clear RT
            {
                cmd.ReleaseTemporaryRT(my_level[k].up);
                cmd.ReleaseTemporaryRT(my_level[k].down);
            }
        }
    }
} 

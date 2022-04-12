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

        RenderTargetIdentifier buffer1;//RTa1的ID
        RenderTargetIdentifier buffer2;//RTa2的ID
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
        int maxLevel = 16;//指定一个最大值来限制申请的ID的数量，这里限制到16个，这么多肯定用不完了

        public DualBlurRenderPass(RenderPassEvent evt)//构造函数
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

        public void Setup(RenderTargetIdentifier currentTarget)//初始化，接收render feature传的图
        {
            this.currentRenderTarget = currentTarget;
            my_level = new LEVEL[maxLevel];
            for (int t = 0; t < maxLevel; t++)//申请32个ID的，up和down各16个，用这个id去代替临时RT来使用
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
            CommandBuffer cmd = CommandBufferPool.Get(ProfilerRenderTag);//定义cmd
            DualKawaseBlurMaterial.SetFloat("_BlurRadiusOffset",dualBlurVolume.BlurRadius.value);//指定材质参数
            //cmd.SetGlobalFloat("_BlurRadiusOffset",dualBlurVolume.BlurRadius.value);//设置模糊,但是我不想全局设置怕影响其他的shader，所以注销它了用上面那个，但是cmd这个性能可能好些？
            RenderTextureDescriptor opaquedesc = renderingData.cameraData.cameraTargetDescriptor;//定义屏幕图像参数结构体
            int width = opaquedesc.width/(int)dualBlurVolume.downSample.value;//第一次降采样是使用的参数，后面就是除2去降采样了
            int height = opaquedesc.height/(int)dualBlurVolume.downSample.value;
            opaquedesc.depthBufferBits = 0;

            // Downsampling
            RenderTargetIdentifier LastDown = currentRenderTarget;//把初始图像作为lastdown的起始图去计算

            for(int t = 0; t < dualBlurVolume.Iteration.value; t++)
            {
                int midDown = my_level[t].down;//middle down ，即间接计算down的工具人ID
                int midUp = my_level[t].up; //middle Up ，即间接计算的up工具人ID
                cmd.GetTemporaryRT(midDown,width,height,0,FilterMode.Bilinear,RenderTextureFormat.ARGB32);//对指定高宽申请RT，每个循环的指定RT都会变小为原来一半
                cmd.GetTemporaryRT(midUp,width,height,0,FilterMode.Bilinear,RenderTextureFormat.ARGB32);//同上，但是这里申请了并未计算，先把位置霸占了，这样在UP的循环里就不用申请RT了
                cmd.Blit(LastDown,midDown,DualKawaseBlurMaterial,0);//计算down的pass
                LastDown = midDown;
                width = Mathf.Max(width/2,1);//每次循环都降尺寸
                height = Mathf.Max(height/2,1);
            }

            // UpSampling
            int lastUp = my_level[dualBlurVolume.Iteration.value-1].down;//把down的最后一次图像当成up的第一张图去计算up
            for(int j = dualBlurVolume.Iteration.value-2; j >= 0; j--)//这里减2是因为第一次已经有了要减去1，但是第一次是直接复制的，所以循环完后还得补一次up
            {
                int midUp = my_level[j].up;
                cmd.Blit(lastUp,midUp,DualKawaseBlurMaterial,1);//这里直接开干就是因为在down过程中已经把RT的位置霸占好了，这里直接用，不虚
                lastUp = midUp;
            }
            
            cmd.Blit(lastUp,currentRenderTarget,DualKawaseBlurMaterial,1);//补一次up，顺便就输出了
            
            // Execute the command buffer and release it
            context.ExecuteCommandBuffer(cmd);//执行命令缓冲区的该命令
            CommandBufferPool.Release(cmd);//释放cmd
        }
        
        
        // Called when the camera has finished rendering.
        // Here we release/cleanup any allocated resources that were created by this pass.
        // Gets called for all cameras in a camera stack.
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            if (cmd == null) throw new ArgumentNullException("cmd");
        
            // Since we created a temporary render texture in OnCameraSetup, we need to release the memory here to avoid a leak.
            for(int k = 0;k<dualBlurVolume.Iteration.value;k++)//清RT，防止内存泄漏
            {
                cmd.ReleaseTemporaryRT(my_level[k].up);
                cmd.ReleaseTemporaryRT(my_level[k].down);
            }
        }
    }
} 
